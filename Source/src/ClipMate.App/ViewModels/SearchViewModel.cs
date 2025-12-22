using System.Collections.ObjectModel;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Services;
using ClipMate.Core.ValueObjects;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the search panel with advanced filtering.
/// Sends SearchResultsChangedEvent via messenger when results change.
/// </summary>
public partial class SearchViewModel : ObservableObject
{
    private readonly IMessenger _messenger;
    private readonly SearchResultsCache _searchResultsCache;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    // Filtering Criteria - Captured After
    [ObservableProperty]
    private bool _capturedAfterEnabled;

    // Filtering Criteria - Captured Before
    [ObservableProperty]
    private bool _capturedBeforeEnabled;

    [ObservableProperty]
    private DateTime? _dateFrom;

    [ObservableProperty]
    private DateTime? _dateTo;

    // Filtering Criteria - Checkboxes
    [ObservableProperty]
    private bool _encryptedOnly;

    [ObservableProperty]
    private bool _filterByFiles = true;

    [ObservableProperty]
    private bool _filterByImage = true;

    [ObservableProperty]
    private bool _filterByText = true;

    // Filtering Criteria - Has Format
    [ObservableProperty]
    private bool _hasFormatEnabled;

    [ObservableProperty]
    private bool _hasShortcutOnly;

    [ObservableProperty]
    private bool _includeDeletedClips;

    [ObservableProperty]
    private bool _isCaseSensitive;

    [ObservableProperty]
    private bool _isRegex;

    [ObservableProperty]
    private bool _isSearching;

    // Filtering Criteria - Member of Collection
    [ObservableProperty]
    private bool _memberOfCollectionEnabled;

    [ObservableProperty]
    private string _searchClipText = string.Empty;

    // Clips Containing - Clip TEXT
    [ObservableProperty]
    private bool _searchClipTextEnabled;

    [ObservableProperty]
    private string _searchCreator = string.Empty;

    // Clips Containing - Creator
    [ObservableProperty]
    private bool _searchCreatorEnabled;

    [ObservableProperty]
    private SearchScope _searchScope = SearchScope.AllCollections;

    [ObservableProperty]
    private string _searchSourceUrl = string.Empty;

    // Clips Containing - Source URL
    [ObservableProperty]
    private bool _searchSourceUrlEnabled;

    [ObservableProperty]
    private string _searchText = string.Empty;

    // Clips Containing - Title
    [ObservableProperty]
    private bool _searchTitleEnabled = true;

    [ObservableProperty]
    private string? _selectedCollection;

    [ObservableProperty]
    private string? _selectedFormat;

    // Saved Queries
    [ObservableProperty]
    private string? _selectedSavedQuery;

    // SQL
    [ObservableProperty]
    private string _sqlQuery = "Select Clip.* from Clip\nWhere Clip.Del = False\nOrder By Clip.ID;";

    [ObservableProperty]
    private int _totalMatches;

    public SearchViewModel(IServiceScopeFactory serviceScopeFactory, IMessenger messenger, SearchResultsCache searchResultsCache)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _searchResultsCache = searchResultsCache ?? throw new ArgumentNullException(nameof(searchResultsCache));

