using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.Services.Initialization;

/// <summary>
/// Initialization step that starts following the foreground window, so the Paste Target is the window the user was
/// working in before ClipMate whenever a QuickPaste happens.
/// </summary>
public class PasteTargetTrackingStep : IStartupInitializationStep
{
    private readonly ILogger<PasteTargetTrackingStep> _logger;
    private readonly IPasteTargetTracker _pasteTargetTracker;

    public PasteTargetTrackingStep(IPasteTargetTracker pasteTargetTracker,
        ILogger<PasteTargetTrackingStep> logger)
    {
        _pasteTargetTracker = pasteTargetTracker ?? throw new ArgumentNullException(nameof(pasteTargetTracker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string Name => "Paste Target Tracking";

    /// <inheritdoc />
    public int Order => 70; // After configuration loading (20), which supplies the good/bad lists

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _pasteTargetTracker.Start();
        }
        catch (Exception ex)
        {
            // QuickPaste without a tracked target is degraded, not fatal
            _logger.LogError(ex, "Failed to start Paste Target tracking");
        }

        return Task.CompletedTask;
    }
}
