using ClipMate.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service responsible for ensuring default collections and folders exist,
/// and setting the active collection/folder to Inbox on startup.
/// This must run BEFORE clipboard monitoring starts.
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
    /// Ensures default collections exist and sets Inbox as the active collection.
    /// Should be called during application startup, before clipboard monitoring begins.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing default data and active collection");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var collectionService = scope.ServiceProvider.GetRequiredService<ICollectionService>();
            var configurationService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();

            // Get all collections
            var collections = await collectionService.GetAllAsync(cancellationToken);

            // Find Inbox collection (default collection for new clips)
            var inboxCollection = collections.FirstOrDefault(p =>
                p.Name.Equals("Inbox", StringComparison.OrdinalIgnoreCase));

            if (inboxCollection == null)
            {
                // Inbox doesn't exist - create it
                _logger.LogWarning("Inbox collection not found, creating default Inbox collection");
                inboxCollection = await collectionService.CreateAsync(
                    "Inbox",
                    "Default collection for clipboard captures",
                    cancellationToken);
            }

            // Set Inbox as the active collection for new clips
            // Set Inbox as the active collection
            var configuration = configurationService.Configuration;
            var defaultDatabaseKey = configuration.DefaultDatabase;
            if (string.IsNullOrEmpty(defaultDatabaseKey))
                _logger.LogWarning("No default database configured, cannot set active collection");
            else
                await collectionService.SetActiveAsync(inboxCollection.Id, defaultDatabaseKey, cancellationToken);

            _logger.LogInformation("Set Inbox collection (ID: {CollectionId}) as active for new clips", inboxCollection.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize default data and active collection");

            throw;
        }
    }
}
