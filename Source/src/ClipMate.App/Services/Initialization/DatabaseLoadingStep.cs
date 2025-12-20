using ClipMate.Data.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.Services.Initialization;

/// <summary>
/// Initialization step that loads all auto-load databases from configuration.
/// This must run AFTER DatabaseSchemaInitializationStep and BEFORE DefaultDataInitializationStep.
/// </summary>
public class DatabaseLoadingStep : IStartupInitializationStep
{
    private readonly ILogger<DatabaseLoadingStep> _logger;
    private readonly IServiceProvider _serviceProvider;

    public DatabaseLoadingStep(IServiceProvider serviceProvider,
        ILogger<DatabaseLoadingStep> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "Database Loading";

    // Order 25: After DatabaseSchema (20) and before DefaultData (30)
    public int Order => 25;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== Database Loading Started ===");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var databaseManager = scope.ServiceProvider.GetRequiredService<IDatabaseManager>();

            // Load all databases configured with AutoLoad=true
            var loadedCount = await databaseManager.LoadAutoLoadDatabasesAsync(cancellationToken);

            _logger.LogInformation("Loaded {Count} auto-load database(s)", loadedCount);

            if (loadedCount == 0)
                _logger.LogWarning("No databases were loaded from configuration");

            _logger.LogInformation("=== Database Loading Completed ===");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Database loading failed");
            throw;
        }
    }
}
