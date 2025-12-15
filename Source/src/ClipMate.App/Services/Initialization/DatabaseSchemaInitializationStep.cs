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
            using var scope = _serviceProvider.CreateScope();

            var configService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
            var config = configService.Configuration;

            var contextFactory = scope.ServiceProvider.GetRequiredService<IDatabaseContextFactory>();
            var migrationService = scope.ServiceProvider.GetRequiredService<DatabaseSchemaMigrationService>();

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

                    // Get or create context for this database
                    var dbContext = contextFactory.GetOrCreateContext(databasePath);

                    var fileExists = File.Exists(databasePath);
                    _logger.LogInformation("Database '{Name}' file exists: {Exists}", databaseName, fileExists);

                    // Ensure database file exists
                    _logger.LogDebug("Ensuring database '{Name}' file is created...", databaseName);
                    await dbContext.Database.EnsureCreatedAsync(cancellationToken);
                    _logger.LogInformation("Database '{Name}' file creation verified", databaseName);

                    // Migrate schema to match EF Core model
                    _logger.LogInformation("Starting schema migration for database '{Name}'...", databaseName);
                    await migrationService.MigrateAsync(dbContext, cancellationToken);

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
