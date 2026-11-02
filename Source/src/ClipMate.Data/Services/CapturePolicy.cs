using ClipMate.Core.Models.Capture;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Platform.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Every capture decision, in ClipMate 7's order (<c>ClipViewCBChange</c> and <c>CaptureTheClip</c>; ADR-0005):
/// Auto-Append first, then the Application Profile's result, the target collection and the small-graphic rule. The
/// clip store applies Duplicate to the verdict. It reads state and returns a verdict; it plays, saves and logs nothing
/// the user sees.
/// </summary>
public sealed class CapturePolicy : ICapturePolicy
{
    // ClipMate 7 rejects a bitmap from no owner whose largest Format is at most 4 KB: a toolbar button being copied
    private const int _smallGraphicMaxSize = 0x1000;

    private readonly IAppendMode _appendMode;
    private readonly IDatabaseContextFactory _databaseContextFactory;
    private readonly ILogger<CapturePolicy> _logger;
    private readonly ICaptureTargetResolver _targetResolver;

    public CapturePolicy(IAppendMode appendMode,
        ICaptureTargetResolver targetResolver,
        IDatabaseContextFactory databaseContextFactory,
        ILogger<CapturePolicy> logger)
    {
        _appendMode = appendMode ?? throw new ArgumentNullException(nameof(appendMode));
        _targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
        _databaseContextFactory = databaseContextFactory ?? throw new ArgumentNullException(nameof(databaseContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<CaptureVerdict> DecideAsync(CaptureAttempt attempt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        // Auto-Append comes before the Application Profile: the clipboard's text goes onto the growing clip. A copy with no
        // text, or a growing clip that is gone, ends Append mode and the copy is captured normally
        var endsAppendMode = false;
        if (_appendMode is { IsActive: true, GrowingClipId: { } growingClipId, DatabaseKey: { } appendDatabaseKey })
        {
            if (!string.IsNullOrEmpty(attempt.PlainText) && await GrowingClipExistsAsync(appendDatabaseKey, growingClipId, cancellationToken))
                return new AppendVerdict(appendDatabaseKey, growingClipId, attempt.PlainText);

            endsAppendMode = true;
        }

        if (attempt.Clip is not { } clip)
            return new RejectedVerdict(CaptureRejection.NoAcceptableFormats);

        CaptureTarget? target;
        try
        {
            target = await _targetResolver.ResolveAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A clip saved with no collection would be lost from the tree, so nothing is kept
            _logger.LogError(ex, "Could not choose the collection for a capture");

            return new RejectedVerdict(CaptureRejection.Error, ex.Message);
        }

        if (target == null)
            return new RejectedVerdict(CaptureRejection.Error, "No Database is active.");

        if (attempt.Kind != CaptureKind.CaptureSpecial &&
            attempt.SourceApplication == ClipboardService.NoOwnerApplication &&
            clip.ImageData is { Length: > 0 } &&
            attempt.LargestFormatSize <= _smallGraphicMaxSize)
            return new RejectedVerdict(CaptureRejection.SmallGraphic);

        // The Capture hotkey and Capture Special always keep the clip
        var rejectDuplicate = attempt.Kind is CaptureKind.Monitored or CaptureKind.Startup;

        return new SaveVerdict(clip, target, rejectDuplicate, endsAppendMode);
    }

    private async Task<bool> GrowingClipExistsAsync(string databaseKey, Guid growingClipId, CancellationToken cancellationToken) =>
        await _databaseContextFactory.GetClipRepository(databaseKey).GetByIdAsync(growingClipId, cancellationToken) != null;
}
