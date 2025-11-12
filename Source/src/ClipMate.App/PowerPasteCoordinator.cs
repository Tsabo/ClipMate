using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.App.Views;

namespace ClipMate.App;

/// <summary>
/// Coordinates PowerPaste functionality including hotkey registration and window lifecycle.
/// </summary>
public class PowerPasteCoordinator : IDisposable
{
    private const int PowerPasteHotkeyId = 1001;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHotkeyService _hotkeyService;
    private readonly ILogger<PowerPasteCoordinator> _logger;
    private PowerPasteWindow? _powerPasteWindow;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PowerPasteCoordinator"/> class.
    /// </summary>
    public PowerPasteCoordinator(
        IServiceProvider serviceProvider,
        IHotkeyService hotkeyService,
        ILogger<PowerPasteCoordinator> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes PowerPaste with hotkey registration.
    /// Must be called after the main window is loaded.
    /// </summary>
    /// <param name="mainWindow">The main application window for receiving hotkey messages.</param>
    public void Initialize(Window mainWindow)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (mainWindow == null)
        {
            throw new ArgumentNullException(nameof(mainWindow));
        }

        try
        {
            // Initialize HotkeyService with the main window
            if (_hotkeyService is Platform.Services.HotkeyService platformHotkeyService)
            {
                platformHotkeyService.Initialize(mainWindow);
            }

            // Register Ctrl+Shift+V hotkey for PowerPaste
            var registered = _hotkeyService.RegisterHotkey(
                PowerPasteHotkeyId,
                Core.Models.ModifierKeys.Control | Core.Models.ModifierKeys.Shift,
                (int)Key.V,
                OnPowerPasteHotkeyPressed);

            if (registered)
            {
                _logger.LogInformation("PowerPaste hotkey registered successfully (Ctrl+Shift+V)");
            }
            else
            {
                _logger.LogWarning("Failed to register PowerPaste hotkey (Ctrl+Shift+V). The hotkey may already be in use.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing PowerPaste coordinator");
        }
    }

    /// <summary>
    /// Called when the PowerPaste hotkey (Ctrl+Shift+V) is pressed.
    /// </summary>
    private void OnPowerPasteHotkeyPressed()
    {
        _logger.LogDebug("PowerPaste hotkey pressed (Ctrl+Shift+V)");

        try
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing PowerPaste window");
        }
    }

    /// <summary>
    /// Disposes the coordinator and unregisters hotkeys.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            // Unregister hotkey
            _hotkeyService.UnregisterHotkey(PowerPasteHotkeyId);
            _logger.LogInformation("PowerPaste hotkey unregistered");

            // Close window if open
            _powerPasteWindow?.Close();
            _powerPasteWindow = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing PowerPaste coordinator");
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
