using ClipMate.Core.Models;

namespace ClipMate.Core.Repositories;

/// <summary>
/// Repository interface for managing SoundEvent entities in the data store.
/// </summary>
public interface ISoundEventRepository
{
    /// <summary>
    /// Retrieves a sound event by its unique identifier.
    /// </summary>
    /// <param name="id">The sound event's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sound event if found; otherwise, null.</returns>
    Task<SoundEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a sound event by its event type.
    /// </summary>
    /// <param name="eventType">The event type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sound event if found; otherwise, null.</returns>
    Task<SoundEvent?> GetByEventTypeAsync(SoundEventType eventType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all sound events.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all sound events.</returns>
    Task<IReadOnlyList<SoundEvent>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new sound event in the data store.
    /// </summary>
    /// <param name="soundEvent">The sound event to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created sound event with generated ID.</returns>
    Task<SoundEvent> CreateAsync(SoundEvent soundEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing sound event in the data store.
    /// </summary>
    /// <param name="soundEvent">The sound event to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if updated successfully; otherwise, false.</returns>
    Task<bool> UpdateAsync(SoundEvent soundEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a sound event from the data store.
    /// </summary>
    /// <param name="id">The sound event's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted successfully; otherwise, false.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
