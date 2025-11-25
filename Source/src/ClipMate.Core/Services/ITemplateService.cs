using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for managing text templates with variable substitution.
/// </summary>
public interface ITemplateService
{
    /// <summary>
    /// Gets a template by ID.
    /// </summary>
    /// <param name="id">The template ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The template if found; otherwise, null.</returns>
    Task<Template?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all templates.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all templates.</returns>
    Task<IReadOnlyList<Template>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets templates in a collection.
    /// </summary>
    /// <param name="collectionId">The collection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of templates in the collection.</returns>
    Task<IReadOnlyList<Template>> GetByCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new template.
    /// </summary>
    /// <param name="name">Template name.</param>
    /// <param name="content">Template content with {variable} placeholders.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="collectionId">Optional collection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created template.</returns>
    Task<Template> CreateAsync(string name, string content, string? description = null, Guid? collectionId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing template.
    /// </summary>
    /// <param name="template">The template to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateAsync(Template template, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a template.
    /// </summary>
    /// <param name="id">The template ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Expands a template by substituting variables with provided values.
    /// </summary>
    /// <param name="templateId">The template ID.</param>
    /// <param name="variables">Dictionary of variable names to values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The expanded template content.</returns>
    Task<string> ExpandTemplateAsync(Guid templateId, Dictionary<string, string> variables, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts variable names from a template content string.
    /// </summary>
    /// <param name="content">The template content.</param>
    /// <returns>List of variable names found in {braces}.</returns>
    IReadOnlyList<string> ExtractVariables(string content);
}
