using System.Text.RegularExpressions;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Platform;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for filtering clipboard captures based on source application.
/// </summary>
public class ApplicationFilterService : IApplicationFilterService
{
    private readonly ICollectionService _collectionService;
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly ILogger<ApplicationFilterService> _logger;
    private readonly ISoundService _soundService;

    public ApplicationFilterService(IDatabaseContextFactory contextFactory,
        ICollectionService collectionService,
        ISoundService soundService,
        ILogger<ApplicationFilterService> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _soundService = soundService ?? throw new ArgumentNullException(nameof(soundService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> ShouldFilterAsync(string? processName,
        string? windowTitle,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var databaseKey = _collectionService.GetActiveDatabaseKey();
            if (string.IsNullOrEmpty(databaseKey))
                return false; // No active database, don't filter

            var repository = _contextFactory.GetApplicationFilterRepository(databaseKey);
            var enabledFilters = await repository.GetEnabledAsync(cancellationToken);

            foreach (var item in enabledFilters)
            {
                if (!MatchesFilter(item, processName, windowTitle))
                    continue;

                _logger.LogDebug(
                    "Clip filtered by rule '{FilterName}': Process={Process}, Title={Title}",
                    item.Name,
                    processName,
                    windowTitle);

                // Play filter sound when rejecting clipboard capture
                await _soundService.PlaySoundAsync(SoundEvent.Filter, cancellationToken);

                return true;
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
            var databaseKey = _collectionService.GetActiveDatabaseKey();
            if (string.IsNullOrEmpty(databaseKey))
                return Array.Empty<ApplicationFilter>();

            var repository = _contextFactory.GetApplicationFilterRepository(databaseKey);
            return await repository.GetAllAsync(cancellationToken);
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
            var databaseKey = _collectionService.GetActiveDatabaseKey();
            if (string.IsNullOrEmpty(databaseKey))
                return Array.Empty<ApplicationFilter>();

            var repository = _contextFactory.GetApplicationFilterRepository(databaseKey);
            return await repository.GetEnabledAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving enabled filters");

            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ApplicationFilter> CreateFilterAsync(string name,
        string? processName,
        string? windowTitlePattern,
        bool isEnabled = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (string.IsNullOrWhiteSpace(processName) && string.IsNullOrWhiteSpace(windowTitlePattern))
            throw new ArgumentException("At least one of processName or windowTitlePattern must be specified");

        var databaseKey = _collectionService.GetActiveDatabaseKey();
        if (string.IsNullOrEmpty(databaseKey))
            throw new InvalidOperationException("No active database selected");

        try
        {
            var repository = _contextFactory.GetApplicationFilterRepository(databaseKey);
            var filter = new ApplicationFilter
            {
                Id = Guid.NewGuid(),
                Name = name,
                ProcessName = processName,
                WindowTitlePattern = windowTitlePattern,
                IsEnabled = isEnabled,
                CreatedAt = DateTime.UtcNow,
            };

            var created = await repository.CreateAsync(filter, cancellationToken);
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

        var databaseKey = _collectionService.GetActiveDatabaseKey();
        if (string.IsNullOrEmpty(databaseKey))
            throw new InvalidOperationException("No active database selected");

        try
        {
            var repository = _contextFactory.GetApplicationFilterRepository(databaseKey);
            filter.ModifiedAt = DateTime.UtcNow;
            await repository.UpdateAsync(filter, cancellationToken);
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
        var databaseKey = _collectionService.GetActiveDatabaseKey();
        if (string.IsNullOrEmpty(databaseKey))
            throw new InvalidOperationException("No active database selected");

        try
        {
            var repository = _contextFactory.GetApplicationFilterRepository(databaseKey);
            await repository.DeleteAsync(id, cancellationToken);
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
        var databaseKey = _collectionService.GetActiveDatabaseKey();
        if (string.IsNullOrEmpty(databaseKey))
            throw new InvalidOperationException("No active database selected");

        try
        {
            var repository = _contextFactory.GetApplicationFilterRepository(databaseKey);
            var filter = await repository.GetByIdAsync(id, cancellationToken);

            if (filter == null)
                throw new InvalidOperationException($"Filter with ID {id} not found");

            filter.IsEnabled = isEnabled;
            filter.ModifiedAt = DateTime.UtcNow;
            await repository.UpdateAsync(filter, cancellationToken);

            _logger.LogInformation(
                "Filter '{FilterName}' (ID: {FilterId}) {Status}",
                filter.Name,
                id,
                isEnabled
                    ? "enabled"
                    : "disabled");
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
    private static bool MatchesFilter(ApplicationFilter filter, string? processName, string? windowTitle)
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
            return MatchesPattern(processName, filter.ProcessName);

        // If only title filter is specified, check window title
        if (hasTitleFilter)
            return MatchesPattern(windowTitle, filter.WindowTitlePattern);

        // No filters specified (shouldn't happen due to CreateFilterAsync validation)
        return false;
    }

    /// <summary>
    /// Matches a value against a pattern with wildcard support.
    /// </summary>
    private static bool MatchesPattern(string? value, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(pattern))
            return false;

        // Convert wildcard pattern to regex
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return Regex.IsMatch(
            value,
            regexPattern,
            RegexOptions.IgnoreCase);
    }
}
