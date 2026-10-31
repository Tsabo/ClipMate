using System.Diagnostics.CodeAnalysis;
using ClipMate.Core.Helpers;
using ClipMate.Core.Services;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.DependencyInjection;

/// <summary>
/// Extension methods for registering Data layer services with the DI container.
/// </summary>
[ExcludeFromCodeCoverage]
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

        // Reads a clip's formats and content from a local Database or a Remote Database (its Peer)
        services.AddTransient<IClipContentService, ClipContentService>();
        services.AddTransient<IShortcutService, ShortcutService>();
        services.AddTransient<IApplicationFilterService, ApplicationFilterService>();
        services.AddTransient<IFolderService, FolderService>();
        services.AddTransient<ISqlValidationService, SqlValidationService>();
        services.AddTransient<ISetupService, SetupService>();
        services.AddTransient<ISearchService, SearchService>();
        services.AddTransient<IClipAppendService, ClipAppendService>();
        services.AddTransient<DatabaseSchemaMigrationService>();
        services.AddSingleton<IDatabaseIdService, DatabaseIdService>(); // remembers Database IDs; see DatabaseIdService
        services.AddTransient<ISharedDatabaseService, SharedDatabaseService>();

        // Live change notifications: every write through the factory's contexts is seen by the change monitor, and the
        // feed reports those in Shared Databases (only while a Peer is watching)
        services.AddSingleton<DatabaseChangeMonitor>();
        services.AddSingleton<SharedDatabaseChangeFeed>();
        services.AddSingleton<ISharedDatabaseChangeFeed>(p => p.GetRequiredService<SharedDatabaseChangeFeed>());
        services.AddTransient<IDatabaseMaintenanceService, DatabaseMaintenanceService>();
        services.AddTransient<IClipMate7ImportService, ClipMate7ImportService>();
        services.AddTransient<IRetentionEnforcementService, RetentionEnforcementService>();

        // Stateful services that must be Singleton
        // TemplateService maintains template cache and sequence counter
        services.AddSingleton<ITemplateService, TemplateService>();

        // CollectionService maintains active collection/database state shared across the app
        services.AddSingleton<ICollectionService, CollectionService>();

        // PowerPasteService maintains explode mode state; PowerPasteStatus is what other services read
        services.AddSingleton<PowerPasteStatus>();
        services.AddSingleton<IPowerPasteService, PowerPasteService>();

        // AutoAppendService maintains Auto-Append mode toggle state
        services.AddSingleton<IAutoAppendService, AutoAppendService>();

        // UndoService maintains undo/redo state
        services.AddSingleton<IUndoService, UndoService>();

        // Register default data initialization service (ensures Inbox exists and is set as active)
        services.AddSingleton<DefaultDataInitializationService>();

        // Register configuration service
        // Configuration is always stored in the AppData\ClipMate directory, not in the database directory
        var configDirectory = ClipMatePaths.DataDirectory;

        services.AddSingleton<IConfigurationService>(sp =>
            new ConfigurationService(configDirectory, sp.GetRequiredService<ILogger<ConfigurationService>>()));

        // Peer records (clipmate.toml): the single source of truth for which Devices are trusted
        services.AddSingleton<IPeerStore, PeerStore>();

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
}
