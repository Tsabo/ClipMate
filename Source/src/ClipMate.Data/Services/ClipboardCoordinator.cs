using ClipMate.Core.Events;
using ClipMate.Core.Helpers;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Capture;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Platform;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Background service that coordinates clipboard monitoring and clip persistence.
/// Consumes clips from the clipboard service channel and saves them to the database.
/// </summary>
public class ClipboardCoordinator : IHostedService,
    IRecipient<PreferencesChangedEvent>,
    IRecipient<ToggleAutoCaptureEvent>,
    IRecipient<ManualCaptureClipboardEvent>,
    IRecipient<CaptureSpecialRequestedEvent>,
    IRecipient<ManualFilterRequestedEvent>
{
    private readonly IAppendMode _appendMode;
    private readonly IClipboardService _clipboardService;
    private readonly IClipStore _clipStore;
    private readonly IConfigurationService _configurationService;
    private readonly ICurrentClipWriter _currentClipWriter;
    private readonly ILogger<ClipboardCoordinator> _logger;
    private readonly IMessenger _messenger;
    private readonly IServiceProvider _serviceProvider;
    private int _capturedCount;
    private CancellationTokenSource? _cts;
    private bool _isMonitoring;
    private Task? _processingTask;

    public ClipboardCoordinator(IClipboardService clipboardService,
        IConfigurationService configurationService,
        ICurrentClipWriter currentClipWriter,
        IClipStore clipStore,
        IAppendMode appendMode,
        IServiceProvider serviceProvider,
        IMessenger messenger,
        ILogger<ClipboardCoordinator> logger)
    {
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _currentClipWriter = currentClipWriter ?? throw new ArgumentNullException(nameof(currentClipWriter));
        _clipStore = clipStore ?? throw new ArgumentNullException(nameof(clipStore));
        _appendMode = appendMode ?? throw new ArgumentNullException(nameof(appendMode));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Register for preferences changed events and hotkey events
        _messenger.Register<PreferencesChangedEvent>(this);
        _messenger.Register<ToggleAutoCaptureEvent>(this);
        _messenger.Register<ManualCaptureClipboardEvent>(this);
        _messenger.Register<CaptureSpecialRequestedEvent>(this);
        _messenger.Register<ManualFilterRequestedEvent>(this);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting clipboard coordinator");

        try
        {
            // Check if auto-capture is enabled in preferences
            var preferences = _configurationService.Configuration.Preferences;
            if (!preferences.EnableAutoCaptureAtStartup)
            {
                _logger.LogInformation("Auto-capture at startup is disabled in preferences, skipping clipboard monitoring");
                _isMonitoring = false;

                return;
            }

            // Apply startup delay if configured
            if (preferences.StartupDelaySeconds > 0)
            {
                _logger.LogInformation("Delaying clipboard monitoring start by {Seconds} seconds", preferences.StartupDelaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(preferences.StartupDelaySeconds), cancellationToken);
            }

            // Capture existing clipboard content if enabled, unless a test copy was started with --no-startup-capture
            if (preferences.CaptureExistingClipboardAtStartup && !StartupCaptureSwitch.IsSkipped)
            {
                _logger.LogDebug("Capturing existing clipboard content at startup");
                try
                {
                    var existing = await _clipboardService.GetCurrentClipboardContentAsync(ManualCaptureKind.Startup, cancellationToken);
                    if (existing != null)
                    {
                        _logger.LogInformation("Captured existing clipboard content: Type={ClipType}", existing.Clip?.Type);

                        // Start background processing task before capturing
                        _cts = new CancellationTokenSource();
                        _processingTask = ProcessClipsAsync(_cts.Token);

                        await ProcessAttemptAsync(existing, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to capture existing clipboard content");
                }
            }
            else if (preferences.CaptureExistingClipboardAtStartup)
                _logger.LogInformation("Not capturing the clipboard at startup: started with --no-startup-capture");

            // Start clipboard monitoring (on its own clipboard thread)
            await _clipboardService.StartMonitoringAsync(cancellationToken);

            // Start background task to process clips from channel (if not already started)
            if (_processingTask == null)
            {
                _cts = new CancellationTokenSource();
                _processingTask = ProcessClipsAsync(_cts.Token);
            }

            _isMonitoring = true;
            _messenger.Send(new AutoCaptureStateChangedEvent(true));
            _logger.LogInformation("Clipboard coordinator started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start clipboard coordinator");

            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping clipboard coordinator");

        try
        {
            await _clipboardService.StopMonitoringAsync();

            // Cancel processing and wait for it to finish
            _cts?.Cancel();

            if (_processingTask != null)
                await _processingTask;

            _cts?.Dispose();

            // Unregister from messenger to prevent memory leaks
            _messenger.UnregisterAll(this);

            _logger.LogInformation("Clipboard coordinator stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping clipboard coordinator");
        }
    }

    /// <summary>
    /// Handles ManualCaptureClipboardEvent by manually capturing current clipboard content.
    /// </summary>
    public async void Receive(ManualCaptureClipboardEvent message)
    {
        try
        {
            _logger.LogInformation("Manual clipboard capture requested");

            var attempt = await _clipboardService.GetCurrentClipboardContentAsync(ManualCaptureKind.CaptureHotkey);
            if (attempt != null)
            {
                _logger.LogDebug("Captured clipboard content: Type={ClipType}", attempt.Clip?.Type);
                await ProcessAttemptAsync(attempt, CancellationToken.None);
            }
            else
                _logger.LogDebug("No clipboard content to capture");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to manually capture clipboard content");
        }
    }

    /// <summary>
    /// Handles CaptureSpecialRequestedEvent: saves a clip of the chosen Formats, regardless of the Application Profile.
    /// </summary>
    public async void Receive(CaptureSpecialRequestedEvent message)
    {
        try
        {
            var clip = await _clipboardService.CaptureSpecialAsync(message.Reading, message.FormatNames);
            if (clip != null)
                await ProcessAttemptAsync(new CaptureAttempt(clip, clip.TextContent, clip.SourceApplicationName, CaptureKind.CaptureSpecial, 0),
                    CancellationToken.None);
            else
                _logger.LogDebug("Capture Special: nothing to keep in the chosen formats");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Capture Special failed");
        }
    }

    /// <summary>
    /// Handles ManualFilterRequestedEvent: puts the Current Clip back on the clipboard with only its plain text (or its
    /// image), as ClipMate 7's Manual Filter does (manual: Hotkeys). With no Current Clip it does nothing.
    /// </summary>
    public async void Receive(ManualFilterRequestedEvent message)
    {
        try
        {
            if (!await _currentClipWriter.RewriteCurrentAsync(new WriteOptions(FilterMode.Forced)))
            {
                _logger.LogDebug("Manual Filter: there is no Current Clip");
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ISoundService>().PlaySoundAsync(SoundEvent.Filter);
            _logger.LogInformation("Manual Filter: left only plain text or an image on the clipboard");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual Filter failed");
        }
    }

    /// <summary>
    /// Handles preferences changed events to dynamically start/stop monitoring.
    /// </summary>
    public async void Receive(PreferencesChangedEvent message)
    {
        var preferences = _configurationService.Configuration.Preferences;
        var shouldBeMonitoring = preferences.EnableAutoCaptureAtStartup;

        if (shouldBeMonitoring && !_isMonitoring)
        {
            _logger.LogInformation("Auto-capture enabled via preferences, starting monitoring");
            try
            {
                // Start monitoring
                await _clipboardService.StartMonitoringAsync(CancellationToken.None);

                // Start processing task if not already running
                if (_processingTask == null || _processingTask.IsCompleted)
                {
                    _cts = new CancellationTokenSource();
                    _processingTask = ProcessClipsAsync(_cts.Token);
                }

                _isMonitoring = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start monitoring via preferences change");
            }
        }
        else if (!shouldBeMonitoring && _isMonitoring)
        {
            _logger.LogInformation("Auto-capture disabled via preferences, stopping monitoring");
            try
            {
                await _clipboardService.StopMonitoringAsync();
                _isMonitoring = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop monitoring via preferences change");
            }
        }
    }

    /// <summary>
    /// Handles ToggleAutoCaptureEvent by toggling clipboard monitoring on/off.
    /// </summary>
    public async void Receive(ToggleAutoCaptureEvent message)
    {
        try
        {
            if (_isMonitoring)
            {
                // Stop monitoring
                _logger.LogInformation("Stopping clipboard monitoring (toggle off)");
                await _clipboardService.StopMonitoringAsync();
                _isMonitoring = false;
                _messenger.Send(new StateRefreshRequestedEvent());

                // Cancel background processing
                _cts?.Cancel();
                if (_processingTask != null)
                    await _processingTask;
            }
            else
            {
                // Start monitoring
                _logger.LogInformation("Starting clipboard monitoring (toggle on)");

                // Create new cancellation token source
                _cts = new CancellationTokenSource();
                _processingTask = ProcessClipsAsync(_cts.Token);

                await _clipboardService.StartMonitoringAsync(_cts.Token);
                _isMonitoring = true;
                _messenger.Send(new StateRefreshRequestedEvent());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle auto-capture");
        }
    }

    /// <summary>
    /// Background task that consumes clips from the channel and persists them.
    /// Runs until the channel is completed or cancellation is requested.
    /// </summary>
    private async Task ProcessClipsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Clip processing loop started");

        try
        {
            // Read all clips from the channel until it's completed
            await foreach (var item in _clipboardService.ClipsChannel.ReadAllAsync(cancellationToken))
            {
                try
                {
                    await ProcessAttemptAsync(item, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Log error but continue processing other clips
                    _logger.LogError(ex, "Failed to process a capture: Type={ClipType}", item.Clip?.Type);
                }
            }

            _logger.LogInformation("Clip processing loop completed (channel closed)");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Clip processing loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in clip processing loop");
        }
    }

    /// <summary>
    /// Acts on the Capture Policy's verdict for <paramref name="attempt" />: appends, saves through the clip store or drops
    /// it, with ClipMate 7's sound and Event Log line for the outcome (ADR-0005).
    /// </summary>
    internal async Task ProcessAttemptAsync(CaptureAttempt attempt, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var soundService = scope.ServiceProvider.GetRequiredService<ISoundService>();
        var verdict = await scope.ServiceProvider.GetRequiredService<ICapturePolicy>().DecideAsync(attempt, cancellationToken);

        switch (verdict)
        {
            case AppendVerdict append:
                await AppendAsync(scope.ServiceProvider.GetRequiredService<IClipAppendService>(), append, cancellationToken);
                break;

            case RejectedVerdict rejected:
                LogRejection(rejected);
                await soundService.PlaySoundAsync(SoundEvent.Erase, cancellationToken);
                break;

            case SaveVerdict save:
                await SaveAsync(soundService, save, cancellationToken);
                break;
        }
    }

    private async Task AppendAsync(IClipAppendService clipAppendService, AppendVerdict append, CancellationToken cancellationToken)
    {
        var preferences = _configurationService.Configuration.Preferences;
        var updatedClip = await clipAppendService.AppendCapturedTextAsync(append.GrowingClipId, append.Text, preferences.AppendSeparatorString,
            preferences.StripTrailingLineBreak, cancellationToken);

        _logger.LogInformation("Auto-Append: added the captured text to the growing clip {ClipId}", updatedClip.Id);
        _messenger.Send(new ClipContentUpdatedEvent(updatedClip.Id, append.DatabaseKey, updatedClip.TextContent ?? string.Empty,
            updatedClip.Title ?? string.Empty, updatedClip.Size));
    }

    private async Task SaveAsync(ISoundService soundService, SaveVerdict save, CancellationToken cancellationToken)
    {
        // A copy with no text ends Auto-Append, as in ClipMate 7, and is then captured normally
        if (save.EndsAppendMode)
        {
            _appendMode.Stop();
            _logger.LogInformation("Auto-Append ended by a copy with no text");
        }

        var target = save.Target;
        var added = await _clipStore.AddAsync(target.DatabaseKey, save.Clip, target.Collection.Id, target.FolderId, save.RejectDuplicate,
            cancellationToken);

        if (added is DuplicateFound { Existing: var existingClip })
        {
            _logger.LogInformation("Duplicate Item Ignored.");
            await soundService.PlaySoundAsync(SoundEvent.Ignore, cancellationToken);

            // The clip already there stays the Current Clip, as in ClipMate 7, so the Outbound Filter still cleanses the copy
            await _currentClipWriter.ReportCapturedAsync(existingClip, target.DatabaseKey, cancellationToken);

            return;
        }

        var savedClip = ((ClipAdded)added).Clip;
        _logger.LogInformation("Captured Item # {Number} And Placed In: {Collection}", Interlocked.Increment(ref _capturedCount),
            target.Collection.Title);
        await soundService.PlaySoundAsync(SoundEvent.ClipboardUpdate, cancellationToken);

        _messenger.Send(new ClipAddedEvent(savedClip, false, target.DatabaseKey, savedClip.CollectionId, savedClip.FolderId) { Captured = true });

        // After the capture sound, so the Outbound Filter's sound follows it, as in ClipMate 7
        await _currentClipWriter.ReportCapturedAsync(savedClip, target.DatabaseKey, cancellationToken);
    }

    // ClipMate 7's Event Log text for each rejection
    private void LogRejection(RejectedVerdict rejected)
    {
        switch (rejected.Reason)
        {
            case CaptureRejection.NoAcceptableFormats:
                _logger.LogInformation("New Clipboard Data Ignored - No Acceptable Formats, According To Application Profile.");
                break;
            case CaptureRejection.SmallGraphic:
                _logger.LogInformation("Small Graphic Ignored - Probably a button being copied to a toolbar.");
                break;
            default:
                _logger.LogError("Capture Failed - {Detail}", rejected.Detail);
                break;
        }
    }
}
