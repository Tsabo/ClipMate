using System.Windows.Input;
using ClipMate.App.Views;
using ClipMate.Core.Services;
using ClipMate.Platform.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;
using ModifierKeys = ClipMate.Core.Models.ModifierKeys;

namespace ClipMate.App;

/// <summary>
/// Coordinates ClipBar (quick paste picker) functionality including hotkey registration and window lifecycle.
/// ClipBar is a quick access popup window (Ctrl+Shift+V) for selecting and pasting individual clips.
/// This is distinct from the PowerPaste sequential automation feature.
/// </summary>
public class ClipBarCoordinator : IHostedService, IDisposable
{
    private const int ClipBarHotkeyId = 1001;
    private readonly IHotkeyService _hotkeyService;
    private readonly ILogger<ClipBarCoordinator> _logger;
    private readonly IServiceProvider _serviceProvider;
    private bool _disposed;
    private HotkeyWindow? _hotkeyWindow;
    private ClipBarWindow? _clipBarWindow;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClipBarCoordinator" /> class.
    /// </summary>
    public ClipBarCoordinator(IServiceProvider serviceProvider,
        IHotkeyService hotkeyService,
        ILogger<ClipBarCoordinator> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                _clipBarWindow?.Close();
                _clipBarWindow = null;

                _hotkeyWindow?.Close();
                _hotkeyWindow = null;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing ClipBar coordinator");
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
                ClipBarHotkeyId,
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
            _hotkeyService.UnregisterHotkey(ClipBarHotkeyId);
            _logger.LogInformation("ClipBar hotkey unregistered");

            // Close windows on the UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                _clipBarWindow?.Close();
                _clipBarWindow = null;

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
    /// Called when the ClipBar hotkey (Ctrl+Shift+V) is pressed.
    /// </summary>
    private void OnClipBarHotkeyPressed()
    {
        _logger.LogInformation("ClipBar hotkey pressed (Ctrl+Shift+V)");

        try
        {
            // Ensure we're on the UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                // If window is already open, just activate it
                if (_clipBarWindow != null && _clipBarWindow.IsVisible)
                {
                    _clipBarWindow.Activate();
                    _clipBarWindow.Focus();
                    _logger.LogDebug("ClipBar window already open, activating");
                    return;
                }

                // Create new window instance from DI
                _clipBarWindow = _serviceProvider.GetRequiredService<ClipBarWindow>();

                // Close window when user is done
                _clipBarWindow.Closed += (s, e) =>
                {
                    _logger.LogDebug("ClipBar window closed");
                    _clipBarWindow = null;
                };

                // Show the window
                _clipBarWindow.Show();
                _clipBarWindow.Activate();
                _logger.LogDebug("ClipBar window shown");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing ClipBar window");
        }
    }
}
