using ClipMate.Data.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.Services.Initialization;

/// <summary>
/// Initialization step that ensures default collections exist and sets the active collection.
/// This MUST run before clipboard monitoring starts.
/// </summary>
public class DefaultDataInitializationStep : IStartupInitializationStep
{
    private readonly DefaultDataInitializationService _defaultDataService;
    private readonly ILogger<DefaultDataInitializationStep> _logger;

    public DefaultDataInitializationStep(DefaultDataInitializationService defaultDataService,
        ILogger<DefaultDataInitializationStep> logger)
    {
        _defaultDataService = defaultDataService ?? throw new ArgumentNullException(nameof(defaultDataService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "Default Data";

    public int Order => 30;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Initializing default data (Inbox collection)");

        // Note: Default data seeding now happens in DatabaseSchemaInitializationStep
        // This step just ensures Inbox is set as the active collection
        await _defaultDataService.InitializeAsync(cancellationToken);

        _logger.LogDebug("Default data initialized (Inbox collection set as active)");
    }
}
