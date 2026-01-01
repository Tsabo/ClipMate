using Microsoft.Extensions.Logging;

namespace ClipMate.App.Services.Initialization;

/// <summary>
/// Initialization step that ensures the ClipOperationsCoordinator and CollectionOperationsCoordinator
/// are instantiated at startup. The coordinators register for messenger events in their constructors,
/// so they must be created before any operation events are sent.
/// </summary>
public class ClipOperationsInitializationStep : IStartupInitializationStep
{
    // ReSharper disable once NotAccessedField.Local
    private readonly ClipOperationsCoordinator _clipCoordinator;
    // ReSharper disable once NotAccessedField.Local
    private readonly CollectionOperationsCoordinator _collectionCoordinator;
    private readonly ILogger<ClipOperationsInitializationStep> _logger;

    public ClipOperationsInitializationStep(ClipOperationsCoordinator clipCoordinator,
        CollectionOperationsCoordinator collectionCoordinator,
        ILogger<ClipOperationsInitializationStep> logger)
    {
        // Simply injecting the coordinators triggers their construction and event registration
        _clipCoordinator = clipCoordinator ?? throw new ArgumentNullException(nameof(clipCoordinator));
        _collectionCoordinator = collectionCoordinator ?? throw new ArgumentNullException(nameof(collectionCoordinator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string Name => "Clip Operations";

    /// <inheritdoc />
    public int Order => 60; // After hotkey registration (50)

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // The coordinators are already initialized via constructor injection
        // This step just ensures it happens during the startup pipeline
        _logger.LogDebug("ClipOperationsCoordinator and CollectionOperationsCoordinator initialized and ready for events");
        return Task.CompletedTask;
    }
}
