using ClipMate.App.Helpers;
using ClipMate.Core.Events;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.Services;

/// <summary>
/// Coordinates hotkey registration and maps hotkeys to their actions.
/// </summary>
public class HotkeyCoordinator
{
    // Hotkey ID constants (1-11 for the 11 configured hotkeys)
    private const int _hotkeyIdShowWindow = 1;
    private const int _hotkeyIdScrollNext = 2;
    private const int _hotkeyIdScrollPrevious = 3;
    private const int _hotkeyIdActivateQuickPaste = 4;
    private const int _hotkeyIdRegionScreenCapture = 5;
    private const int _hotkeyIdObjectScreenCapture = 6;
    private const int _hotkeyIdViewClip = 7;
    private const int _hotkeyIdPopupClipBar = 8;
    private const int _hotkeyIdToggleAutoCapture = 9;
    private const int _hotkeyIdManualCapture = 10;
    private const int _hotkeyIdManualFilter = 11;
    private readonly IConfigurationService _configurationService;
    private readonly IHotkeyService _hotkeyService;
    private readonly ILogger<HotkeyCoordinator> _logger;
    private readonly IMessenger _messenger;

    public HotkeyCoordinator(IHotkeyService hotkeyService,
        IConfigurationService configurationService,
        IMessenger messenger,
        ILogger<HotkeyCoordinator> logger)
    {
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Registers all configured hotkeys with the system.
    /// </summary>
    public void RegisterAllHotkeys()
    {
        _logger.LogInformation("Registering all configured hotkeys");

        var config = _configurationService.Configuration.Hotkeys;

        RegisterHotkey(_hotkeyIdShowWindow, config.Activate, OnShowWindowRequested);
        RegisterHotkey(_hotkeyIdScrollNext, config.SelectNext, OnScrollNextRequested);
        RegisterHotkey(_hotkeyIdScrollPrevious, config.SelectPrevious, OnScrollPreviousRequested);
        RegisterHotkey(_hotkeyIdActivateQuickPaste, config.QuickPaste, OnActivateQuickPasteRequested);
        RegisterHotkey(_hotkeyIdRegionScreenCapture, config.ScreenCapture, OnRegionScreenCaptureRequested);
        RegisterHotkey(_hotkeyIdObjectScreenCapture, config.ScreenCaptureObject, OnObjectScreenCaptureRequested);
        RegisterHotkey(_hotkeyIdViewClip, config.ViewClip, OnViewClipRequested);
        RegisterHotkey(_hotkeyIdPopupClipBar, config.PopupClipBar, OnPopupClipBarRequested);
        RegisterHotkey(_hotkeyIdToggleAutoCapture, config.AutoCapture, OnToggleAutoCaptureRequested);
        RegisterHotkey(_hotkeyIdManualCapture, config.Capture, OnManualCaptureRequested);
        RegisterHotkey(_hotkeyIdManualFilter, config.ManualFilter, OnManualFilterRequested);

        _logger.LogInformation("Hotkey registration completed");
    }

    /// <summary>
    /// Reloads all hotkeys by unregistering and re-registering them.
    /// Call this after configuration changes.
    /// </summary>
    public void ReloadHotkeys()
    {
        _logger.LogInformation("Reloading hotkeys after configuration change");

        _hotkeyService.UnregisterAllHotkeys();
        RegisterAllHotkeys();
    }

    private void RegisterHotkey(int id, string hotkeyString, Action action)
    {
        if (string.IsNullOrWhiteSpace(hotkeyString))
        {
            _logger.LogDebug("Skipping hotkey registration for ID {HotkeyId} - no configuration", id);
            return;
        }

        if (!HotkeyParser.TryParse(hotkeyString, out var modifiers, out var virtualKey, out var error))
        {
            _logger.LogWarning("Failed to parse hotkey '{HotkeyString}' for ID {HotkeyId}: {Error}",
                hotkeyString, id, error);

            return;
        }

        var success = _hotkeyService.RegisterHotkey(id, modifiers, virtualKey, action);
        if (success)
            _logger.LogDebug("Registered hotkey '{HotkeyString}' with ID {HotkeyId}", hotkeyString, id);
        else
        {
            _logger.LogWarning("Failed to register hotkey '{HotkeyString}' with ID {HotkeyId} - may be in use by another application",
                hotkeyString, id);
        }
    }

    // Hotkey action handlers
    private void OnShowWindowRequested()
    {
        _logger.LogDebug("Show Window hotkey pressed");
        _messenger.Send(new ShowExplorerWindowEvent());
    }

    private void OnScrollNextRequested()
    {
        _logger.LogDebug("Scroll Next hotkey pressed");
        _messenger.Send(new SelectNextClipEvent());
    }

    private void OnScrollPreviousRequested()
    {
        _logger.LogDebug("Scroll Previous hotkey pressed");
        _messenger.Send(new SelectPreviousClipEvent());
    }

    private void OnActivateQuickPasteRequested()
    {
        _logger.LogDebug("Activate QuickPaste hotkey pressed");
        _messenger.Send(new QuickPasteNowEvent());
    }

    private void OnRegionScreenCaptureRequested()
    {
        _logger.LogDebug("Region Screen Capture hotkey pressed");
        // TODO: Implement screen capture functionality
        // Create StartRegionScreenCaptureEvent and implement handler
        _logger.LogWarning("Region screen capture not yet implemented");
    }

    private void OnObjectScreenCaptureRequested()
    {
        _logger.LogDebug("Object Screen Capture hotkey pressed");
        // TODO: Implement screen capture functionality
        // Create StartObjectScreenCaptureEvent and implement handler
        _logger.LogWarning("Object screen capture not yet implemented");
    }

    private void OnViewClipRequested()
    {
        _logger.LogDebug("View Clip hotkey pressed");
        // TODO: Implement floating clip viewer
        // Create ViewClipInFloatingWindowEvent and implement handler
        _logger.LogWarning("View clip in floating window not yet implemented");
    }

    private void OnPopupClipBarRequested()
    {
        _logger.LogDebug("Show Classic Window hotkey pressed");
        _messenger.Send(new ShowClipBarRequestedEvent(true));
    }

    private void OnToggleAutoCaptureRequested()
    {
        _logger.LogDebug("Toggle Auto-Capture hotkey pressed");
        _messenger.Send(new ToggleAutoCaptureEvent());
    }

    private void OnManualCaptureRequested()
    {
        _logger.LogDebug("Manual Capture hotkey pressed");
        _messenger.Send(new ManualCaptureClipboardEvent());
    }

    private void OnManualFilterRequested()
    {
        _logger.LogDebug("Manual Filter hotkey pressed");
        // TODO: Implement manual filter functionality
        // Create ManualFilterEvent and implement handler
        _logger.LogWarning("Manual filter not yet implemented");
    }
}
