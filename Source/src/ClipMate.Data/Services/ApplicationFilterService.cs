using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for filtering clipboard captures based on source application.
/// </summary>
public class ApplicationFilterService : IApplicationFilterService
{
    private readonly IApplicationFilterRepository _repository;
    private readonly ILogger<ApplicationFilterService> _logger;

    public ApplicationFilterService(
        IApplicationFilterRepository repository,
        ILogger<ApplicationFilterService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> ShouldFilterAsync(
        string? processName,
        string? windowTitle,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var enabledFilters = await _repository.GetEnabledAsync(cancellationToken);

            foreach (var filter in enabledFilters)
            {
                if (MatchesFilter(filter, processName, windowTitle))
                {
                    _logger.LogDebug(
                        "Clip filtered by rule '{FilterName}': Process={Process}, Title={Title}",
                        filter.Name,
                        processName,
                        windowTitle);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking application filters");
            // Don't block clipboard capture on filter errors
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApplicationFilter>> GetAllFiltersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _repository.GetAllAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all filters");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApplicationFilter>> GetEnabledFiltersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _repository.GetEnabledAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving enabled filters");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ApplicationFilter> CreateFilterAsync(
        string name,
        string? processName,
        string? windowTitlePattern,
        bool isEnabled = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (string.IsNullOrWhiteSpace(processName) && string.IsNullOrWhiteSpace(windowTitlePattern))
        {
            throw new ArgumentException("At least one of processName or windowTitlePattern must be specified");
        }

        try
        {
            var filter = new ApplicationFilter
            {
                Id = Guid.NewGuid(),
                Name = name,
                ProcessName = processName,
                WindowTitlePattern = windowTitlePattern,
                IsEnabled = isEnabled,
                CreatedAt = DateTime.UtcNow
            };

            var created = await _repository.CreateAsync(filter, cancellationToken);
            _logger.LogInformation("Created filter '{FilterName}' (ID: {FilterId})", name, created.Id);

            return created;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating filter '{FilterName}'", name);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task UpdateFilterAsync(ApplicationFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        try
        {
            filter.ModifiedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(filter, cancellationToken);
            _logger.LogInformation("Updated filter '{FilterName}' (ID: {FilterId})", filter.Name, filter.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating filter {FilterId}", filter.Id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteFilterAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _repository.DeleteAsync(id, cancellationToken);
            _logger.LogInformation("Deleted filter {FilterId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting filter {FilterId}", id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SetFilterEnabledAsync(Guid id, bool isEnabled, CancellationToken cancellationToken = default)
    {
        try
        {
            var filter = await _repository.GetByIdAsync(id, cancellationToken);
            if (filter == null)
            {
                throw new InvalidOperationException($"Filter with ID {id} not found");
            }

            filter.IsEnabled = isEnabled;
            filter.ModifiedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(filter, cancellationToken);

            _logger.LogInformation(
                "Filter '{FilterName}' (ID: {FilterId}) {Status}",
                filter.Name,
                id,
                isEnabled ? "enabled" : "disabled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting enabled state for filter {FilterId}", id);
            throw;
        }
    }

    /// <summary>
    /// Determines if a process and window title match a filter.
    /// </summary>
    private bool MatchesFilter(ApplicationFilter filter, string? processName, string? windowTitle)
    {
        var hasProcessFilter = !string.IsNullOrWhiteSpace(filter.ProcessName);
        var hasTitleFilter = !string.IsNullOrWhiteSpace(filter.WindowTitlePattern);

        // If both filters are specified, both must match
        if (hasProcessFilter && hasTitleFilter)
        {
            var processMatches = MatchesPattern(processName, filter.ProcessName);
            var titleMatches = MatchesPattern(windowTitle, filter.WindowTitlePattern);
            return processMatches && titleMatches;
        }

        // If only process filter is specified, check process name
        if (hasProcessFilter)
        {
            return MatchesPattern(processName, filter.ProcessName);
        }

        // If only title filter is specified, check window title
        if (hasTitleFilter)
        {
            return MatchesPattern(windowTitle, filter.WindowTitlePattern);
        }

        // No filters specified (shouldn't happen due to CreateFilterAsync validation)
        return false;
    }

    /// <summary>
    /// Matches a value against a pattern with wildcard support.
    /// </summary>
    private bool MatchesPattern(string? value, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        // Convert wildcard pattern to regex
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return System.Text.RegularExpressions.Regex.IsMatch(
            value,
            regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
