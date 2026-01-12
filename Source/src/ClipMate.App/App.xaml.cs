using System.IO;
using System.Windows.Threading;
using ClipMate.App.DependencyInjection;
using ClipMate.App.Logging;
using ClipMate.App.Services;
using ClipMate.App.Views;
using ClipMate.Core.DependencyInjection;
using ClipMate.Core.Models.Configuration;
using ClipMate.Data.DependencyInjection;
using ClipMate.Data.Services;
using ClipMate.Platform.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Tomlyn;
using ILogger = Serilog.ILogger;
using MessageBox = System.Windows.MessageBox;

namespace ClipMate.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    /// <summary>
    /// Shared EventLogSink instance created early for Serilog integration.
    /// Must be created before Serilog configuration and registered in DI.
    /// </summary>
    private static readonly EventLogSink _eventLogSink = new();

    private string? _databasePath;
    private IHost? _host;
    private ILogger<App>? _logger;
    private SingleInstanceManager? _singleInstanceManager;

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
        using var singleInstanceLoggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
        _singleInstanceManager = new SingleInstanceManager(
            singleInstanceLoggerFactory.CreateLogger<SingleInstanceManager>());

        if (!_singleInstanceManager.TryAcquire())
        {
            SingleInstanceManager.ShowAlreadyRunningMessage();
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

            Log.Information("Database check completed, building host...");

            // Build the host (now with confirmed database path)
            try
            {
                Log.Information("About to call CreateHostBuilder...");
                var builder = CreateHostBuilder(_databasePath!);
                Log.Information("CreateHostBuilder returned, about to call Build()...");
                _host = builder.Build();
                Log.Information("Host built successfully");
            }
            catch (Exception hostEx)
            {
                var errorMsg = $"Failed to build host: {hostEx.Message}\n\nType: {hostEx.GetType().FullName}\n\nStack:\n{hostEx.StackTrace}";
                Log.Fatal(hostEx, "Failed to build host");

                // Write to debug file for diagnosis
                try
                {
                    var debugPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipMate", "startup-error.txt");
                    await File.WriteAllTextAsync(debugPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{errorMsg}\n\nInner Exception:\n{hostEx.InnerException?.ToString() ?? "None"}");
                }
                catch
                {
                    /* ignore */
                }

                MessageBox.Show(errorMsg, "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            // Get logger
            _logger = ServiceProvider.GetRequiredService<ILogger<App>>();

            // Run startup orchestration (initialization, maintenance, window creation)
            // This MUST happen before starting hosted services so active collection is set
            var startupService = ServiceProvider.GetRequiredService<StartupOrchestrationService>();
            await startupService.RunAsync();

            // Start all hosted services (clipboard monitoring, PowerPaste, etc)
            await _host.StartAsync();
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
        // Configure Serilog before any database checks so we can log setup issues
        Log.Logger = SerilogConfigurationFactory.CreateEarlyLogger(_eventLogSink);

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
        var configuration = LoadConfiguration();

        Log.Logger = SerilogConfigurationFactory.CreateConfiguredLogger(_eventLogSink, configuration);

        return Host.CreateDefaultBuilder()
            .UseSerilog() // Use Serilog for logging
            .ConfigureAppConfiguration(p => p.SetBasePath(AppContext.BaseDirectory))
            .ConfigureServices(services =>
            {
                // Register the shared EventLogSink instance before any service registration
                services.AddSingleton(_eventLogSink);

                // Register Core services
                services.AddClipMateCore();

                // Register Data services (includes hosted services for database init and clipboard monitoring)
                services.AddClipMateData(databasePath);

                // Register Platform services
                services.AddClipMatePlatform();

                // Register App services
                services.AddClipMateApp();
            });
    }

    /// <summary>
    /// Loads the ClipMate configuration from disk.
    /// </summary>
    private static ClipMateConfiguration LoadConfiguration()
    {
        // Default paths
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipMate");

        var configPath = Path.Join(appDataPath, "clipmate.toml");
        var tomlContent = File.ReadAllText(configPath);
        var configuration = Toml.ToModel<ClipMateConfiguration>(tomlContent, options: new TomlModelOptions
        {
            IgnoreMissingProperties = true,
        });

        return configuration;
    }

    /// <summary>
    /// Called when the application exits.
    /// </summary>
    protected override async void OnExit(ExitEventArgs e)
    {
        _logger?.LogInformation("ClipMate application shutting down");

        try
        {
            // Run shutdown maintenance tasks (if configured)
            var backupService = ServiceProvider.GetService<BackupOrchestrationService>();
            if (backupService != null)
                await backupService.RunShutdownMaintenanceTasksAsync();

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
        _singleInstanceManager?.Dispose();

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
