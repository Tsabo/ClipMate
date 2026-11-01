using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Platform;
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
    private readonly IConfigurationService _configurationService;
    private readonly IDatabases _databases;
    private readonly ILogger<CurrentClipWriter> _logger;
    private readonly IMessenger _messenger;
    private readonly ISoundService _soundService;
    private readonly ITemplateService _templateService;
    private CurrentClip? _currentClip;

    // The Current Clip was captured from another program and is still on the clipboard as that program put it there
    private volatile bool _currentIsUnfilteredCapture;

    public CurrentClipWriter(IClipboardService clipboardService,
        IDatabases databases,
        ITemplateService templateService,
        IConfigurationService configurationService,
        ISoundService soundService,
        IMessenger messenger,
        ILogger<CurrentClipWriter> logger)
    {
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _databases = databases ?? throw new ArgumentNullException(nameof(databases));
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _soundService = soundService ?? throw new ArgumentNullException(nameof(soundService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public CurrentClip? CurrentClip => Volatile.Read(ref _currentClip);

    private PreferencesConfiguration Preferences => _configurationService.Configuration.Preferences;

    /// <inheritdoc />
    public async Task<Clip?> MakeCurrentAsync(string databaseKey, Guid clipId, WriteOptions options = default, CancellationToken cancellationToken = default)
    {
        if (await LoadAsync(databaseKey, clipId, cancellationToken) is not { } clip)
            return null;

        clip = _templateService.TryApplyTemplate(clip) ?? clip;

        // Recompute after loading the content, by the same rule as capture
        clip.ContentHash = ContentHasher.HashClip(clip);

        await WriteAsync(clip, databaseKey, ShouldFilter(clip, options), cancellationToken);
        _logger.LogInformation("Put clip {ClipId} from database {DatabaseKey} on the clipboard", clip.Id, databaseKey);

        return clip;
    }

    /// <inheritdoc />
    public Task PutOnClipboardAsync(Clip content, WriteOptions options = default, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        return WriteAsync(content, null, ShouldFilter(content, options), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyList<ClipboardFormatData> Prepare(Clip clip, WriteOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(clip);

        var formats = _clipboardService.GetClipboardFormats(clip, ShouldFilter(clip, options));
        SetCurrentClip(new CurrentClip(clip, null), false);

        return formats;
    }

    /// <inheritdoc />
    public async Task<bool> RewriteCurrentAsync(WriteOptions options = default, CancellationToken cancellationToken = default)
    {
        switch (CurrentClip)
        {
            case null:
                return false;
            case { DatabaseKey: { } databaseKey } currentClip:
                await MakeCurrentAsync(databaseKey, currentClip.Clip.Id, options, cancellationToken);
                return true;
            case var currentClip:
                await PutOnClipboardAsync(currentClip.Clip, options, cancellationToken);
                return true;
        }
    }

    /// <inheritdoc />
    public void ReportCurrent(Clip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);

        SetCurrentClip(new CurrentClip(clip, null), false);
    }

    /// <inheritdoc />
    public async Task ReportCapturedAsync(Clip clip, string databaseKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clip);

        SetCurrentClip(new CurrentClip(clip, databaseKey), true);

        // Active filtering, as at the end of ClipMate 7's ClipViewCBChange: a Duplicate is filtered too
        if (Preferences is { OutboundFilter: true, OutboundFilterMode: OutboundFilterMode.Active })
            await PutBackFilteredAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> FilterCapturedClipAsync(CancellationToken cancellationToken = default) =>
        Preferences is { OutboundFilter: true, OutboundFilterMode: OutboundFilterMode.Passive } &&
        _currentIsUnfilteredCapture &&
        await PutBackFilteredAsync(cancellationToken);

    /// <summary>
    /// Puts the captured Current Clip back on the clipboard filtered, with the Filter sound, when the filter drops one of
    /// its Formats. The active template is applied, as ClipMate 7's ClipObj.Copy applies it to every write.
    /// </summary>
    private async Task<bool> PutBackFilteredAsync(CancellationToken cancellationToken)
    {
        _currentIsUnfilteredCapture = false;
        if (CurrentClip is not { DatabaseKey: { } databaseKey } currentClip ||
            await LoadAsync(databaseKey, currentClip.Clip.Id, cancellationToken) is not { } clip ||
            !ShouldFilter(clip, new WriteOptions(FilterMode.Forced)) ||
            !HasFormatsTheFilterDrops(clip))
            return false;

        clip = _templateService.TryApplyTemplate(clip) ?? clip;
        clip.ContentHash = ContentHasher.HashClip(clip);
        await WriteAsync(clip, databaseKey, true, cancellationToken);
        await _soundService.PlaySoundAsync(SoundEvent.Filter, cancellationToken);
        _logger.LogInformation("Outbound Filter: put clip {ClipId} back on the clipboard as plain text or an image", clip.Id);

        return true;
    }

    private async Task<Clip?> LoadAsync(string databaseKey, Guid clipId, CancellationToken cancellationToken)
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

        return clip;
    }

    private async Task WriteAsync(Clip clip, string? databaseKey, bool filtered, CancellationToken cancellationToken)
    {
        await _clipboardService.SetClipboardContentAsync(clip, filtered, cancellationToken);
        SetCurrentClip(new CurrentClip(clip, databaseKey), false);

        // The status bar confirms that the selected clip was copied
        await UiThread.RunAsync(() => _messenger.Send(new ClipboardCopiedEvent(clip)));
    }

    /// <summary>
    /// Whether a write keeps only the clip's plain text, or its image: the Outbound Filter is on (or Manual Filter forces
    /// it), the clip has text or an image to keep, and its Source Application isn't excluded.
    /// </summary>
    private bool ShouldFilter(Clip clip, WriteOptions options) =>
        (options.Filter == FilterMode.Forced || Preferences.OutboundFilter) &&
        (!string.IsNullOrEmpty(clip.TextContent) || clip.ImageData is { Length: > 0 }) &&
        !IsExcluded(clip.SourceApplicationName);

    private bool IsExcluded(string? sourceApplication)
    {
        var name = ProgramName(sourceApplication);
        return name.Length > 0 &&
               Preferences.OutboundFilterExclusions.Any(p => string.Equals(ProgramName(p), name, StringComparison.OrdinalIgnoreCase));
    }

    private static string ProgramName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^4]
            : trimmed;
    }

    // What the filter leaves out: rich text, HTML, a file list, other programs' Formats, or an image beside the text
    private static bool HasFormatsTheFilterDrops(Clip clip) =>
        !string.IsNullOrEmpty(clip.RtfContent) ||
        !string.IsNullOrEmpty(clip.HtmlContent) ||
        !string.IsNullOrEmpty(clip.FilePathsJson) ||
        clip.RawFormats.Count > 0 ||
        (!string.IsNullOrEmpty(clip.TextContent) && clip.ImageData is { Length: > 0 });

    private void SetCurrentClip(CurrentClip currentClip, bool unfilteredCapture)
    {
        Volatile.Write(ref _currentClip, currentClip);
        _currentIsUnfilteredCapture = unfilteredCapture;
        _messenger.Send(new CurrentClipChangedEvent(currentClip));
    }
}
