using ClipMate.Core.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for database setup and initialization tasks.
/// </summary>
public class SetupService
{
    private readonly ILogger<SetupService> _logger;

    public SetupService(ILogger<SetupService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new ClipMate database at the specified path.
    /// </summary>
    /// <param name="databasePath">The full path to the database file to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if database was created successfully, false otherwise.</returns>
    public async Task<bool> CreateDatabaseAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Database path cannot be null or empty.", nameof(databasePath));

        try
        {
            _logger.LogInformation("Creating database at: {DatabasePath}", databasePath);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogDebug("Created directory: {Directory}", directory);
            }

            // Create DbContext with the specified path
            var optionsBuilder = new DbContextOptionsBuilder<ClipMateDbContext>();
            optionsBuilder.UseSqlite($"Data Source={databasePath}");

            await using var context = new ClipMateDbContext(optionsBuilder.Options);

            // Create database schema
            var created = await context.Database.EnsureCreatedAsync(cancellationToken);

            if (created)
            {
                await context.Database.OpenConnectionAsync(cancellationToken);
                await DatabaseIdStore.EnsureDatabaseIdAsync(context.Database.GetDbConnection(), cancellationToken);
                await context.Database.CloseConnectionAsync();

                _logger.LogInformation("Database created successfully at: {DatabasePath}", databasePath);
                return true;
            }

            _logger.LogWarning("Database already exists at: {DatabasePath}", databasePath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating database at: {DatabasePath}", databasePath);
            return false;
        }
    }

    /// <summary>
    /// Validates that a database exists and has the correct schema.
    /// </summary>
    /// <param name="databasePath">The full path to the database file to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if database is valid, false otherwise.</returns>
    public async Task<bool> ValidateDatabaseAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            return false;

        if (!File.Exists(databasePath))
        {
            _logger.LogWarning("Database file does not exist: {DatabasePath}", databasePath);
            return false;
        }

        try
        {
            _logger.LogDebug("Validating database at: {DatabasePath}", databasePath);

            var optionsBuilder = new DbContextOptionsBuilder<ClipMateDbContext>();
            optionsBuilder.UseSqlite($"Data Source={databasePath}");

            await using var context = new ClipMateDbContext(optionsBuilder.Options);

            // Try to open connection and query a table
            var canConnect = await context.Database.CanConnectAsync(cancellationToken);

            if (!canConnect)
            {
                _logger.LogWarning("Cannot connect to database: {DatabasePath}", databasePath);
                return false;
            }

            // Verify schema by checking if key tables exist
            _ = await context.Clips.AnyAsync(cancellationToken) || true; // AnyAsync returns false if empty, so always return true

            _logger.LogDebug("Database validation successful: {DatabasePath}", databasePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating database at: {DatabasePath}", databasePath);
            return false;
        }
    }

    /// <summary>
    /// Gets the default database path for the application.
    /// </summary>
    /// <returns>The full path to the default database location.</returns>
    public string GetDefaultDatabasePath()
    {
        var clipMateDataPath = Path.Combine(ClipMatePaths.DataDirectory, "Databases");
        var defaultDbPath = Path.Combine(clipMateDataPath, "ClipMate.db");

        _logger.LogDebug("Default database path: {DefaultDbPath}", defaultDbPath);
        return defaultDbPath;
    }

    /// <summary>
    /// Seeds default data (collections, etc.) into an existing database and applies schema migrations.
    /// </summary>
    /// <param name="databasePath">The full path to the database file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SeedDefaultDataAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Database path cannot be null or empty.", nameof(databasePath));

        _logger.LogInformation("Seeding default data for database: {DatabasePath}", databasePath);

        // Create DbContext
        var optionsBuilder = new DbContextOptionsBuilder<ClipMateDbContext>();
        optionsBuilder.UseSqlite($"Data Source={databasePath}");
        await using var context = new ClipMateDbContext(optionsBuilder.Options);

        // Run schema migrations first
        var migrationService = new DatabaseSchemaMigrationService(_logger as ILogger<DatabaseSchemaMigrationService>);
        await migrationService.MigrateAsync(context, cancellationToken);

        // Seed default collections and data
        var seeder = new DefaultDataSeeder(context, _logger as ILogger<DefaultDataSeeder>);
        await seeder.SeedDefaultDataAsync(false);

        _logger.LogInformation("Default data seeding completed for: {DatabasePath}", databasePath);
    }
}
