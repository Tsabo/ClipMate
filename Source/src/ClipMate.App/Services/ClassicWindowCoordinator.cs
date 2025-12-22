using ClipMate.Core.Events;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace ClipMate.App.Services;

/// <summary>
/// Coordinates Classic window lifecycle and ensures single instance.
/// Handles ShowClipBarRequestedEvent to display the Classic window.
/// Window positioning and state management is handled by ClassicWindow itself.
/// </summary>
public class ClassicWindowCoordinator : IHostedService, IRecipient<ShowClipBarRequestedEvent>, IDisposable
{
    private readonly ILogger<ClassicWindowCoordinator> _logger;
    private readonly IMessenger _messenger;
    private readonly IServiceProvider _serviceProvider;
    private ClassicWindow? _classicWindow;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassicWindowCoordinator" /> class.
    /// </summary>
    public ClassicWindowCoordinator(IServiceProvider serviceProvider,
        IMessenger messenger,
        ILogger<ClassicWindowCoordinator> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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
    /// Initializes coordinator when the host starts.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Classic window coordinator");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops coordinator when the host stops.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Classic window coordinator");

        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _classicWindow?.Close();
                _classicWindow = null;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping Classic window coordinator");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles ShowClipBarRequestedEvent by showing the ClipBar popup.
    /// </summary>
    public void Receive(ShowClipBarRequestedEvent message)
    {
        _logger.LogDebug("Show Classic Window requested (hotkey: {IsHotkey})", message.IsHotkeyTriggered);
        ShowClipBar(message.IsHotkeyTriggered);
    }

    /// <summary>
    /// Shows the Classic window, ensuring single instance.
    /// Window handles its own positioning via TOML configuration.
    /// </summary>
    private void ShowClipBar(bool isHotkeyTriggered)
    {
        try
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // If window exists and is visible, just activate it
                if (_classicWindow != null)
                {
                    if (_classicWindow.IsVisible)
                    {
                        _logger.LogDebug("Classic window already visible, activating");
                        _classicWindow.Activate();
                        return;
                    }

                    // Window exists but not visible, close and recreate
                    _logger.LogDebug("Classic window exists but not visible, recreating");
                    try
                    {
                        _classicWindow.Close();
                    }
                    catch
                    {
                        // Ignore close errors
                    }

                    _classicWindow = null;
                }

                // Create new window from DI with hotkey flag
                _classicWindow = ActivatorUtilities.CreateInstance<ClassicWindow>(
                    _serviceProvider,
                    isHotkeyTriggered);

                // Handle cleanup when window closes
                _classicWindow.Closed += (_, _) =>
                {
                    _logger.LogDebug("Classic window closed");
                    _classicWindow = null;
                };

                // Show and activate the window
                _classicWindow.Show();
                _classicWindow.Activate();

                _logger.LogDebug("Classic window shown");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing Classic window");
        }
    }
}
