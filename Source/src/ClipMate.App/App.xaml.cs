using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ClipMate.Core.DependencyInjection;
using ClipMate.Core.Exceptions;
using ClipMate.Data.DependencyInjection;
using ClipMate.Data.Services;
using ClipMate.Platform.DependencyInjection;

namespace ClipMate.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    private ILogger<App>? _logger;

    /// <summary>
    /// Gets the service provider for the application.
    /// </summary>
    public static IServiceProvider Services => ((App)Current)._serviceProvider 
        ?? throw new InvalidOperationException("Service provider is not initialized.");

    /// <summary>
    /// Called when the application starts.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize DPI awareness
        ClipMate.Platform.DpiHelper.InitializeDpiAwareness();

        // Setup global exception handlers
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // Get application data path for logging
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipMate");
        var databasePath = Path.Combine(appDataPath, "clipmate.db");

        // Configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Initialize logger
        _logger = _serviceProvider.GetService<ILogger<App>>();

        // Initialize database
        try
        {
            var initialized = _serviceProvider.InitializeDatabase();
            if (!initialized)
            {
                _logger?.LogError("Failed to initialize database schema.");
                MessageBox.Show(
                    "Failed to initialize the database. The application will now exit.",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            _logger?.LogInformation("ClipMate application started successfully");
            _logger?.LogInformation("Database path: {DatabasePath}", databasePath);
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
            return;
        }

        // Create and show main window
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        // Start clipboard monitoring
        var coordinator = _serviceProvider.GetRequiredService<ClipboardCoordinator>();
        _logger?.LogInformation("Starting clipboard monitoring coordinator");
        _ = coordinator.StartAsync(); // Fire and forget - runs in background
        _logger?.LogInformation("Clipboard monitoring coordinator started");
    }

    /// <summary>
    /// Configures the dependency injection container.
    /// </summary>
    private void ConfigureServices(IServiceCollection services)
    {
        // Get application data path
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipMate");

        if (!Directory.Exists(appDataPath))
        {
            Directory.CreateDirectory(appDataPath);
        }

        var databasePath = Path.Combine(appDataPath, "clipmate.db");

        // Configure logging
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug); // Changed to Debug for troubleshooting
            builder.AddDebug();
            builder.AddConsole();
        });

        // Register Core services
        services.AddClipMateCore();

        // Register Data services
        services.AddClipMateData(databasePath);

        // Register Platform services
        services.AddClipMatePlatform();

        // Register MainWindow
        services.AddTransient<MainWindow>();

        // ViewModels will be registered here as they are created
        // Example:
        // services.AddTransient<MainViewModel>();
        // services.AddTransient<HistoryViewModel>();
        // services.AddTransient<SearchViewModel>();
    }

    /// <summary>
    /// Called when the application exits.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.LogInformation("ClipMate application shutting down");

        // Stop clipboard monitoring
        var coordinator = _serviceProvider?.GetService<ClipboardCoordinator>();
        if (coordinator != null)
        {
            _ = coordinator.StopAsync(); // Fire and forget
        }

        // Dispose service provider
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

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

