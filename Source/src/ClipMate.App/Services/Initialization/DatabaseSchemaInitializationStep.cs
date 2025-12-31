using System.IO;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.Services.Initialization;

/// <summary>
/// Initialization step that ensures all configured database schemas are created and migrated.
/// Supports ClipMate's multi-database architecture.
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

    // Order 20: Run AFTER ConfigurationLoadingStep (Order 10) to ensure databases are configured
    public int Order => 20;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== Database Schema Initialization Started ===");

        try
        {
            // All services are singletons, no scope needed
            var configService = _serviceProvider.GetRequiredService<IConfigurationService>();
            var config = configService.Configuration;

            var contextFactory = _serviceProvider.GetRequiredService<IDatabaseContextFactory>();
            var migrationService = _serviceProvider.GetRequiredService<DatabaseSchemaMigrationService>();

            // Get all auto-load databases from configuration
            var autoLoadDatabases = config.Databases
                .Where(p => p.Value.AutoLoad)
                .ToList();

            if (autoLoadDatabases.Count == 0)
            {
                _logger.LogWarning("No auto-load databases configured. At least one database should be marked for auto-load.");
                return;
            }

            _logger.LogInformation("Found {Count} auto-load databases to initialize", autoLoadDatabases.Count);

            var successCount = 0;
            var failureCount = 0;

            // Initialize and migrate each auto-load database
            foreach (var dbConfig in autoLoadDatabases)
            {
                var databaseName = dbConfig.Value.Name;
                var databasePath = Environment.ExpandEnvironmentVariables(dbConfig.Value.FilePath);

                try
                {
                    _logger.LogInformation("Initializing database: '{Name}' at {Path}", databaseName, databasePath);

                    // Ensure directory exists
                    var directory = Path.GetDirectoryName(databasePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                        _logger.LogInformation("Created database directory: {Directory}", directory);
                    }

                    // Check if database file exists BEFORE creating it
                    var databaseExisted = File.Exists(databasePath);
                    _logger.LogInformation("Database '{Name}' file exists: {Exists}", databaseName, databaseExisted);

                    // Create context for this database (contexts are not cached, fresh per operation)
                    await using var dbContext = contextFactory.CreateContext(databasePath);

                    // Ensure database file exists
                    _logger.LogDebug("Ensuring database '{Name}' file is created...", databaseName);
                    await dbContext.Database.EnsureCreatedAsync(cancellationToken);

                    if (!databaseExisted)
                        _logger.LogInformation("Database '{Name}' was newly created", databaseName);
                    else
                        _logger.LogInformation("Database '{Name}' file already existed", databaseName);

                    // Migrate schema to match EF Core model
                    _logger.LogInformation("Starting schema migration for database '{Name}'...", databaseName);
                    await migrationService.MigrateAsync(dbContext, cancellationToken);

                    // Seed default data if this was a newly created database
                    if (!databaseExisted)
                    {
                        _logger.LogInformation("Database '{Name}' is new, seeding default collections...", databaseName);
                        var seeder = new DefaultDataSeeder(dbContext,
                            _serviceProvider.GetService<ILogger<DefaultDataSeeder>>());

                        await seeder.SeedDefaultDataAsync(false);
                        _logger.LogInformation("Default data seeding completed for '{Name}'", databaseName);
                    }

                    successCount++;
                    _logger.LogInformation("✓ Database '{Name}' initialization completed successfully", databaseName);
                }
                catch (Exception ex)
                {
                    failureCount++;
                    _logger.LogError(ex, "✗ Failed to initialize database '{Name}' at {Path}", databaseName, databasePath);

                    // Continue with other databases rather than failing completely
                    // The application can still function with partial database availability
                }
            }

            _logger.LogInformation("=== Database Schema Initialization Completed: {Success} succeeded, {Failed} failed ===",
                successCount, failureCount);

            if (failureCount > 0 && successCount == 0)
                throw new InvalidOperationException($"All {failureCount} database(s) failed to initialize. Application cannot start.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogCritical(ex, "FATAL: Database schema initialization failed");
            throw new InvalidOperationException("Database schema initialization failed. See inner exception for details.", ex);
        }
    }
}
