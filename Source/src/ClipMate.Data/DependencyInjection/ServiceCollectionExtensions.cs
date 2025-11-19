using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data.Repositories;
using ClipMate.Data.Services;
using System.IO;

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
        {
            throw new ArgumentException("Database path cannot be null or empty.", nameof(databasePath));
        }

        // Register EF Core DbContext with SQLite
        services.AddDbContext<ClipMateDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));

        // Register repositories
        services.AddScoped<IClipRepository, ClipRepository>();
        services.AddScoped<ICollectionRepository, CollectionRepository>();
        services.AddScoped<IFolderRepository, FolderRepository>();
        services.AddScoped<ITemplateRepository, TemplateRepository>();
        services.AddScoped<ISearchQueryRepository, SearchQueryRepository>();
        services.AddScoped<IApplicationFilterRepository, ApplicationFilterRepository>();
        services.AddScoped<ISoundEventRepository, SoundEventRepository>();
        
        // Register ClipMate 7.5 compatibility repositories
        services.AddScoped<IClipDataRepository, ClipDataRepository>();
        services.AddScoped<IBlobRepository, BlobRepository>();
        services.AddScoped<IShortcutRepository, ShortcutRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        // Register services
        // Note: IClipboardService implementation is in Platform layer
        services.AddScoped<IClipService, ClipService>();
        services.AddScoped<IApplicationFilterService, ApplicationFilterService>();
        services.AddSingleton<ICollectionService, CollectionService>();
        services.AddSingleton<IFolderService, FolderService>();
        services.AddSingleton<ISearchService, SearchService>(); // Singleton for search history caching
        services.AddSingleton<ITemplateService, TemplateService>(); // Singleton for template management
        services.AddSingleton<DatabaseInitializationService>(); // Database initialization

        // Register configuration service
        // Extract directory from database path
        var configDirectory = Path.GetDirectoryName(databasePath) ?? 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipMate");
        services.AddSingleton<IConfigurationService>(sp => 
            new ConfigurationService(configDirectory, sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ConfigurationService>>()));

        // Register multi-database management
        services.AddSingleton<DatabaseContextFactory>();
        services.AddSingleton<DatabaseManager>();

        // Register ClipboardCoordinator as singleton first (so it can be injected)
        services.AddSingleton<ClipboardCoordinator>();
        
        // Register hosted services (use the singleton instance)
        services.AddHostedService<DatabaseInitializationHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<ClipboardCoordinator>());

        return services;
    }

    /// <summary>
    /// Initializes the database schema and applies migrations.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>True if initialization was successful.</returns>
    public static bool InitializeDatabase(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClipMateDbContext>();
        
        try
        {
            // Apply any pending migrations
            dbContext.Database.Migrate();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
