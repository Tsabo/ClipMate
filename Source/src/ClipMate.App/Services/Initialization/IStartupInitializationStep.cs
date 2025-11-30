namespace ClipMate.App.Services.Initialization;

/// <summary>
/// Represents a single step in the application startup initialization pipeline.
/// Steps are executed sequentially in order of their <see cref="Order" /> property.
/// </summary>
public interface IStartupInitializationStep
{
    /// <summary>
    /// Gets the name of this initialization step (used for logging).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the execution order of this step. Lower values execute first.
    /// Recommended ranges:
    /// - 10-19: Database initialization
    /// - 20-29: Configuration loading
    /// - 30-39: Default data seeding
    /// - 40-49: Application state restoration
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Executes this initialization step.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
