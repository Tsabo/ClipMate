using System.Globalization;
using System.Text.RegularExpressions;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service for searching clips with advanced filtering capabilities.
/// </summary>
public partial class SearchService : ISearchService
{
    private const int _maxHistorySize = 50;

    // SQL validation - prevent dangerous operations
    private static readonly string[] _dangerousKeywords =
    [
        "DROP", "DELETE", "UPDATE", "INSERT", "ALTER", "CREATE", "TRUNCATE", "EXEC", "EXECUTE", "PRAGMA",
    ];

    private readonly ICollectionService _collectionService;
    private readonly IConfigurationService _configurationService;

    private readonly IDatabaseContextFactory _contextFactory;
    private readonly ILogger<SearchService> _logger;
    private readonly List<string> _searchHistory = [];
    private readonly ISqlValidationService _sqlValidationService;

    public SearchService(IDatabaseContextFactory contextFactory,
        ICollectionService collectionService,
        IConfigurationService configurationService,
        ISqlValidationService sqlValidationService,
        ILogger<SearchService> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _sqlValidationService = sqlValidationService ?? throw new ArgumentNullException(nameof(sqlValidationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SearchResults> SearchAsync(string query, SearchFilters? filters = null, CancellationToken cancellationToken = default)
    {
        // Get active database from collection service
        var databaseKey = _collectionService.GetActiveDatabaseKey();
        if (string.IsNullOrEmpty(databaseKey))
            throw new InvalidOperationException("No active database selected");

        var clipRepository = _contextFactory.GetClipRepository(databaseKey);

        // Track search history (non-empty queries only)
        if (!string.IsNullOrWhiteSpace(query))
            AddToSearchHistory(query);

        // Build parameterized SQL from filters; filter values never enter the SQL text
        var sqlQuery = BuildParameterizedSqlQuery(query, filters);

        // Execute SQL query
        var clips = await clipRepository.ExecuteSqlQueryAsync(sqlQuery.Sql, sqlQuery.Parameters, cancellationToken);

        return new SearchResults
        {
            Clips = clips.ToList(),
            TotalMatches = clips.Count,
            Query = query,
        };
    }

    /// <inheritdoc />
    public async Task<SearchMatches> SearchDatabaseAsync(string databaseKey, SearchFilters filters, int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseKey);
        ArgumentNullException.ThrowIfNull(filters);

        // Build parameterized SQL from filters; filter values never enter the SQL text
        var sqlQuery = BuildParameterizedSqlQuery(string.Empty, filters);
        var clipRepository = _contextFactory.GetClipRepository(databaseKey);
        return await clipRepository.ExecuteSearchQueryAsync(sqlQuery.Sql, sqlQuery.Parameters, maxResults, cancellationToken);
    }

    public string BuildSqlQuery(string query, SearchFilters? filters = null)
    {
        var sqlQuery = BuildParameterizedSqlQuery(query, filters);

        // Readable rendering for display: substitute each @Name with its value as an escaped SQL literal.
        // Parameter values never appear in the parameterized SQL text, so only real references are replaced.
        return ParameterReferenceRegex().Replace(sqlQuery.Sql,
            p => sqlQuery.Parameters.TryGetValue(p.Groups[1].Value, out var value)
                ? ToSqlLiteral(value)
                : p.Value);
    }

    public SearchSqlQuery BuildParameterizedSqlQuery(string query, SearchFilters? filters = null)
    {
        var conditions = new List<string>();
        var parameters = new Dictionary<string, object?>();
        var needsClipDataJoin = false;
        var needsBlobTxtJoin = false;
        var needsShortCutJoin = false;

        // Del filter (exclude deleted by default)
        if (filters?.IncludeDeleted != true)
            conditions.Add("Clips.Del = False");

        // Encrypted filter
        if (filters?.EncryptedOnly == true)
            conditions.Add("Clips.Encrypted = True");

        // Has shortcut filter (requires join with ShortCut table)
        if (filters?.HasShortcutOnly == true)
            needsShortCutJoin = true;

        // Scope: restrict to one collection or one folder; AllCollections applies no restriction
        switch (filters?.Scope)
        {
            case SearchScope.CurrentCollection:
                var collectionId = filters.CollectionId
                                   ?? throw new ArgumentException("A collection ID is required when the search scope is the current collection.", nameof(filters));

                conditions.Add("Clips.CollectionId = @CollectionId");
                parameters["CollectionId"] = ToDatabaseGuid(collectionId);
                break;

            case SearchScope.SpecificFolder:
                var folderId = filters.FolderId
                               ?? throw new ArgumentException("A folder ID is required when the search scope is a specific folder.", nameof(filters));

                conditions.Add("Clips.FolderId = @FolderId");
                parameters["FolderId"] = ToDatabaseGuid(folderId);
                break;
        }

        // Content type filter (null or empty = all types)
        var contentTypes = filters?.ContentTypes?.Distinct().ToList();
        if (contentTypes is { Count: > 0 })
        {
            var names = new List<string>();
            for (var i = 0; i < contentTypes.Count; i++)
            {
                var name = $"ContentType{i}";
                names.Add($"@{name}");
                parameters[name] = (int)contentTypes[i];
            }

            conditions.Add($"Clips.Type IN ({string.Join(", ", names)})");
        }

        // Date range filters. CapturedAt is stored as ISO 8601 text, so comparing against a
        // date-only string includes the whole "after" day and excludes the whole "before" day.
        if (filters?.DateRange?.From is { } from)
        {
            conditions.Add("Clips.CapturedAt > @CapturedAfter");
            parameters["CapturedAfter"] = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (filters?.DateRange?.To is { } to)
        {
            conditions.Add("Clips.CapturedAt < @CapturedBefore");
            parameters["CapturedBefore"] = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        // Format filter (requires join with ClipData)
        if (!string.IsNullOrEmpty(filters?.Format))
        {
            needsClipDataJoin = true;
            conditions.Add("ClipData.Format = @Format");
            parameters["Format"] = FormatToFormatId(filters.Format);
        }

        // Text search conditions using TextSearch custom function
        if (!string.IsNullOrEmpty(filters?.TitleQuery))
        {
            conditions.Add("TextSearch(Clips.TITLE, @TitleQuery) = 1");
            parameters["TitleQuery"] = filters.TitleQuery;
        }

        if (!string.IsNullOrEmpty(filters?.TextContentQuery))
        {
            needsClipDataJoin = true;
            needsBlobTxtJoin = true;
            conditions.Add("TextSearch(BlobTxt.Data, @TextContentQuery) = 1");
            parameters["TextContentQuery"] = filters.TextContentQuery;
        }

        if (!string.IsNullOrEmpty(filters?.CreatorQuery))
        {
            conditions.Add("TextSearch(Clips.CREATOR, @CreatorQuery) = 1");
            parameters["CreatorQuery"] = filters.CreatorQuery;
        }

        if (!string.IsNullOrEmpty(filters?.SourceUrlQuery))
        {
            conditions.Add("TextSearch(Clips.SOURCEURL, @SourceUrlQuery) = 1");
            parameters["SourceUrlQuery"] = filters.SourceUrlQuery;
        }

        // Legacy query parameter (searches in text content via BlobTxt)
        if (!string.IsNullOrWhiteSpace(query))
        {
            needsClipDataJoin = true;
            needsBlobTxtJoin = true;
            conditions.Add("TextSearch(BlobTxt.Data, @Query) = 1");
            parameters["Query"] = query;
        }

        // Build FROM clause with necessary joins
        var fromClause = "Clips";
        var joinConditions = new List<string>();

        if (needsClipDataJoin)
        {
            fromClause += ", ClipData";
            joinConditions.Add("ClipData.ClipId = Clips.ID");
        }

        if (needsBlobTxtJoin)
        {
            fromClause += ", BlobTxt";
            joinConditions.Add("BlobTxt.ClipDataId = ClipData.ID");
        }

        if (needsShortCutJoin)
        {
            fromClause += ", ShortCut";
            joinConditions.Add("ShortCut.ClipId = Clips.ID");
        }

        // Add join conditions first, then other conditions
        var allConditions = new List<string>();
        allConditions.AddRange(joinConditions);
        allConditions.AddRange(conditions);

        // Build complete SQL query
        var whereClause = allConditions.Count > 0
            ? $"Where {string.Join("\n  And ", allConditions)}"
            : string.Empty;

        var sql = $"Select DISTINCT Clips.* from {fromClause}\n{whereClause}\nOrder By Clips.ID;";
        return new SearchSqlQuery(sql, parameters);
    }

    public async Task SaveSearchQueryAsync(string name, string query, bool isCaseSensitive, bool isRegex, string? filtersJson = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var savedQuery = new SavedSearchQuery
        {
            Name = name,
            Query = query,
            IsCaseSensitive = isCaseSensitive,
            IsRegex = isRegex,
            FiltersJson = filtersJson,
        };

        var config = _configurationService.Configuration;

        // Remove existing query with same name if it exists
        config.SavedSearchQueries.RemoveAll(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        // Add new query
        config.SavedSearchQueries.Add(savedQuery);

        await _configurationService.SaveAsync(cancellationToken);
    }

    public Task<IReadOnlyList<SavedSearchQuery>> GetSavedQueriesAsync(CancellationToken cancellationToken = default)
    {
        var queries = _configurationService.Configuration.SavedSearchQueries
            .OrderBy(p => p.Name)
            .ToList()
            .AsReadOnly();

        return Task.FromResult<IReadOnlyList<SavedSearchQuery>>(queries);
    }

    public async Task RenameSearchQueryAsync(string oldName, string newName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);

        var config = _configurationService.Configuration;
        var query = config.SavedSearchQueries.FirstOrDefault(p => p.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase));

        if (query == null)
            throw new InvalidOperationException($"Search query '{oldName}' not found");

        query.Name = newName;
        await _configurationService.SaveAsync(cancellationToken);
    }

    public async Task DeleteSearchQueryAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var config = _configurationService.Configuration;
        var removed = config.SavedSearchQueries.RemoveAll(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (removed == 0)
            throw new InvalidOperationException($"Search query '{name}' not found");

        await _configurationService.SaveAsync(cancellationToken);
    }

    public Task<IReadOnlyList<string>> GetSearchHistoryAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        var history = _searchHistory
            .Take(count)
            .ToList()
            .AsReadOnly();

        return Task.FromResult<IReadOnlyList<string>>(history);
    }

    /// <summary>
    /// Validates a SQL query for security and syntax.
    /// Strategy 1: Read-only validation (dangerous keywords)
    /// Strategy 2: SELECT-only enforcement
    /// Strategy 4: EXPLAIN QUERY PLAN pre-execution check
    /// </summary>
    /// <param name="sql">The SQL query to validate.</param>
    /// <param name="databaseKey">The database key to validate against.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<(bool IsValid, string? ErrorMessage)> ValidateSqlQueryAsync(string sql,
        string databaseKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return (false, "SQL query cannot be empty");

        var trimmedSql = sql.Trim();
        var upperSql = trimmedSql.ToUpperInvariant();

        // Strategy 1 & 2: Must start with SELECT
        if (!upperSql.StartsWith("SELECT"))
            return (false, "Only SELECT queries are allowed for searching");

        // Strategy 1: Check for dangerous keywords that could modify data
        foreach (var item in _dangerousKeywords)
        {
            // Use word boundaries to avoid false positives (e.g., "DELETED" column name is ok)
            if (Regex.IsMatch(upperSql, $@"\b{item}\b"))
                return (false, $"Keyword '{item}' is not allowed in search queries");
        }

        // Strategy 4: Delegate to SqlValidationService for low-level validation
        var (isValid, error) = await _sqlValidationService.ValidateSqlQueryAsync(trimmedSql, databaseKey, cancellationToken);
        return (isValid, error);
    }

    /// <summary>
    /// Renders a parameter value as a SQL literal for the readable (display-only) query.
    /// </summary>
    private static string ToSqlLiteral(object? value) => value switch
    {
        null => "NULL",
        string s => $"'{s.Replace("'", "''")}'",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        var _ => $"'{value.ToString()?.Replace("'", "''")}'",
    };

    /// <summary>
    /// Formats a GUID the way EF Core stores it in SQLite (upper-case text), so it compares equal.
    /// </summary>
    private static string ToDatabaseGuid(Guid id) => id.ToString("D").ToUpperInvariant();

    [GeneratedRegex(@"@(\w+)")]
    private static partial Regex ParameterReferenceRegex();

    private int FormatToFormatId(string format)
    {
        // Map format names to ClipMate format IDs
        // These are from the ClipMate 7.5 schema
        return format.ToLowerInvariant() switch
        {
            "text" => 1,
            "richtext" => 2,
            "html" => 3,
            "image" => 13,
            "files" => 15,
            var _ => 1, // Default to text
        };
    }

    private void AddToSearchHistory(string query)
    {
        // Remove if already exists
        _searchHistory.Remove(query);

        // Add to front
        _searchHistory.Insert(0, query);

        // Trim to max size
        if (_searchHistory.Count > _maxHistorySize)
            _searchHistory.RemoveAt(_searchHistory.Count - 1);
    }
}
