using ClipMate.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service responsible for ensuring default collections and folders exist,
/// and setting the active collection/folder to Inbox on startup.
/// This must run BEFORE clipboard monitoring starts.
/// Uses DefaultDataSeeder to create the full ClipMate 7.5 collection structure.
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
    /// Ensures Inbox collection exists and sets it as the active collection.
    /// Should be called during application startup, before clipboard monitoring begins.
    /// Note: Default data seeding is now handled by DatabaseSchemaInitializationStep
    /// to ensure it happens immediately after database creation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing default data and active collection");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var collectionService = scope.ServiceProvider.GetRequiredService<ICollectionService>();
            var configurationService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDatabaseContextFactory>();

            var configuration = configurationService.Configuration;
            var defaultDatabaseKey = configuration.DefaultDatabase;

            if (string.IsNullOrEmpty(defaultDatabaseKey))
            {
                _logger.LogWarning("No default database configured, cannot initialize default data");
                return;
            }

            // Get the database path for the default database
            if (!configuration.Databases.TryGetValue(defaultDatabaseKey, out var dbConfig))
            {
                _logger.LogWarning("Default database '{Key}' not found in configuration", defaultDatabaseKey);
                return;
            }

            var databasePath = Environment.ExpandEnvironmentVariables(dbConfig.FilePath);

            // Get all collections (seeding should have happened in DatabaseSchemaInitializationStep)
            var collections = await collectionService.GetAllAsync(cancellationToken);

            // Find Inbox collection (default collection for new clips)
            var inboxCollection = collections.FirstOrDefault(p =>
                p.Name.Equals("Inbox", StringComparison.OrdinalIgnoreCase));

            if (inboxCollection == null)
            {
                _logger.LogError("Inbox collection not found - database may not have been seeded properly");
                return;
            }

            // Set Inbox as the active collection for new clips
            await collectionService.SetActiveAsync(inboxCollection.Id, defaultDatabaseKey, cancellationToken);

            _logger.LogInformation("Set Inbox collection (ID: \"{CollectionId}\") as active for new clips", inboxCollection.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize default data and active collection");

            throw;
        }
    }
}
