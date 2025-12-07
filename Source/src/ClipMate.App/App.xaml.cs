using System.IO;
using System.Windows;
using System.Windows.Threading;
using ClipMate.App.Services;
using ClipMate.App.Services.Initialization;
using ClipMate.App.ViewModels;
using ClipMate.App.Views;
using ClipMate.Core.DependencyInjection;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.DependencyInjection;
using ClipMate.Platform.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MessageBox = System.Windows.MessageBox;

namespace ClipMate.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    private const string _mutexName = "Global\\ClipMate_SingleInstance_Mutex";
    private string? _databasePath;
    private IHost? _host;

#pragma warning disable CS0649 // Field is assigned via dependency injection
    private ILogger<App>? _logger;
#pragma warning restore CS0649
    private Mutex? _singleInstanceMutex;
    private TrayIconWindow? _trayIconWindow;

    /// <summary>
    /// Gets the service provider for dependency injection.
    /// </summary>
    public IServiceProvider ServiceProvider => _host?.Services ?? throw new InvalidOperationException("Host not initialized");

    /// <summary>
    /// Called when the application starts.
    /// </summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Enforce single instance - check if another instance is already running
        _singleInstanceMutex = new Mutex(true, _mutexName, out var createdNew);

        if (!createdNew)
        {
            // Another instance is already running
            MessageBox.Show(
                "ClipMate is already running. Please check the system tray.",
                "ClipMate",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Shutdown(0);

            return;
        }

        // Setup global exception handlers
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            // Check if database exists, if not show setup wizard
            if (!await CheckDatabaseAndRunSetupIfNeededAsync())
            {
                // User cancelled setup
                Shutdown(0);

                return;
            }

            // Build the host (now with confirmed database path)
            _host = CreateHostBuilder(_databasePath!).Build();

            // Get logger
            _logger = ServiceProvider.GetRequiredService<ILogger<App>>();

            // Run initialization pipeline (database schema, configuration, default data)
            var pipeline = ServiceProvider.GetRequiredService<StartupInitializationPipeline>();
            await pipeline.RunAsync();

            // Start all hosted services (clipboard monitoring, PowerPaste, etc)
            await _host.StartAsync();

            // Apply icon configuration
            var configService = ServiceProvider.GetRequiredService<IConfigurationService>();
            var config = configService.Configuration.Preferences;

            // Validate icon visibility - at least one must be visible
            if (!config.ShowTrayIcon && !config.ShowTaskbarIcon)
            {
                _logger?.LogCritical("Both tray icon and taskbar icon are disabled! Forcing tray icon to be visible for user access.");
                config.ShowTrayIcon = true;
                await configService.SaveAsync();
            }

            // Create and show the tray icon window if enabled
            if (config.ShowTrayIcon)
            {
                _trayIconWindow = ServiceProvider.GetRequiredService<TrayIconWindow>();
                _trayIconWindow.Show();
                _logger?.LogDebug("Tray icon window created");
            }
            else
                _logger?.LogDebug("Tray icon disabled in configuration");

            // ExplorerWindow ShowInTaskbar is set in ExplorerWindow constructor from config
        }
        catch (Exception ex)
        {
            _logger?.LogCritical(ex, "Fatal error during application startup");
            MessageBox.Show(
                $"A fatal error occurred during startup:\n\n{ex.Message}\n\nThe application will now exit.",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
        }
    }

    /// <summary>
    /// Checks if database exists and is valid. If not, runs the setup wizard.
    /// </summary>
    /// <returns>True if database is ready, false if user cancelled setup.</returns>
    private async Task<bool> CheckDatabaseAndRunSetupIfNeededAsync()
    {
        // Default database path
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipMate");

        _databasePath = Path.Combine(appDataPath, "clipmate.db");

        // Check if database file exists and has tables
        var databaseExists = File.Exists(_databasePath);
        var databaseValid = false;

        if (databaseExists)
        {
            try
            {
                // Check if database has the required tables
                var optionsBuilder = new DbContextOptionsBuilder<ClipMateDbContext>();
                optionsBuilder.UseSqlite($"Data Source={_databasePath}");

                await using var context = new ClipMateDbContext(optionsBuilder.Options);

                // Try to query Collections table (will throw if doesn't exist)
                await context.Collections.AnyAsync();
                databaseValid = true;
            }
            catch
            {
                // Database exists but is invalid/empty
                databaseValid = false;
            }
        }

        if (!databaseValid)
        {
            // Show setup wizard
            // Create a minimal logger for the wizard
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddDebug();
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            var setupLogger = loggerFactory.CreateLogger<SetupWizard>();
            var setupWizard = new SetupWizard(setupLogger, appDataPath);

            var result = setupWizard.ShowDialog();

            if (result != true || !setupWizard.SetupCompleted)
            {
                // User cancelled setup
                return false;
            }

            // Use the path chosen in setup wizard
            _databasePath = setupWizard.DatabasePath;

            // Configuration has been saved by the wizard
        }

        return true;
    }

    /// <summary>
    /// Creates and configures the host builder.
    /// </summary>
    private static IHostBuilder CreateHostBuilder(string databasePath)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(config => config.SetBasePath(AppContext.BaseDirectory))
            .ConfigureServices(services =>
            {
                // Configure logging
                services.AddLogging(builder =>
                {
                    builder.SetMinimumLevel(LogLevel.Debug);
                    builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Error);
                    builder.AddDebug();
                    builder.AddConsole();
                });

                // App Host
                services.AddHostedService<ApplicationHostService>();

                // Register Core services
                services.AddClipMateCore();

                // Register Data services (includes hosted services for database init and clipboard monitoring)
                services.AddClipMateData(databasePath);

                // Register Platform services
                services.AddClipMatePlatform();

                // Register Update Checker as hosted service
                services.AddHostedService<UpdateCheckerService>();

                // Register MVVM Toolkit Messenger as singleton
                services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

                // Register ClipBar (quick paste picker) as hosted service
                services.AddSingleton<ClassicWindowCoordinator>();
                services.AddHostedService(p => p.GetRequiredService<ClassicWindowCoordinator>());

                // Register ExplorerWindow as singleton (always exists, just hidden/shown)
                services.AddSingleton<ExplorerWindow>();
                services.AddSingleton<IWindow, ExplorerWindow>(p => p.GetRequiredService<ExplorerWindow>());

                // Register TrayIconWindow (system tray icon)
                services.AddTransient<TrayIconWindow>();

                // Register ClipBar (quick paste picker) components
                services.AddTransient<ClassicViewModel>();
                services.AddTransient<ClassicWindow>();

                // Register ViewModels
                services.AddSingleton<MainMenuViewModel>(); // Shared menu ViewModel
                services.AddSingleton<ExplorerWindowViewModel>();
                services.AddSingleton<CollectionTreeViewModel>();
                services.AddSingleton<ClipListViewModel>();
                services.AddSingleton<PreviewPaneViewModel>();
                services.AddSingleton<SearchViewModel>();
                services.AddSingleton<QuickPasteToolbarViewModel>();
                services.AddTransient<ClipViewerToolbarViewModel>();
                services.AddTransient<ClipPropertiesViewModel>();
                services.AddTransient<ClipViewerViewModel>();

                // Register Clip Viewer factory and manager
                services.AddSingleton<Func<ClipViewerViewModel>>(p => p.GetRequiredService<ClipViewerViewModel>);
                services.AddSingleton<IClipViewerWindowManager, ClipViewerWindowManager>();

                // Register Text Tools components
                services.AddTransient<TextToolsViewModel>();
                services.AddTransient<TextToolsDialog>();

                // Register Text Cleanup dialog (no ViewModel - uses code-behind)
                services.AddTransient<TextCleanupDialog>();

                // Register Template components
                services.AddTransient<TemplateEditorViewModel>();
                services.AddTransient<TemplateEditorDialog>();
                services.AddTransient<PromptDialog>();

                // Register Options dialog components
                services.AddTransient<GeneralOptionsViewModel>();
                services.AddTransient<PowerPasteOptionsViewModel>();
                services.AddTransient<QuickPasteOptionsViewModel>();
                services.AddTransient<EditorOptionsViewModel>();
                services.AddTransient<CapturingOptionsViewModel>();
                services.AddTransient<ApplicationProfilesOptionsViewModel>();
                services.AddTransient<SoundsOptionsViewModel>();
                services.AddTransient<HotkeysOptionsViewModel>();
                services.AddTransient<OptionsViewModel>();
                services.AddTransient<OptionsDialog>();

                // Register hotkey coordinator
                services.AddSingleton<HotkeyCoordinator>();

                // Register initialization pipeline and steps
                services.AddSingleton<StartupInitializationPipeline>();
                services.AddSingleton<IStartupInitializationStep, DatabaseSchemaInitializationStep>();
                services.AddSingleton<IStartupInitializationStep, ConfigurationLoadingStep>();
                services.AddSingleton<IStartupInitializationStep, DefaultDataInitializationStep>();
                services.AddSingleton<IStartupInitializationStep, HotkeyRegistrationStep>();
            });
    }

    /// <summary>
    /// Called when the application exits.
    /// </summary>
    protected override async void OnExit(ExitEventArgs e)
    {
        _logger?.LogInformation("ClipMate application shutting down");

        try
        {
            if (_host != null)
            {
                // Stop all hosted services gracefully
                await _host.StopAsync(TimeSpan.FromSeconds(5));
                _host.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during application shutdown");
        }

        // Release single instance mutex
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }

    /// <summary>
    /// Handles unhandled exceptions from the AppDomain.
    /// </summary>
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        _logger?.LogCritical(exception, "Unhandled exception in AppDomain");

        if (e.IsTerminating)
        {
            MessageBox.Show(
                $"A fatal error occurred:\n\n{exception?.Message}\n\nThe application will now exit.",
                "Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Handles unhandled exceptions from the UI thread.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unhandled exception in UI thread");

        MessageBox.Show(
            $"An error occurred:\n\n{e.Exception.Message}",
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        // Mark as handled to prevent application crash
        e.Handled = true;
    }

    /// <summary>
    /// Handles unobserved task exceptions.
    /// </summary>
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unobserved task exception");

        // Mark as observed to prevent application crash
        e.SetObserved();
    }
}
