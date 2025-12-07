using System.Windows.Input;
using ClipMate.App.Views;
using ClipMate.Core.Events;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Platform.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;
using Cursor = System.Windows.Forms.Cursor;
using ModifierKeys = ClipMate.Core.Models.ModifierKeys;

namespace ClipMate.App;

/// <summary>
/// Coordinates ClipBar (quick paste picker) functionality including hotkey registration and window lifecycle.
/// ClipBar is a quick access popup window (Ctrl+Shift+V) for selecting and pasting individual clips.
/// This is distinct from the PowerPaste sequential automation feature.
/// </summary>
public class ClassicWindowCoordinator : IHostedService, IRecipient<ShowClipBarRequestedEvent>, IDisposable
{
    private const int _clipBarHotkeyId = 1001;
    private readonly IConfigurationService _configurationService;
    private readonly IHotkeyService _hotkeyService;
    private readonly ILogger<ClassicWindowCoordinator> _logger;
    private readonly IMessenger _messenger;
    private readonly IServiceProvider _serviceProvider;
    private ClassicWindow? _classicWindow;
    private bool _disposed;
    private HotkeyWindow? _hotkeyWindow;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassicWindowCoordinator" /> class.
    /// </summary>
    public ClassicWindowCoordinator(IServiceProvider serviceProvider,
        IHotkeyService hotkeyService,
        IConfigurationService configurationService,
        IMessenger messenger,
        ILogger<ClassicWindowCoordinator> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Register for show ClipBar requests
        _messenger.Register(this);
    }

    /// <summary>
    /// Disposes the coordinator and cleans up resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _classicWindow?.Close();
                _classicWindow = null;

                _hotkeyWindow?.Close();
                _hotkeyWindow = null;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing Classic window coordinator");
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Initializes ClipBar with hotkey registration when the host starts.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting ClipBar coordinator");

            // Create and show the hidden hotkey window on the UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                _hotkeyWindow = new HotkeyWindow();
                _hotkeyWindow.Show(); // CRITICAL: Must be shown for message pump to work

                // Initialize HotkeyService with the hotkey window (must be on UI thread)
                if (_hotkeyService is HotkeyService platformService)
                    platformService.Initialize(_hotkeyWindow);
            });

            // Register Ctrl+Shift+V hotkey for ClipBar
            var virtualKey = KeyInterop.VirtualKeyFromKey(Key.V);
            var registered = _hotkeyService.RegisterHotkey(
                _clipBarHotkeyId,
                ModifierKeys.Control | ModifierKeys.Shift,
                virtualKey,
                OnClipBarHotkeyPressed);

            if (registered)
                _logger.LogInformation("ClipBar hotkey registered successfully (Ctrl+Shift+V)");
            else
                _logger.LogWarning("Failed to register ClipBar hotkey (Ctrl+Shift+V). The hotkey may already be in use.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting ClipBar coordinator");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops ClipBar and unregisters hotkeys when the host stops.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping ClipBar coordinator");

        try
        {
            // Unregister hotkey
            _hotkeyService.UnregisterHotkey(_clipBarHotkeyId);
            _logger.LogInformation("ClipBar hotkey unregistered");

            // Close windows on the UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                _classicWindow?.Close();
                _classicWindow = null;

                _hotkeyWindow?.Close();
                _hotkeyWindow = null;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping ClipBar coordinator");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles ShowClipBarRequestedEvent by showing the ClipBar popup.
    /// </summary>
    public void Receive(ShowClipBarRequestedEvent message)
    {
        _logger.LogDebug("ShowClipBarRequestedEvent received");
        ShowClipBar();
    }

    /// <summary>
    /// Called when the ClipBar hotkey (Ctrl+Shift+V) is pressed.
    /// </summary>
    private void OnClipBarHotkeyPressed()
    {
        _logger.LogInformation("ClipBar hotkey pressed (Ctrl+Shift+V)");
        ShowClipBar();
    }

    /// <summary>
    /// Shows the ClipBar window with appropriate positioning.
    /// </summary>
    private void ShowClipBar()
    {
        try
        {
            // Ensure we're on the UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                // If window is already open, just activate it
                if (_classicWindow != null && _classicWindow.IsVisible)
                {
                    _classicWindow.Activate();
                    _classicWindow.Focus();
                    _logger.LogDebug("Classic window already open, activating");
                    return;
                }

                // Create new window instance from DI
                _classicWindow = _serviceProvider.GetRequiredService<ClassicWindow>();

                // Calculate and set position based on configuration
                var position = CalculateClipBarPosition();
                _classicWindow.Left = position.X;
                _classicWindow.Top = position.Y;

                // Close window when user is done
                _classicWindow.Closed += (s, e) =>
                {
                    _logger.LogDebug("Classic window closed");
                    _classicWindow = null;
                };

                // Show the window
                _classicWindow.Show();
                _classicWindow.Activate();
                _logger.LogDebug("Classic window shown at position ({X}, {Y})", position.X, position.Y);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing ClipBar window");
        }
    }

    /// <summary>
    /// Calculates the position for the ClipBar window based on configuration.
    /// </summary>
    private (double X, double Y) CalculateClipBarPosition()
    {
        var location = _configurationService.Configuration.Preferences.ClipBarPopupLocation;

        try
        {
            switch (location)
            {
                case ClipBarPopupLocation.RememberLastLocation:
                    // Position already set by ClassicWindow constructor from saved config
                    // Return center screen as fallback if no saved position
                    var lastPos = _configurationService.Configuration.Preferences.ClipBarLastPosition;
                    if (!string.IsNullOrWhiteSpace(lastPos))
                    {
                        var parts = lastPos.Split(',');
                        if (parts.Length == 2
                            && double.TryParse(parts[0], out var x)
                            && double.TryParse(parts[1], out var y))
                            return (x, y);
                    }

                    // Fallback to mouse cursor if no saved position
                    return GetMouseCursorPosition();

                case ClipBarPopupLocation.AboveTaskbar:
                    return GetPositionAboveTaskbar();

                case ClipBarPopupLocation.AtMouseCursor:
                default:
                    return GetMouseCursorPosition();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error calculating ClipBar position, falling back to mouse cursor");
            return GetMouseCursorPosition();
        }
    }

    private (double X, double Y) GetMouseCursorPosition()
    {
        var cursorPos = Cursor.Position;
        return (cursorPos.X, cursorPos.Y);
    }

    private (double X, double Y) GetPositionAboveTaskbar()
    {
        try
        {
            // Get the screen containing the mouse cursor
            var cursorPos = Cursor.Position;
            var screen = Screen.FromPoint(cursorPos);

            // Calculate position above taskbar using working area
            // WorkingArea excludes the taskbar
            var workingArea = screen.WorkingArea;

            // Position at bottom of working area (above taskbar)
            // Assuming ClipBar window height is 400 (from XAML)
            const double windowHeight = 400;
            const double windowWidth = 600;

            var x = workingArea.Left + (workingArea.Width - windowWidth) / 2;
            var y = workingArea.Bottom - windowHeight - 10; // 10px padding

            return (x, y);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to calculate position above taskbar, falling back to mouse cursor");
            return GetMouseCursorPosition();
        }
    }
}
