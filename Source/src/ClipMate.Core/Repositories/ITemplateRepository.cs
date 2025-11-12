using ClipMate.Core.Models;

namespace ClipMate.Core.Repositories;

/// <summary>
/// Repository interface for managing Template entities in the data store.
/// </summary>
public interface ITemplateRepository
{
    /// <summary>
    /// Retrieves a template by its unique identifier.
    /// </summary>
    /// <param name="id">The template's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The template if found; otherwise, null.</returns>
    Task<Template?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all templates, ordered by sort order.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all templates.</returns>
    Task<IReadOnlyList<Template>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves templates for a specific collection.
    /// </summary>
    /// <param name="collectionId">The collection's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of templates in the collection.</returns>
    Task<IReadOnlyList<Template>> GetByCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new template in the data store.
    /// </summary>
    /// <param name="template">The template to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created template with generated ID.</returns>
    Task<Template> CreateAsync(Template template, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing template in the data store.
    /// </summary>
    /// <param name="template">The template to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if updated successfully; otherwise, false.</returns>
    Task<bool> UpdateAsync(Template template, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a template from the data store.
    /// </summary>
    /// <param name="id">The template's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted successfully; otherwise, false.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
