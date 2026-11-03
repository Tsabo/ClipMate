using ClipMate.Core.Models.Configuration;

namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// Service for managing application configuration.
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Raised after the configuration has been saved to disk (by any of the save methods).
    /// </summary>
    event EventHandler? Saved;

    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    ClipMateConfiguration Configuration { get; }

    /// <summary>
    /// Loads configuration from disk. Creates default configuration if file doesn't exist.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded configuration.</returns>
    Task<ClipMateConfiguration> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the current configuration to disk.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the provided configuration to disk and updates current configuration.
    /// </summary>
    /// <param name="configuration">The configuration to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(ClipMateConfiguration configuration, CancellationToken cancellationToken = default);
}
