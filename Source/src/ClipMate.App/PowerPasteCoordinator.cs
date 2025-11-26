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
/// Coordinates PowerPaste functionality including hotkey registration and window lifecycle.
/// </summary>
public class PowerPasteCoordinator : IHostedService, IDisposable
{
    private const int PowerPasteHotkeyId = 1001;
    private readonly IHotkeyService _hotkeyService;
    private readonly ILogger<PowerPasteCoordinator> _logger;
    private readonly IServiceProvider _serviceProvider;
    private bool _disposed;
    private HotkeyWindow? _hotkeyWindow;
    private PowerPasteWindow? _powerPasteWindow;

    /// <summary>
    /// Initializes a new instance of the <see cref="PowerPasteCoordinator" /> class.
    /// </summary>
    public PowerPasteCoordinator(IServiceProvider serviceProvider,
        IHotkeyService hotkeyService,
        ILogger<PowerPasteCoordinator> logger)
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
                _powerPasteWindow?.Close();
                _powerPasteWindow = null;

                _hotkeyWindow?.Close();
                _hotkeyWindow = null;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing PowerPaste coordinator");
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Initializes PowerPaste with hotkey registration when the host starts.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting PowerPaste coordinator");

            // Create and show the hidden hotkey window on the UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                _hotkeyWindow = new HotkeyWindow();
                _hotkeyWindow.Show(); // CRITICAL: Must be shown for message pump to work

                // Initialize HotkeyService with the hotkey window (must be on UI thread)
                if (_hotkeyService is HotkeyService platformService)
                    platformService.Initialize(_hotkeyWindow);
            });

            // Register Ctrl+Shift+V hotkey for PowerPaste
            var virtualKey = KeyInterop.VirtualKeyFromKey(Key.V);
            var registered = _hotkeyService.RegisterHotkey(
                PowerPasteHotkeyId,
                ModifierKeys.Control | ModifierKeys.Shift,
                virtualKey,
                OnPowerPasteHotkeyPressed);

            if (registered)
                _logger.LogInformation("PowerPaste hotkey registered successfully (Ctrl+Shift+V)");
            else
                _logger.LogWarning("Failed to register PowerPaste hotkey (Ctrl+Shift+V). The hotkey may already be in use.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting PowerPaste coordinator");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops PowerPaste and unregisters hotkeys when the host stops.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping PowerPaste coordinator");

        try
        {
            // Unregister hotkey
            _hotkeyService.UnregisterHotkey(PowerPasteHotkeyId);
            _logger.LogInformation("PowerPaste hotkey unregistered");

            // Close windows on the UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                _powerPasteWindow?.Close();
                _powerPasteWindow = null;

                _hotkeyWindow?.Close();
                _hotkeyWindow = null;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping PowerPaste coordinator");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the PowerPaste hotkey (Ctrl+Shift+V) is pressed.
    /// </summary>
    private void OnPowerPasteHotkeyPressed()
    {
        _logger.LogInformation("PowerPaste hotkey pressed (Ctrl+Shift+V)");

        try
        {
            // Ensure we're on the UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                // If window is already open, just activate it
                if (_powerPasteWindow != null && _powerPasteWindow.IsVisible)
                {
                    _powerPasteWindow.Activate();
                    _powerPasteWindow.Focus();
                    _logger.LogDebug("PowerPaste window already open, activating");
                    return;
                }

                // Create new window instance from DI
                _powerPasteWindow = _serviceProvider.GetRequiredService<PowerPasteWindow>();

                // Close window when user is done
                _powerPasteWindow.Closed += (s, e) =>
                {
                    _logger.LogDebug("PowerPaste window closed");
                    _powerPasteWindow = null;
                };

                // Show the window
                _powerPasteWindow.Show();
                _powerPasteWindow.Activate();
                _logger.LogDebug("PowerPaste window shown");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing PowerPaste window");
        }
    }
}
