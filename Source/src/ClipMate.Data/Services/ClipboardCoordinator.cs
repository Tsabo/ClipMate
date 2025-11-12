using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Coordinates clipboard monitoring and clip persistence.
/// Wires ClipboardService events to ClipService for automatic saving.
/// </summary>
public class ClipboardCoordinator : IDisposable
{
    private readonly IClipboardService _clipboardService;
    private readonly IClipService _clipService;
    private readonly ILogger<ClipboardCoordinator> _logger;
    private bool _disposed;

    public ClipboardCoordinator(
        IClipboardService clipboardService,
        IClipService clipService,
        ILogger<ClipboardCoordinator> logger)
    {
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Subscribe to clipboard capture events
        _clipboardService.ClipCaptured += OnClipCaptured;
    }

    /// <summary>
    /// Starts clipboard monitoring and automatic clip saving.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting clipboard coordinator");
        await _clipboardService.StartMonitoringAsync(cancellationToken);
    }

    /// <summary>
    /// Stops clipboard monitoring.
    /// </summary>
    public async Task StopAsync()
    {
        _logger.LogInformation("Stopping clipboard coordinator");
        await _clipboardService.StopMonitoringAsync();
    }

    private async void OnClipCaptured(object? sender, ClipCapturedEventArgs e)
    {
        if (e.Cancel)
        {
            _logger.LogDebug("Clip capture cancelled by event handler");
            return;
        }

        try
        {
            _logger.LogDebug(
                "Saving clip: Type={ClipType}, Hash={ContentHash}, Length={Length}",
                e.Clip.Type,
                e.Clip.ContentHash,
                e.Clip.TextContent?.Length ?? 0);

            // ClipService handles duplicate detection via content hash
            var savedClip = await _clipService.CreateAsync(e.Clip);

            if (savedClip.Id == e.Clip.Id)
            {
                _logger.LogInformation("Clip saved successfully: {ClipId}", savedClip.Id);
            }
            else
            {
                _logger.LogDebug(
                    "Duplicate clip detected, using existing: {ExistingId}",
                    savedClip.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save clip: Type={ClipType}, Hash={ContentHash}",
                e.Clip.Type,
                e.Clip.ContentHash);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _clipboardService.ClipCaptured -= OnClipCaptured;
        _disposed = true;
    }
}
