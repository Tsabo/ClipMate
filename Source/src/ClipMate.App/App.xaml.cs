using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ClipMate.Core.DependencyInjection;
using ClipMate.Core.Exceptions;
using ClipMate.Core.Services;
using ClipMate.Data.DependencyInjection;
using ClipMate.Data.Services;
using ClipMate.Platform.DependencyInjection;
using ClipMate.Platform.Services;

namespace ClipMate.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    private ILogger<App>? _logger;
    private Mutex? _singleInstanceMutex;
    private const string _mutexName = "Global\\ClipMate_SingleInstance_Mutex";

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
            
            // Try to activate the existing instance (future enhancement: use IPC to signal existing instance)
            Shutdown(0);
            return;
        }

        // Initialize DPI awareness (Windows 8.1+)
        if (OperatingSystem.IsWindowsVersionAtLeast(8, 1))
        {
            ClipMate.Platform.DpiHelper.InitializeDpiAwareness();
        }

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

            // Initialize default collections and folders
            var dbInitService = _serviceProvider.GetRequiredService<DatabaseInitializationService>();
            dbInitService.InitializeAsync().Wait(); // Synchronous wait during startup
            _logger?.LogInformation("Database default data initialization complete");
            
            // Set the first collection as active (or create default if none exist)
            var collectionService = _serviceProvider.GetRequiredService<ICollectionService>();
            var collections = collectionService.GetAllAsync().Result;
            if (collections.Count > 0)
            {
                collectionService.SetActiveAsync(collections[0].Id).Wait();
                _logger?.LogInformation("Active collection set to: {CollectionName}", collections[0].Name);
            }
            else
            {
                _logger?.LogWarning("No collections found to set as active");
            }
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

        // Create dedicated hidden window for hotkey messages
        // This window must be "shown" to receive WM_HOTKEY messages, but it's completely invisible
        var hotkeyWindow = new HotkeyWindow();
        hotkeyWindow.Show(); // CRITICAL: Must be shown for message pump to work
        
        // Initialize PowerPaste with the hotkey window
        var powerPasteCoordinator = _serviceProvider.GetRequiredService<PowerPasteCoordinator>();
        powerPasteCoordinator.Initialize(hotkeyWindow);
        _logger?.LogInformation("PowerPaste coordinator initialized");
        
        // Create main window
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        
        // Initialize system tray (pass mainWindow for DPI-aware context menu positioning)
        var systemTray = _serviceProvider.GetRequiredService<SystemTrayService>();
        systemTray.Initialize(mainWindow);
        systemTray.ShowWindowRequested += (_, _) =>
        {
            mainWindow.Show();
            mainWindow.Activate();
        };
        systemTray.ExitRequested += (_, _) =>
        {
            _logger?.LogInformation("Exit requested from system tray");
            mainWindow.PrepareForExit();
            Shutdown();
        };
        _logger?.LogInformation("System tray initialized");
        
        // Check command-line arguments for /show flag
        bool showWindow = e.Args.Any(arg => 
            arg.Equals("/show", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("--show", StringComparison.OrdinalIgnoreCase));
        
        if (showWindow)
        {
            mainWindow.Show();
            _logger?.LogInformation("Main window shown (command-line flag)");
        }
        else
        {
            _logger?.LogInformation("Application started minimized to system tray");
        }

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
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Information); // Reduce EF Core verbosity
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

        // Register PowerPaste components
        services.AddTransient<ViewModels.PowerPasteViewModel>();
        services.AddTransient<Views.PowerPasteWindow>();
        services.AddSingleton<PowerPasteCoordinator>();

        // Register Collections/Folders ViewModel
        services.AddSingleton<ViewModels.CollectionTreeViewModel>();

        // Register Search ViewModel
        services.AddSingleton<ViewModels.SearchViewModel>(); // Singleton to maintain search state

        // Register Text Tools components
        services.AddTransient<ViewModels.TextToolsViewModel>();
        services.AddTransient<Views.TextToolsDialog>();

        // Register Template components
        services.AddTransient<ViewModels.TemplateEditorViewModel>();
        services.AddTransient<Views.TemplateEditorDialog>();
        services.AddTransient<Views.PromptDialog>();

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

        // Dispose system tray
        var systemTray = _serviceProvider?.GetService<SystemTrayService>();
        systemTray?.Dispose();

        // Dispose PowerPaste coordinator
        var powerPasteCoordinator = _serviceProvider?.GetService<PowerPasteCoordinator>();
        powerPasteCoordinator?.Dispose();

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

