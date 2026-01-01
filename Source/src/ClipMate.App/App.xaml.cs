using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using ClipMate.App.Logging;
using ClipMate.App.Services;
using ClipMate.App.Services.Initialization;
using ClipMate.App.ViewModels;
using ClipMate.App.Views;
using ClipMate.App.Views.Dialogs;
using ClipMate.Core.DependencyInjection;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Data.DependencyInjection;
using ClipMate.Data.Services;
using ClipMate.Platform.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Tomlyn;
using ILogger = Serilog.ILogger;
using MessageBox = System.Windows.MessageBox;

namespace ClipMate.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    private const string _mutexName = "Global\\ClipMate_SingleInstance_Mutex";

    /// <summary>
    /// Shared EventLogSink instance created early for Serilog integration.
    /// Must be created before Serilog configuration and registered in DI.
    /// </summary>
    private static readonly EventLogSink _eventLogSink = new();

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

            // Initialize coordinators (registers for events)
            _ = ServiceProvider.GetRequiredService<DatabaseMaintenanceCoordinator>();

            // Start all hosted services (clipboard monitoring, PowerPaste, etc)
            await _host.StartAsync();

            // Check if any databases need backup
            await CheckAndPromptForBackupsAsync();

            // Apply icon configuration
            var configService = ServiceProvider.GetRequiredService<IConfigurationService>();
            var config = configService.Configuration.Preferences;

            // Validate icon visibility - at least one must be visible
            if (config is { ShowTrayIcon: false, ShowTaskbarIcon: false })
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
    /// Checks if any databases are due for backup and prompts the user.
    /// </summary>
    private async Task CheckAndPromptForBackupsAsync()
    {
        try
        {
            var configService = ServiceProvider.GetRequiredService<IConfigurationService>();
            var maintenanceService = ServiceProvider.GetRequiredService<IDatabaseMaintenanceService>();

            var config = configService.Configuration;

            // Check if backup interval is disabled globally
            if (config.Preferences.BackupIntervalDays is 0 or >= 9999)
            {
                _logger?.LogDebug("Automatic backups disabled (interval: {Days} days)", config.Preferences.BackupIntervalDays);
                return;
            }

            // Get list of databases that need backup
            var databasesDue = await maintenanceService.CheckBackupDueAsync(config.Databases.Values);

            if (databasesDue.Count == 0)
            {
                _logger?.LogDebug("No databases due for backup");
                return;
            }

            _logger?.LogInformation("Found {Count} database(s) due for backup", databasesDue.Count);

            // Filter out databases that were recently prompted (within 3 days)
            const int promptSnoozesDays = 3;
            var now = DateTime.UtcNow;
            var databasesToPrompt = databasesDue
                .Where(p => p.LastBackupPromptDate == null ||
                            (now - p.LastBackupPromptDate.Value).TotalDays >= promptSnoozesDays)
                .ToList();

            if (databasesToPrompt.Count == 0)
            {
                _logger?.LogDebug("All databases due for backup were recently prompted (within {Days} days)", promptSnoozesDays);
                return;
            }

            // Show backup dialog(s) based on count
            if (databasesToPrompt.Count == 1)
            {
                // Single database - show simple backup dialog
                var dbConfig = databasesToPrompt[0];
                var dialog = new DatabaseBackupDialog(
                    dbConfig,
                    config.Preferences.BackupIntervalDays,
                    config.Preferences.AutoConfirmBackupSeconds)
                {
                    Owner = Current.GetDialogOwner(),
                };

                // Record that we prompted the user
                dbConfig.LastBackupPromptDate = DateTime.UtcNow;

                if (dialog.ShowDialog() == true && dialog is { ShouldBackup: true, UpdatedConfiguration: not null })
                    await PerformBackupAsync(dbConfig, dialog.UpdatedConfiguration);
                else
                    await configService.SaveAsync(); // Save the prompt date even if cancelled
            }
            else
            {
                // Multiple databases - show batch backup dialog
                var dialog = new MultipleDatabaseBackupDialog(
                    databasesToPrompt,
                    config.Preferences.BackupIntervalDays,
                    config.Preferences.AutoConfirmBackupSeconds)
                {
                    Owner = Current.GetDialogOwner(),
                };

                // Record that we prompted the user for all databases
                foreach (var item in databasesToPrompt)
                    item.LastBackupPromptDate = DateTime.UtcNow;

                if (dialog.ShowDialog() == true && dialog.ShouldBackup && dialog.SelectedDatabases.Count != 0)
                {
                    foreach (var item in dialog.SelectedDatabases)
                        await PerformBackupAsync(item, item);
                }
                else
                    await configService.SaveAsync(); // Save the prompt dates even if cancelled
            }

            // Save configuration with updated backup dates
            await configService.SaveAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking for backup-due databases");
        }
    }

    /// <summary>
    /// Performs a database backup operation.
    /// </summary>
    private async Task PerformBackupAsync(DatabaseConfiguration dbConfig, DatabaseConfiguration updatedConfig)
    {
        try
        {
            var maintenanceService = ServiceProvider.GetRequiredService<IDatabaseMaintenanceService>();

            var backupPath = await maintenanceService.BackupDatabaseAsync(
                dbConfig,
                updatedConfig.BackupDirectory);

            // Update last backup date
            dbConfig.LastBackupDate = DateTime.Now;
            dbConfig.BackupDirectory = updatedConfig.BackupDirectory;

            _logger?.LogInformation("Backup completed: {Path}", backupPath);

            // Show success notification (optional - could use toast notification)
            MessageBox.Show(
                $"Database backup completed successfully!\n\nBackup saved to:\n{backupPath}",
                "Backup Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Backup failed for database: {Database}", dbConfig.Name);

            MessageBox.Show(
                $"Database backup failed:\n\n{ex.Message}",
                "Backup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Checks if database exists and is valid. If not, runs the setup wizard.
    /// </summary>
    /// <returns>True if database is ready, false if user cancelled setup.</returns>
    private async Task<bool> CheckDatabaseAndRunSetupIfNeededAsync()
    {
        // Configure Serilog before any database checks so we can log setup issues
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipMate",
            "Logs");

        try
        {
            Directory.CreateDirectory(logDirectory);
        }
        catch (Exception ex)
        {
            // Can't create log directory - log to Debug output only
            Debug.WriteLine($"Failed to create log directory: {ex.Message}");
        }

        var logFilePath = Path.Join(logDirectory, "clipmate-.log");

        // Create early logger for database setup (also writes to EventLogSink)
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .WriteTo.Debug()
            .WriteTo.Console()
            .WriteTo.Sink(new EventLogSerilogSink(_eventLogSink)) // Capture for Event Log dialog
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 10_485_760, // 10 MB
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ThreadId}] {SourceContext} - {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        var earlyLogger = Log.ForContext<App>();
        earlyLogger.Information("Checking database setup...");

        // Default paths
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipMate");

        var configPath = Path.Join(appDataPath, "clipmate.toml");

        // Check if configuration exists
        if (!File.Exists(configPath))
        {
            earlyLogger.Information("No configuration file found - launching setup wizard");

            // Show setup wizard for first-time setup
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddSerilog();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            var setupServiceLogger = loggerFactory.CreateLogger<SetupService>();
            var setupService = new SetupService(setupServiceLogger);
            var setupLogger = loggerFactory.CreateLogger<SetupWizard>();
            var setupWizard = new SetupWizard(setupLogger, setupService);

            earlyLogger.Debug("Showing setup wizard dialog...");
            var result = setupWizard.ShowDialog();

            if (result != true || !setupWizard.SetupCompleted)
            {
                earlyLogger.Warning("User cancelled database setup");
                return false;
            }

            _databasePath = setupWizard.DatabasePath;
            earlyLogger.Information("Setup wizard completed successfully. Database path: {DatabasePath}", _databasePath);
            return true;
        }

        // Load configuration to check databases
        earlyLogger.Information("Loading configuration from {ConfigPath}", configPath);
        using var configLoggerFactory = LoggerFactory.Create(p =>
        {
            p.AddSerilog();
            p.SetMinimumLevel(LogLevel.Information);
        });

        var configLogger = configLoggerFactory.CreateLogger<ConfigurationService>();
        var configService = new ConfigurationService(appDataPath, configLogger);

        ClipMateConfiguration? config;

        try
        {
            config = await configService.LoadAsync();
        }
        catch (TomlException ex)
        {
            // TOML parsing error - file is malformed
            earlyLogger.Error(ex, "Configuration file has invalid TOML syntax");
            config = await HandleBadConfigurationFileAsync(earlyLogger, configPath, $"TOML syntax error: {ex.Message}");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Configuration validation failed"))
        {
            // Validation error - file parsed but content is invalid
            earlyLogger.Error(ex, "Configuration validation failed");
            var errorDetails = ex.Message.Replace("Configuration validation failed:\n", "");
            config = await HandleBadConfigurationFileAsync(earlyLogger, configPath, errorDetails);
        }
        catch (OperationCanceledException)
        {
            // User cancelled configuration recovery
            return false;
        }
        catch (Exception ex)
        {
            // Unexpected error during configuration loading
            earlyLogger.Error(ex, "Unexpected error loading configuration file");
            MessageBox.Show(
                $"An unexpected error occurred while loading the configuration:\n\n{ex.Message}\n\n" +
                "The application will now exit. Please check the log files for details.",
                "Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return false;
        }

        // If config failed to load or has no databases, run setup wizard
        if (config == null || config.Databases.Count == 0)
        {
            if (config?.Databases.Count == 0)
                earlyLogger.Warning("Configuration exists but no databases configured - launching setup wizard");

            using var loggerFactory = LoggerFactory.Create(p =>
            {
                p.AddSerilog();
                p.SetMinimumLevel(LogLevel.Information);
            });

            var setupServiceLogger = loggerFactory.CreateLogger<SetupService>();
            var setupService = new SetupService(setupServiceLogger);
            var setupLogger = loggerFactory.CreateLogger<SetupWizard>();
            var setupWizard = new SetupWizard(setupLogger, setupService);

            var result = setupWizard.ShowDialog();

            if (result != true || !setupWizard.SetupCompleted)
            {
                earlyLogger.Warning("User cancelled database setup");
                return false;
            }

            _databasePath = setupWizard.DatabasePath;
            earlyLogger.Information("Setup wizard completed successfully. Database path: {DatabasePath}", _databasePath);
            return true;
        }

        // Find the default database (validation already done in ConfigurationService)
        var defaultDbKey = config.DefaultDatabase!;
        var dbConfig = config.Databases[defaultDbKey];

        _databasePath = Environment.ExpandEnvironmentVariables(dbConfig.FilePath);

        earlyLogger.Information("Using database: {Name} at {Path}", dbConfig.Name, _databasePath);

        // Check if database file exists
        if (!File.Exists(_databasePath))
        {
            earlyLogger.Warning("Configured database file does not exist: {Path}", _databasePath);
            earlyLogger.Information("Database will be created during initialization");
        }
        else
        {
            // Validate existing database using SetupService
            try
            {
                earlyLogger.Debug("Validating database schema...");

                // Create temporary SetupService for validation
                using var loggerFactory = LoggerFactory.Create(p =>
                {
                    p.AddSerilog();
                    p.SetMinimumLevel(LogLevel.Information);
                });

                var setupLogger = loggerFactory.CreateLogger<SetupService>();
                var setupService = new SetupService(setupLogger);

                var isValid = await setupService.ValidateDatabaseAsync(_databasePath);
                if (isValid)
                    earlyLogger.Information("Database schema validation successful");
                else
                    earlyLogger.Warning("Database validation failed - database will be repaired during initialization");
            }
            catch (Exception ex)
            {
                earlyLogger.Warning(ex, "Database validation failed - database will be repaired during initialization");
            }
        }

        return true;
    }

    /// <summary>
    /// Creates and configures the host builder.
    /// </summary>
    private static IHostBuilder CreateHostBuilder(string databasePath)
    {
        // Configure Serilog early
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipMate",
            "Logs");

        Directory.CreateDirectory(logDirectory);

        var logFilePath = Path.Join(logDirectory, "clipmate-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .WriteTo.Debug()
            .WriteTo.Console()
            .WriteTo.Sink(new EventLogSerilogSink(_eventLogSink)) // Capture for Event Log dialog
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 10_485_760, // 10 MB
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ThreadId}] {SourceContext} - {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        return Host.CreateDefaultBuilder()
            .UseSerilog() // Use Serilog for logging
            .ConfigureAppConfiguration(config => config.SetBasePath(AppContext.BaseDirectory))
            .ConfigureServices(services =>
            {
                // App Host
                services.AddHostedService<ApplicationHostService>();

                // Register Core services
                services.AddClipMateCore();

                // Register Data services (includes hosted services for database init and clipboard monitoring)
                services.AddClipMateData(databasePath);

                // Register Platform services
                services.AddClipMatePlatform();

                // Register Dialog service (App-layer implementation for Platform layer)
                services.AddSingleton<IDialogService, DialogService>();

                // Register active window tracking service
                services.AddSingleton<IActiveWindowService, ActiveWindowService>();

                // Register Update Checker as hosted service
                services.AddHostedService<UpdateCheckerService>();

                // Register MVVM Toolkit Messenger as singleton
                services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

                // Register the shared EventLogSink instance (already receiving logs via Serilog sink)
                services.AddSingleton<IEventLogSink>(_eventLogSink);

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

                // Register Collection Tree Builder
                services.AddTransient<ICollectionTreeBuilder, CollectionTreeBuilder>();

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
                services.AddTransient<RenameClipDialogViewModel>();
                services.AddTransient<ClipViewerViewModel>();

                // Register Clip Viewer factory and manager
                services.AddSingleton<Func<ClipViewerViewModel>>(p => p.GetRequiredService<ClipViewerViewModel>);
                services.AddSingleton<IClipViewerWindowManager, ClipViewerWindowManager>();

                // Register Text Tools components
                services.AddTransient<TextToolsViewModel>();
                services.AddTransient<TextToolsDialog>();

                // Register Diagnostic ViewModels
                services.AddTransient<ClipboardDiagnosticsViewModel>();
                services.AddTransient<EventLogViewModel>();
                services.AddTransient<PasteTraceViewModel>();
                services.AddTransient<SqlMaintenanceViewModel>();

                // Register Text Cleanup dialog (no ViewModel - uses code-behind)
                services.AddTransient<TextCleanupDialog>();

                // Register Options dialog components
                services.AddTransient<GeneralOptionsViewModel>();
                services.AddTransient<PowerPasteOptionsViewModel>();
                services.AddTransient<QuickPasteOptionsViewModel>();
                services.AddTransient<EditorOptionsViewModel>();
                services.AddTransient<CapturingOptionsViewModel>();
                services.AddTransient<ApplicationProfilesOptionsViewModel>();
                services.AddTransient<SoundsOptionsViewModel>();
                services.AddTransient<HotkeysOptionsViewModel>();
                services.AddTransient<DatabaseOptionsViewModel>();
                services.AddTransient<AdvancedOptionsViewModel>();
                services.AddTransient<OptionsViewModel>();
                services.AddTransient<OptionsDialog>();

                // Register database dialogs
                services.AddTransient<DatabaseRestoreWizard>();

                // Register coordinators
                services.AddSingleton<HotkeyCoordinator>();
                services.AddSingleton<DatabaseMaintenanceCoordinator>();
                services.AddSingleton<ClipOperationsCoordinator>();
                services.AddSingleton<CollectionOperationsCoordinator>();
                services.AddTransient<HotkeyWindow>();

                // Register initialization pipeline and steps
                services.AddSingleton<StartupInitializationPipeline>();
                services.AddSingleton<IStartupInitializationStep, ConfigurationLoadingStep>();
                services.AddSingleton<IStartupInitializationStep, DatabaseSchemaInitializationStep>();
                services.AddSingleton<IStartupInitializationStep, DatabaseLoadingStep>();
                services.AddSingleton<IStartupInitializationStep, DefaultDataInitializationStep>();
                services.AddSingleton<IStartupInitializationStep, HotkeyInitializationStep>();
                services.AddSingleton<IStartupInitializationStep, HotkeyRegistrationStep>();
                services.AddSingleton<IStartupInitializationStep, ClipOperationsInitializationStep>();
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
        finally
        {
            // Ensure Serilog flushes all buffered log entries
            await Log.CloseAndFlushAsync();
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

    /// <summary>
    /// Handles a bad configuration file by offering to backup and run setup wizard.
    /// </summary>
    /// <returns>Null if user chooses to backup and continue to setup wizard, throws if user exits.</returns>
    private static Task<ClipMateConfiguration?> HandleBadConfigurationFileAsync(ILogger logger,
        string configPath,
        string errorDetails)
    {
        var result = MessageBox.Show(
            $"Your configuration file has errors:\n\n{errorDetails}\n\n" +
            $"Configuration file: {configPath}\n\n" +
            "Click 'Yes' to rename the bad file and run setup wizard\n" +
            "Click 'No' to exit and fix the file manually",
            "Configuration Error",
            MessageBoxButton.YesNo,
            MessageBoxImage.Error);

        if (result == MessageBoxResult.No)
        {
            logger.Information("User chose to exit and fix configuration manually");
            throw new OperationCanceledException("User cancelled configuration recovery");
        }

        // Rename the bad configuration file
        try
        {
            var backupPath = $"{configPath}.bad.{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Move(configPath, backupPath);
            logger.Information("Renamed bad configuration file to {BackupPath}", backupPath);

            MessageBox.Show(
                $"Your configuration file has been renamed to:\n{Path.GetFileName(backupPath)}\n\n" +
                "The setup wizard will now help you create a new configuration.",
                "Configuration Backup",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception moveEx)
        {
            logger.Error(moveEx, "Failed to rename bad configuration file");
            MessageBox.Show(
                "Could not rename the bad configuration file.\n\n" +
                "Please manually rename or delete it and restart the application.",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            throw new OperationCanceledException("Failed to backup configuration file", moveEx);
        }

        return Task.FromResult<ClipMateConfiguration?>(null); // Fall through to setup wizard
    }
}
