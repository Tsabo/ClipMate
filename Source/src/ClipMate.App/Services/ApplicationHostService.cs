using System.Windows;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace ClipMate.App.Services;

/// <summary>
/// Managed host of the application.
/// </summary>
internal class ApplicationHostService : IHostedService
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<ApplicationHostService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ApplicationHostService(
        IServiceProvider serviceProvider,
        IConfigurationService configurationService,
        ILogger<ApplicationHostService> logger)
    {
        _serviceProvider = serviceProvider;
        _configurationService = configurationService;
        _logger = logger;
    }

    /// <summary>
    /// Triggered when the application host is ready to start the service.
    /// </summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    public Task StartAsync(CancellationToken cancellationToken) => HandleActivationAsync();

    /// <summary>
    /// Triggered when the application host is performing a graceful shutdown.
    /// </summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be graceful.</param>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Creates and shows windows based on startup configuration.
    /// </summary>
    private async Task HandleActivationAsync()
    {
        var config = _configurationService.Configuration.Preferences;

        _logger.LogDebug(
            "Startup settings: LoadExplorer={LoadExplorer}, LoadClassic={LoadClassic}, InitialShowMode={InitialShowMode}",
            config.LoadExplorerAtStartup,
            config.LoadClassicAtStartup,
            config.InitialShowMode);

        // Ensure we're on the UI thread when creating and showing windows
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                // Pre-load Explorer window if configured (creates but may not show)
                ExplorerWindow? explorerWindow = null;
                if (config.LoadExplorerAtStartup)
                {
                    if (!Application.Current.Windows.OfType<ExplorerWindow>().Any())
                    {
                        explorerWindow = _serviceProvider.GetRequiredService<ExplorerWindow>();
                        _logger.LogDebug("Explorer window pre-loaded");
                    }
                    else
                    {
                        explorerWindow = Application.Current.Windows.OfType<ExplorerWindow>().First();
                    }
                }

                // Pre-load Classic window if configured (creates but may not show)
                // ClassicWindow is managed by ClassicWindowCoordinator, we just trigger creation
                if (config.LoadClassicAtStartup)
                {
                    // Classic window is created on-demand by ClassicWindowCoordinator
                    // For pre-loading, we can resolve the coordinator to ensure it's ready
                    _ = _serviceProvider.GetRequiredService<ClassicWindowCoordinator>();
                    _logger.LogDebug("Classic window coordinator initialized for pre-loading");
                }

                // Show the appropriate window based on InitialShowMode
                switch (config.InitialShowMode)
                {
                    case InitialShowMode.Explorer:
                        // Create explorer window if not already pre-loaded
                        if (explorerWindow == null && !Application.Current.Windows.OfType<ExplorerWindow>().Any())
                            explorerWindow = _serviceProvider.GetRequiredService<ExplorerWindow>();

                        explorerWindow?.Show();
                        _logger.LogInformation("Explorer window shown at startup");
                        break;

                    case InitialShowMode.Classic:
                        // Show Classic window via coordinator event
                        var coordinator = _serviceProvider.GetRequiredService<ClassicWindowCoordinator>();
                        coordinator.ShowClassicWindow();
                        _logger.LogInformation("Classic window shown at startup");
                        break;

                    case InitialShowMode.Nothing:
                        _logger.LogInformation("No window shown at startup (minimized to tray)");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize windows at startup");
                MessageBox.Show(
                    $"Failed to show main window: {ex.Message}\n\n{ex.StackTrace}",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                throw;
            }
        });
    }
}
