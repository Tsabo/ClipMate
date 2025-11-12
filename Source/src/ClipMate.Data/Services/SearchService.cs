using System.Text.RegularExpressions;
using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for searching clips with advanced filtering capabilities.
/// </summary>
public class SearchService : ISearchService
{
    private readonly IClipRepository _clipRepository;
    private readonly ISearchQueryRepository _searchQueryRepository;
    private readonly List<string> _searchHistory = new();
    private const int MaxHistorySize = 50;

    public SearchService(IClipRepository clipRepository, ISearchQueryRepository searchQueryRepository)
    {
        _clipRepository = clipRepository ?? throw new ArgumentNullException(nameof(clipRepository));
        _searchQueryRepository = searchQueryRepository ?? throw new ArgumentNullException(nameof(searchQueryRepository));
    }

    public async Task<SearchResults> SearchAsync(string query, SearchFilters? filters = null, CancellationToken cancellationToken = default)
    {
        // Track search history (non-empty queries only)
        if (!string.IsNullOrWhiteSpace(query))
        {
            AddToSearchHistory(query);
        }

        // Get all clips (or filtered by scope)
        var allClips = await GetClipsInScopeAsync(filters, cancellationToken);

        // Apply filters
        var filteredClips = ApplyFilters(allClips, query, filters);

        return new SearchResults
        {
            Clips = filteredClips.ToList(),
            TotalMatches = filteredClips.Count(),
            Query = query
        };
    }

    public async Task<SearchResults> ExecuteSavedSearchAsync(Guid searchQueryId, CancellationToken cancellationToken = default)
    {
        var searchQuery = await _searchQueryRepository.GetByIdAsync(searchQueryId, cancellationToken);
        if (searchQuery == null)
        {
            throw new ArgumentException($"Search query {searchQueryId} not found", nameof(searchQueryId));
        }

        var filters = new SearchFilters
        {
            CaseSensitive = searchQuery.IsCaseSensitive,
            IsRegex = searchQuery.IsRegex
        };

        return await SearchAsync(searchQuery.QueryText, filters, cancellationToken);
    }

    public async Task<SearchQuery> SaveSearchQueryAsync(string name, string query, bool isCaseSensitive, bool isRegex, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var searchQuery = new SearchQuery
        {
            Id = Guid.NewGuid(),
            Name = name,
            QueryText = query,
            IsCaseSensitive = isCaseSensitive,
            IsRegex = isRegex,
            CreatedAt = DateTime.UtcNow
        };

        return await _searchQueryRepository.CreateAsync(searchQuery, cancellationToken);
    }

    public async Task DeleteSearchQueryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _searchQueryRepository.DeleteAsync(id, cancellationToken);
    }

    public Task<IReadOnlyList<string>> GetSearchHistoryAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        var history = _searchHistory
            .Take(count)
            .ToList()
            .AsReadOnly();

        return Task.FromResult<IReadOnlyList<string>>(history);
    }

    private async Task<IEnumerable<Clip>> GetClipsInScopeAsync(SearchFilters? filters, CancellationToken cancellationToken)
    {
        // Use GetRecentAsync to get all clips (with a large limit)
        // TODO: Implement collection/folder scoping when those services are fully integrated
        return await _clipRepository.GetRecentAsync(10000, cancellationToken);
    }

    private IEnumerable<Clip> ApplyFilters(IEnumerable<Clip> clips, string query, SearchFilters? filters)
    {
        var result = clips;

        // Apply content type filter
        if (filters?.ContentTypes != null && filters.ContentTypes.Any())
        {
            var contentTypes = filters.ContentTypes.ToHashSet();
            result = result.Where(c => contentTypes.Contains(c.Type));
        }

        // Apply date range filter
        if (filters?.DateRange != null)
        {
            if (filters.DateRange.From.HasValue)
            {
                result = result.Where(c => c.CapturedAt >= filters.DateRange.From.Value);
            }
            if (filters.DateRange.To.HasValue)
            {
                result = result.Where(c => c.CapturedAt <= filters.DateRange.To.Value);
            }
        }

        // Apply text search
        if (!string.IsNullOrWhiteSpace(query))
        {
            result = ApplyTextSearch(result, query, filters);
        }

        return result;
    }

    private IEnumerable<Clip> ApplyTextSearch(IEnumerable<Clip> clips, string query, SearchFilters? filters)
    {
        if (filters?.IsRegex == true)
        {
            try
            {
                var regex = new Regex(query, filters.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
                return clips.Where(c => regex.IsMatch(c.TextContent ?? string.Empty));
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException("Invalid regex pattern", nameof(query), ex);
            }
        }

        // Standard text search
        var comparison = filters?.CaseSensitive == true
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        return clips.Where(c => 
            (c.TextContent?.Contains(query, comparison) == true));
    }

    private void AddToSearchHistory(string query)
    {
        // Remove if already exists
        _searchHistory.Remove(query);
        
        // Add to front
        _searchHistory.Insert(0, query);

        // Trim to max size
        if (_searchHistory.Count > MaxHistorySize)
        {
            _searchHistory.RemoveAt(_searchHistory.Count - 1);
        }
    }
}
