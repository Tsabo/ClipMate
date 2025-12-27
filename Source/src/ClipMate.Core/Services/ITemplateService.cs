using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for managing file-based templates.
/// Templates are plain text files stored in the Templates directory.
/// </summary>
public interface ITemplateService
{
    /// <summary>
    /// Gets the currently active template, or null if no template is active.
    /// </summary>
    FileTemplate? ActiveTemplate { get; }

    /// <summary>
    /// Gets all available templates from the Templates directory.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of file templates ordered by name.</returns>
    Task<IReadOnlyList<FileTemplate>> GetAllTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific template by name.
    /// </summary>
    /// <param name="name">Template name (without .txt extension).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The template if found; otherwise, null.</returns>
    Task<FileTemplate?> GetTemplateByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the template list by rescanning the Templates directory.
    /// Useful after user adds/removes/modifies templates externally.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RefreshTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Merges a clip with a template, replacing all #TAG# placeholders.
    /// </summary>
    /// <param name="template">The template to use.</param>
    /// <param name="clip">The clip containing data to merge.</param>
    /// <param name="sequenceNumber">Current sequence number for #SEQUENCE# tag.</param>
    /// <returns>The merged text with all tags replaced.</returns>
    string MergeClipWithTemplate(FileTemplate template, Clip clip, int sequenceNumber);

    /// <summary>
    /// Replaces template tags in any text with clip data.
    /// Useful for macros that contain #TAG# placeholders.
    /// </summary>
    /// <param name="text">The text containing #TAG# placeholders.</param>
    /// <param name="clip">The clip containing data to merge.</param>
    /// <param name="sequenceNumber">Current sequence number for #SEQUENCE# tag.</param>
    /// <returns>The text with all tags replaced.</returns>
    string ReplaceTagsInText(string text, Clip clip, int sequenceNumber);

    /// <summary>
    /// Gets the templates directory path.
    /// </summary>
    string GetTemplatesDirectory();

    /// <summary>
    /// Opens the Templates directory in Windows Explorer.
    /// </summary>
    void OpenTemplatesDirectory();

    /// <summary>
    /// Sets the active template by name.
    /// </summary>
    /// <param name="templateName">Template name, or null to clear active template.</param>
    Task SetActiveTemplateAsync(string? templateName);

    /// <summary>
    /// Resets the template sequence counter to 1.
    /// </summary>
    void ResetSequenceCounter();

    /// <summary>
    /// Attempts to apply the active template to a clip.
    /// Returns a new clip with merged template content if template is active and clip is text type.
    /// Returns null if no template is active or clip type is not compatible.
    /// </summary>
    /// <param name="clip">The clip to potentially transform.</param>
    /// <returns>Transformed clip, or null if no transformation applied.</returns>
    Clip? TryApplyTemplate(Clip clip);
}
