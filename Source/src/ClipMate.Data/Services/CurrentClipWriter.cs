using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Platform.Helpers;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Every clipboard write goes through here (ADR-0004), as every write in ClipMate 7 went through <c>ClipObj.Copy</c>:
/// a stored clip is loaded and templated, the Outbound Filter is applied, and the clip becomes the Current Clip.
/// </summary>
public sealed class CurrentClipWriter : ICurrentClipWriter
{
    private readonly IClipboardService _clipboardService;
    private readonly IDatabases _databases;
    private readonly ILogger<CurrentClipWriter> _logger;
    private readonly IMessenger _messenger;
    private readonly ITemplateService _templateService;
    private CurrentClip? _currentClip;

    public CurrentClipWriter(IClipboardService clipboardService,
        IDatabases databases,
        ITemplateService templateService,
        IMessenger messenger,
        ILogger<CurrentClipWriter> logger)
    {
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _databases = databases ?? throw new ArgumentNullException(nameof(databases));
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public CurrentClip? CurrentClip => Volatile.Read(ref _currentClip);

    /// <inheritdoc />
    public async Task<Clip?> MakeCurrentAsync(string databaseKey, Guid clipId, WriteOptions options = default, CancellationToken cancellationToken = default)
    {
        // Read the clip and its content from its Database (a Remote Database is read from its Peer)
        var content = await _databases.Get(databaseKey).GetClipContentAsync(clipId, cancellationToken);
        if (content == null || content.Formats.Count == 0)
        {
            _logger.LogWarning("Clip {ClipId} in database {DatabaseKey} has no content to put on the clipboard", clipId, databaseKey);

            return null;
        }

        var clip = content.Clip;
        content.PopulateContent(clip);
        clip = _templateService.TryApplyTemplate(clip) ?? clip;

        // Recompute after loading the content, by the same rule as capture
        clip.ContentHash = ContentHasher.HashClip(clip);

        await WriteAsync(clip, databaseKey, options, cancellationToken);
        _logger.LogInformation("Put clip {ClipId} from database {DatabaseKey} on the clipboard", clip.Id, databaseKey);

        return clip;
    }

    /// <inheritdoc />
    public Task PutOnClipboardAsync(Clip content, WriteOptions options = default, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        return WriteAsync(content, null, options, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyList<ClipboardFormatData> Prepare(Clip clip, WriteOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(clip);

        var formats = _clipboardService.GetClipboardFormats(clip, IsFiltered(options));
        SetCurrentClip(new CurrentClip(clip, null));

        return formats;
    }

    /// <inheritdoc />
    public void ReportCurrent(Clip clip, string? databaseKey)
    {
        ArgumentNullException.ThrowIfNull(clip);

        SetCurrentClip(new CurrentClip(clip, databaseKey));
    }

    private async Task WriteAsync(Clip clip, string? databaseKey, WriteOptions options, CancellationToken cancellationToken)
    {
        await _clipboardService.SetClipboardContentAsync(clip, IsFiltered(options), cancellationToken);
        SetCurrentClip(new CurrentClip(clip, databaseKey));

        // The status bar confirms that the selected clip was copied
        await UiThread.RunAsync(() => _messenger.Send(new ClipboardCopiedEvent(clip)));
    }

    // There is no Outbound Filter setting yet, so only Manual Filter filters
    private static bool IsFiltered(WriteOptions options) => options.Filter == FilterMode.Forced;

    private void SetCurrentClip(CurrentClip currentClip)
    {
        Volatile.Write(ref _currentClip, currentClip);
        _messenger.Send(new CurrentClipChangedEvent(currentClip));
    }
}
