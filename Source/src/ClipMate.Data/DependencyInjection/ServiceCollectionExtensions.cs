using ClipMate.Core.Services;
using ClipMate.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.DependencyInjection;

/// <summary>
/// Extension methods for registering Data layer services with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds ClipMate Data services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="databasePath">The path to the SQLite database file.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddClipMateData(this IServiceCollection services, string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Database path cannot be null or empty.", nameof(databasePath));

        // Register EF Core DbContext with SQLite
        services.AddDbContext<ClipMateDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));

        // NOTE: Repositories are NOT registered in DI directly
        // They are created on-demand by IDatabaseContextFactory for multi-database support
        // The factory pattern allows each repository to use the correct DbContext for its database

        // Register services
        // Stateless services that coordinate repository operations - registered as Transient
        // They get scoped contexts via repository factories, so no need to be Singleton
        services.AddTransient<IClipService, ClipService>();
        services.AddTransient<IShortcutService, ShortcutService>();
        services.AddTransient<IApplicationFilterService, ApplicationFilterService>();
        services.AddTransient<IFolderService, FolderService>();
        services.AddTransient<ISqlValidationService, SqlValidationService>();
        services.AddTransient<ISetupService, SetupService>();
        services.AddTransient<ISearchService, SearchService>();
        services.AddTransient<IClipAppendService, ClipAppendService>();
        services.AddTransient<DatabaseSchemaMigrationService>();
        services.AddTransient<IDatabaseMaintenanceService, DatabaseMaintenanceService>();
        services.AddTransient<IRetentionEnforcementService, RetentionEnforcementService>();

        // Stateful services that must be Singleton
        // TemplateService maintains template cache and sequence counter
        services.AddSingleton<ITemplateService, TemplateService>();

        // CollectionService maintains active collection/database state shared across the app
        services.AddSingleton<ICollectionService, CollectionService>();

        // PowerPasteService maintains explode mode state
        services.AddSingleton<IPowerPasteService, PowerPasteService>();

        // UndoService maintains undo/redo state
        services.AddSingleton<IUndoService, UndoService>();

        // Register default data initialization service (ensures Inbox exists and is set as active)
        services.AddSingleton<DefaultDataInitializationService>();

        // Register configuration service
        // Configuration is always stored in the AppData\ClipMate directory, not in the database directory
        var configDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipMate");

        var configurationService = new ConfigurationService(configDirectory,
            services.BuildServiceProvider().GetRequiredService<ILogger<ConfigurationService>>());

        services.AddSingleton<IConfigurationService>(configurationService);

        // Apply SQLite pragmas based on configuration
        ApplySqlitePragmas(databasePath, configurationService);

        // Register multi-database management
        services.AddSingleton<IDatabaseContextFactory, DatabaseContextFactory>();
        services.AddSingleton<IDatabaseManager, DatabaseManager>();

        // Register SQL maintenance factory (creates per-session service instances)
        services.AddSingleton<ISqlMaintenanceServiceFactory, SqlMaintenanceServiceFactory>();

        // Register ClipboardCoordinator as singleton first (so it can be injected)
        services.AddSingleton<ClipboardCoordinator>();

        // Register MaintenanceSchedulerService as singleton
        services.AddSingleton<MaintenanceSchedulerService>();

        // Register hosted services (use the singleton instances)
        services.AddHostedService(p => p.GetRequiredService<ClipboardCoordinator>());
        services.AddHostedService(p => p.GetRequiredService<MaintenanceSchedulerService>());

        return services;
    }

    /// <summary>
    /// Applies SQLite pragmas based on configuration settings.
    /// </summary>
    private static void ApplySqlitePragmas(string databasePath, IConfigurationService configurationService)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();

            var enableCachedWrites = configurationService.Configuration.Preferences.EnableCachedDatabaseWrites;

            if (enableCachedWrites)
            {
                // Performance mode: WAL journal + NORMAL synchronous
                // WAL provides concurrency and faster writes
                // NORMAL synchronous balances performance vs safety
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA journal_mode = WAL;";
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA synchronous = NORMAL;";
                    cmd.ExecuteNonQuery();
                }
            }
            else
            {
                // Safety mode: DELETE journal + FULL synchronous
                // Every write is immediately flushed to disk
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA journal_mode = DELETE;";
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA synchronous = FULL;";
                    cmd.ExecuteNonQuery();
                }
            }
        }
        catch (Exception ex)
        {
            // Log warning but don't fail startup
            Console.WriteLine($"Warning: Failed to apply SQLite pragmas: {ex.Message}");
        }
    }
}
