using Microsoft.Extensions.Logging;

namespace ClipMate.App.Services.Initialization;

/// <summary>
/// Initialization step that ensures the ClipOperationsCoordinator is instantiated at startup.
/// The coordinator registers for messenger events in its constructor, so it must be created
/// before any clip operation events are sent.
/// </summary>
public class ClipOperationsInitializationStep : IStartupInitializationStep
{
    private readonly ClipOperationsCoordinator _coordinator;
    private readonly ILogger<ClipOperationsInitializationStep> _logger;

    public ClipOperationsInitializationStep(ClipOperationsCoordinator coordinator,
        ILogger<ClipOperationsInitializationStep> logger)
    {
        // Simply injecting the coordinator triggers its construction and event registration
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string Name => "Clip Operations";

    /// <inheritdoc />
    public int Order => 60; // After hotkey registration (50)

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // The coordinator is already initialized via constructor injection
        // This step just ensures it happens during the startup pipeline
        _logger.LogDebug("ClipOperationsCoordinator initialized and ready for events");
        return Task.CompletedTask;
    }
}