        // Subscribe to property changes to regenerate SQL
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != nameof(SqlQuery) && e.PropertyName != nameof(IsSearching) &&
                e.PropertyName != nameof(TotalMatches) && e.PropertyName != null)
                UpdateSqlQuery();
        };

        // Generate initial SQL
        UpdateSqlQuery();
    }

    /// <summary>
    /// Collection of search results.
    /// </summary>
    public ObservableCollection<Clip> SearchResults { get; } = [];

    /// <summary>
    /// Collection of recent search queries.
    /// </summary>
    public ObservableCollection<string> SearchHistory { get; } = [];

    private void UpdateSqlQuery()
    {
        using var scope = CreateScope();
        var searchService = scope.ServiceProvider.GetRequiredService<ISearchService>();
        var filters = BuildSearchFilters();
        // Pass empty string for query parameter since all search criteria are in filters
        SqlQuery = searchService.BuildSqlQuery("", filters);
    }

    /// <summary>
    /// Helper to create a scope and resolve a scoped service.
    /// </summary>
    private IServiceScope CreateScope() => _serviceScopeFactory.CreateScope();

    /// <summary>
    /// Executes the search with current filters.
    /// </summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        try
        {
            IsSearching = true;

            var filters = BuildSearchFilters();
            SearchResults results;
            string? databaseKey;
            using (var scope = CreateScope())
            {
                var searchService = scope.ServiceProvider.GetRequiredService<ISearchService>();
                var collectionService = scope.ServiceProvider.GetRequiredService<ICollectionService>();

                // Pass empty string for query parameter since all search criteria are in filters
                results = await searchService.SearchAsync("", filters);
                databaseKey = collectionService.GetActiveDatabaseKey();
            }

            SearchResults.Clear();
            foreach (var item in results.Clips)
                SearchResults.Add(item);

            TotalMatches = results.TotalMatches;

            // Use a descriptive query string - check all text search fields
            var queryDescription = !string.IsNullOrWhiteSpace(SearchText) ? SearchText :
                !string.IsNullOrWhiteSpace(SearchClipText) ? SearchClipText :
                !string.IsNullOrWhiteSpace(SearchCreator) ? SearchCreator :
                !string.IsNullOrWhiteSpace(SearchSourceUrl) ? SearchSourceUrl :
                "(filtered search)";

            // Send messenger event with search results
            _messenger.Send(new SearchResultsChangedEvent(queryDescription, SearchResults.ToList()));

            // Update SearchResultsCache and send SearchExecutedEvent if we have a database key
            if (!string.IsNullOrEmpty(databaseKey))
            {
                var clipIds = results.Clips.Select(p => p.Id).ToList();
                var searchResult = new SearchResult(queryDescription, clipIds);
                _searchResultsCache.SetResults(databaseKey, searchResult);

                _messenger.Send(new SearchExecutedEvent(databaseKey, queryDescription, clipIds));
            }
        }
        finally
        {
            IsSearching = false;
        }
    }

    /// <summary>
    /// Clears the search text and results.
    /// </summary>
    [RelayCommand]
    private Task ClearSearchAsync()
    {
        SearchText = string.Empty;
        SearchResults.Clear();
        TotalMatches = 0;

        // Send messenger event indicating search was cleared
        _messenger.Send(new SearchResultsChangedEvent(null, []));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Loads recent search history.
    /// </summary>
    [RelayCommand]
    private async Task LoadSearchHistory()
    {
        IReadOnlyCollection<string> history;
        using (var scope = CreateScope())
        {
            var searchService = scope.ServiceProvider.GetRequiredService<ISearchService>();
            history = await searchService.GetSearchHistoryAsync();
        }

        SearchHistory.Clear();
        foreach (var item in history)
            SearchHistory.Add(item);
    }

    /// <summary>
    /// Saves the current search query with a name.
    /// </summary>
    [RelayCommand]
    private Task SaveQueryAsync()
    {
        // TODO: Implement save query functionality
        // This would show a dialog to enter a name and save the current search criteria
        return Task.CompletedTask;
    }

    /// <summary>
    /// Renames the selected saved query.
    /// </summary>
    [RelayCommand]
    private Task RenameQueryAsync()
    {
        // TODO: Implement rename query functionality
        // This would show a dialog to enter a new name for the selected query
        return Task.CompletedTask;
    }

    /// <summary>
    /// Deletes the selected saved query.
    /// </summary>
    [RelayCommand]
    private Task DeleteQueryAsync()
    {
        // TODO: Implement delete query functionality
        // This would delete the selected saved query after confirmation
        return Task.CompletedTask;
    }

    private SearchFilters BuildSearchFilters()
    {
        var contentTypes = new List<ClipType>();

        if (FilterByText)
            contentTypes.Add(ClipType.Text);

        if (FilterByImage)
            contentTypes.Add(ClipType.Image);

        if (FilterByFiles)
            contentTypes.Add(ClipType.Files);

        DateRange? dateRange = null;
        if (CapturedAfterEnabled && DateFrom.HasValue || CapturedBeforeEnabled && DateTo.HasValue)
        {
            dateRange = new DateRange(
                CapturedAfterEnabled
                    ? DateFrom
                    : null,
                CapturedBeforeEnabled
                    ? DateTo
                    : null);
        }

        return new SearchFilters
        {
            ContentTypes = contentTypes.Count > 0
                ? contentTypes
                : null,
            DateRange = dateRange,
            Scope = SearchScope,
            CaseSensitive = IsCaseSensitive,
            IsRegex = IsRegex,
            TitleQuery = SearchTitleEnabled && !string.IsNullOrWhiteSpace(SearchText)
                ? SearchText
                : null,
            TextContentQuery = SearchClipTextEnabled && !string.IsNullOrWhiteSpace(SearchClipText)
                ? SearchClipText
                : null,
            CreatorQuery = SearchCreatorEnabled && !string.IsNullOrWhiteSpace(SearchCreator)
                ? SearchCreator
                : null,
            SourceUrlQuery = SearchSourceUrlEnabled && !string.IsNullOrWhiteSpace(SearchSourceUrl)
                ? SearchSourceUrl
                : null,
            Format = HasFormatEnabled && !string.IsNullOrWhiteSpace(SelectedFormat)
                ? SelectedFormat
                : null,
            EncryptedOnly = EncryptedOnly
                ? true
                : null,
            HasShortcutOnly = HasShortcutOnly
                ? true
                : null,
            IncludeDeleted = IncludeDeletedClips,
        };
    }
}
