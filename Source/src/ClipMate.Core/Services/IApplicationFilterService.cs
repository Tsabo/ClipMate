using ClipMate.Core.Models;

namespace ClipMate.Core.Services;

/// <summary>
/// Service for filtering clipboard captures based on source application.
/// </summary>
public interface IApplicationFilterService
{
    /// <summary>
    /// Checks if a clip should be filtered (excluded) based on active filters.
    /// </summary>
    /// <param name="processName">The process name of the source application.</param>
    /// <param name="windowTitle">The window title of the source application.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the clip should be filtered out; otherwise, false.</returns>
    Task<bool> ShouldFilterAsync(string? processName, string? windowTitle, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all application filters.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of all filters.</returns>
    Task<IReadOnlyList<ApplicationFilter>> GetAllFiltersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all enabled application filters.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of enabled filters.</returns>
    Task<IReadOnlyList<ApplicationFilter>> GetEnabledFiltersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new application filter.
    /// </summary>
    /// <param name="name">Filter name.</param>
    /// <param name="processName">Process name pattern (null to ignore).</param>
    /// <param name="windowTitlePattern">Window title pattern (null to ignore).</param>
    /// <param name="isEnabled">Whether the filter is enabled.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created filter.</returns>
    Task<ApplicationFilter> CreateFilterAsync(string name, string? processName, string? windowTitlePattern, bool isEnabled = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing application filter.
    /// </summary>
    /// <param name="filter">The filter to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateFilterAsync(ApplicationFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an application filter.
    /// </summary>
    /// <param name="id">The filter ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteFilterAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables a filter.
    /// </summary>
    /// <param name="id">The filter ID.</param>
    /// <param name="isEnabled">Whether to enable the filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetFilterEnabledAsync(Guid id, bool isEnabled, CancellationToken cancellationToken = default);
}
