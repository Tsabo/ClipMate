using System.IO;
using ClipMate.Data;
using ClipMate.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.Services.Initialization;

/// <summary>
/// Initialization step that ensures the database schema is created and migrated.
/// </summary>
public class DatabaseSchemaInitializationStep : IStartupInitializationStep
{
    private readonly ILogger<DatabaseSchemaInitializationStep> _logger;
    private readonly IServiceProvider _serviceProvider;

    public DatabaseSchemaInitializationStep(IServiceProvider serviceProvider,
        ILogger<DatabaseSchemaInitializationStep> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "Database Schema";

    public int Order => 10;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== Database Schema Initialization Started ===");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ClipMateDbContext>();

            var connectionString = dbContext.Database.GetConnectionString();
            _logger.LogInformation("Database connection string: {ConnectionString}", connectionString);

            // Check if database file exists
            var databasePath = connectionString?.Replace("Data Source=", "").Trim();
            if (!string.IsNullOrEmpty(databasePath))
            {
                var fileExists = File.Exists(databasePath);
                _logger.LogInformation("Database file exists: {Exists} at path: {Path}", fileExists, databasePath);
            }

            // Ensure database file exists
            _logger.LogDebug("Ensuring database file is created...");
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            _logger.LogInformation("Database file creation verified");

            // Migrate schema to match EF Core model
            _logger.LogInformation("Starting schema migration...");
            var migrationService = scope.ServiceProvider.GetRequiredService<DatabaseSchemaMigrationService>();
            await migrationService.MigrateAsync(dbContext, cancellationToken);

            _logger.LogInformation("=== Database Schema Initialization Completed Successfully ===");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "FATAL: Database schema initialization failed");

            throw new InvalidOperationException("Database schema initialization failed. See inner exception for details.", ex);
        }
    }
}
