using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data.Repositories;
using ClipMate.Data.Services;
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

        // Register repositories as Scoped (tied to DbContext lifetime)
        services.AddScoped<IClipRepository, ClipRepository>();
        services.AddScoped<ICollectionRepository, CollectionRepository>();
        services.AddScoped<IFolderRepository, FolderRepository>();
        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddScoped<ISearchQueryRepository, SearchQueryRepository>();
        services.AddScoped<IApplicationFilterRepository, ApplicationFilterRepository>();


        // Register ClipMate 7.5 compatibility repositories
        services.AddScoped<IClipDataRepository, ClipDataRepository>();
        services.AddScoped<IBlobRepository, BlobRepository>();
        services.AddScoped<IShortcutRepository, ShortcutRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IMonacoEditorStateRepository, MonacoEditorStateRepository>();

        // Register repository factory for multi-database support
        // Singleton because it's stateless and creates repositories on demand
        services.AddSingleton<IClipRepositoryFactory, ClipRepositoryFactory>();

        // Register services
        // ClipService is singleton because it supports multi-database operations via IClipRepositoryFactory
        // and must be shared across the entire application
        services.AddSingleton<IClipService, ClipService>();

        services.AddScoped<IApplicationFilterService, ApplicationFilterService>();

        // CollectionService is singleton because it maintains in-memory state (_activeCollectionId)
        // that must be shared across the entire application
        services.AddSingleton<ICollectionService, CollectionService>();

        services.AddScoped<IFolderService, FolderService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IPowerPasteService, PowerPasteService>(); // PowerPaste sequential automation service
        services.AddScoped<IClipAppendService, ClipAppendService>(); // Clip appending service
        services.AddScoped<DatabaseSchemaMigrationService>(); // Schema migration service
        services.AddScoped<IDatabaseMaintenanceService, DatabaseMaintenanceService>(); // Database maintenance (backup/restore/repair)
        services.AddScoped<IRetentionEnforcementService, RetentionEnforcementService>(); // Retention enforcement service
        services.AddSingleton<IUndoService, UndoService>(); // Undo/Redo service

        // Register default data initialization service (ensures Inbox exists and is set as active)
        services.AddSingleton<DefaultDataInitializationService>();

        // Register configuration service
        // Configuration is always stored in the AppData\ClipMate directory, not in the database directory
        var configDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClipMate");

        services.AddSingleton<IConfigurationService>(p =>
            new ConfigurationService(configDirectory, p.GetRequiredService<ILogger<ConfigurationService>>()));

        // Register multi-database management
        services.AddSingleton<IDatabaseContextFactory, DatabaseContextFactory>();
        services.AddSingleton<IDatabaseManager, DatabaseManager>();

        // Register ClipboardCoordinator as singleton first (so it can be injected)
        services.AddSingleton<ClipboardCoordinator>();

        // Register MaintenanceSchedulerService as singleton
        services.AddSingleton<MaintenanceSchedulerService>();

        // Register hosted services (use the singleton instances)
        services.AddHostedService(p => p.GetRequiredService<ClipboardCoordinator>());
        services.AddHostedService(p => p.GetRequiredService<MaintenanceSchedulerService>());

        return services;
    }
}
