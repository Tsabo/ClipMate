using ClipMate.Core.ValueObjects;

namespace ClipMate.Core.Services;

/// <summary>
/// Domain service that manages search result caching per database.
/// Stores the most recent search results for each database independently.
/// </summary>
public sealed class SearchResultsCache
{
    private readonly Dictionary<string, SearchResult> _cache = new();

    /// <summary>
    /// Stores search results for a specific database.
    /// Overwrites any previous results for that database.
    /// </summary>
    /// <param name="databaseKey">The database key.</param>
    /// <param name="result">The search result to cache.</param>
    public void SetResults(string databaseKey, SearchResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseKey);
        ArgumentNullException.ThrowIfNull(result);

        _cache[databaseKey] = result;
    }

    /// <summary>
    /// Gets the cached search results for a database.
    /// </summary>
    /// <param name="databaseKey">The database key.</param>
    /// <returns>The cached search result, or null if none exists.</returns>
    public SearchResult? GetResults(string databaseKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseKey);

        return _cache.GetValueOrDefault(databaseKey);
    }

    /// <summary>
    /// Checks if search results exist for a database.
    /// </summary>
    /// <param name="databaseKey">The database key.</param>
    /// <returns>True if results exist, false otherwise.</returns>
    public bool HasResults(string databaseKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseKey);

        return _cache.ContainsKey(databaseKey);
    }

    /// <summary>
    /// Clears the cached search results for a specific database.
    /// </summary>
    /// <param name="databaseKey">The database key.</param>
    public void ClearResults(string databaseKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseKey);

        _cache.Remove(databaseKey);
    }

    /// <summary>
    /// Clears all cached search results for all databases.
    /// </summary>
    public void ClearAll() => _cache.Clear();
}
