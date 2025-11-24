using ClipMate.Core.Services;
using ClipMate.Data.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace ClipMate.Data.Services;

/// <summary>
/// Hosted service that ensures the database is created and initialized on application startup.
/// </summary>
public class DatabaseInitializationHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseInitializationHostedService> _logger;

    public DatabaseInitializationHostedService(
        IServiceProvider serviceProvider,
        ILogger<DatabaseInitializationHostedService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing database...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ClipMateDbContext>();

            // Apply any pending migrations (this will create the database if it doesn't exist)
            // This is the proper way to handle database schema updates for customers
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pendingMigrations.Any())
            {
                _logger.LogInformation("Applying {Count} pending migrations: {Migrations}", 
                    pendingMigrations.Count(), 
                    string.Join(", ", pendingMigrations));
                    
                await context.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("Migrations applied successfully");
            }
            else
            {
                _logger.LogInformation("Database is up to date, no migrations needed");
            }

            // Seed default ClipMate 7.5 collection structure
            var seeder = new DefaultDataSeeder(context, scope.ServiceProvider.GetService<ILogger<DefaultDataSeeder>>());
            await seeder.SeedDefaultDataAsync();

            _logger.LogInformation("Database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Database initialization service stopping");
        return Task.CompletedTask;
    }
}
