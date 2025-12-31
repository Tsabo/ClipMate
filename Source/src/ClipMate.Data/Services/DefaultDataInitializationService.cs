using ClipMate.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service responsible for setting the active collection to Inbox on startup.
/// This must run BEFORE clipboard monitoring starts.
/// Supports multi-database architecture by finding Inbox in the default database.
/// </summary>
public class DefaultDataInitializationService
{
    private readonly ILogger<DefaultDataInitializationService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public DefaultDataInitializationService(IServiceProvider serviceProvider,
        ILogger<DefaultDataInitializationService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Sets the Inbox collection from the default database as the active collection.
    /// Should be called during application startup, before clipboard monitoring begins.
    /// Note: Default data seeding is handled by DatabaseSchemaInitializationStep.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing active collection");

        try
        {
            // All services are singletons, no scope needed
            var collectionService = _serviceProvider.GetRequiredService<ICollectionService>();
            var configurationService = _serviceProvider.GetRequiredService<IConfigurationService>();

            var configuration = configurationService.Configuration;
            var defaultDatabaseKey = configuration.DefaultDatabase;

            if (string.IsNullOrEmpty(defaultDatabaseKey))
            {
                _logger.LogWarning("No default database configured, cannot set active collection");
                return;
            }

            // Verify the default database exists in configuration
            if (!configuration.Databases.TryGetValue(defaultDatabaseKey, out var dbConfig))
            {
                _logger.LogWarning("Default database '{Key}' not found in configuration", defaultDatabaseKey);
                return;
            }

            // Expand environment variables to get the actual database path (which is the database key)
            var databasePath = Environment.ExpandEnvironmentVariables(dbConfig.FilePath);

            // Get collections from the default database specifically (not all databases)
            var collections = await collectionService.GetAllByDatabaseKeyAsync(databasePath, cancellationToken);

            // Find Inbox collection in the default database
            var inboxCollection = collections.FirstOrDefault(p =>
                p.Name.Equals("Inbox", StringComparison.OrdinalIgnoreCase));

            if (inboxCollection == null)
            {
                _logger.LogError("Inbox collection not found in default database '{Database}' - database may not have been seeded properly",
                    dbConfig.Name);

                return;
            }

            // Set Inbox from the default database as the active collection for new clips
            await collectionService.SetActiveAsync(inboxCollection.Id, databasePath, cancellationToken);

            _logger.LogInformation("Set Inbox collection (ID: {CollectionId}) from database '{Database}' as active",
                inboxCollection.Id, dbConfig.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize active collection");
            throw;
        }
    }
}
