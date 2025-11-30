using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.Services.Initialization;

/// <summary>
/// Initialization step that loads application configuration from disk.
/// </summary>
public class ConfigurationLoadingStep : IStartupInitializationStep
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<ConfigurationLoadingStep> _logger;

    public ConfigurationLoadingStep(IConfigurationService configurationService,
        ILogger<ConfigurationLoadingStep> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "Configuration Loading";

    public int Order => 20;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading application configuration from disk");

        await _configurationService.LoadAsync(cancellationToken);

        _logger.LogDebug("Application configuration loaded successfully");
    }
}
