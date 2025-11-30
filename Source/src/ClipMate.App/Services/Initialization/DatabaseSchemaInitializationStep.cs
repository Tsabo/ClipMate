using ClipMate.Data;
using ClipMate.Data.Services;
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
        _logger.LogDebug("Initializing database schema");

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClipMateDbContext>();

        // Ensure database file exists
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        // Migrate schema to match EF Core model
        var migrationService = scope.ServiceProvider.GetRequiredService<DatabaseSchemaMigrationService>();
        await migrationService.MigrateAsync(dbContext, cancellationToken);

        _logger.LogDebug("Database schema initialized successfully");
    }
}
