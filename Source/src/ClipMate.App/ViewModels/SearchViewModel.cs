using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClipMate.Core.Models;
using ClipMate.Core.Services;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the search panel with advanced filtering.
/// </summary>
public partial class SearchViewModel : ObservableObject
{
    private readonly ISearchService _searchService;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private int _totalMatches;

    [ObservableProperty]
    private bool _filterByText = true;

    [ObservableProperty]
    private bool _filterByImage = true;

    [ObservableProperty]
    private bool _filterByFiles = true;

    [ObservableProperty]
    private DateTime? _dateFrom;

    [ObservableProperty]
    private DateTime? _dateTo;

    [ObservableProperty]
    private bool _isCaseSensitive;

    [ObservableProperty]
    private bool _isRegex;

    [ObservableProperty]
    private Core.Services.SearchScope _searchScope = Core.Services.SearchScope.AllCollections;

    /// <summary>
    /// Collection of search results.
    /// </summary>
    public ObservableCollection<Clip> SearchResults { get; } = new();

    /// <summary>
    /// Collection of recent search queries.
    /// </summary>
    public ObservableCollection<string> SearchHistory { get; } = new();

    public SearchViewModel(ISearchService searchService)
    {
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
    }

    /// <summary>
    /// Executes the search with current filters.
    /// </summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            SearchResults.Clear();
            TotalMatches = 0;
            return;
        }

        try
        {
            IsSearching = true;

            var filters = BuildSearchFilters();
            var results = await _searchService.SearchAsync(SearchText, filters);

            SearchResults.Clear();
            foreach (var clip in results.Clips)
            {
                SearchResults.Add(clip);
            }

            TotalMatches = results.TotalMatches;
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
        return Task.CompletedTask;
    }

    /// <summary>
    /// Loads recent search history.
    /// </summary>
    [RelayCommand]
    private async Task LoadSearchHistoryAsync()
    {
        var history = await _searchService.GetSearchHistoryAsync(10);
        
        SearchHistory.Clear();
        foreach (var query in history)
        {
            SearchHistory.Add(query);
        }
    }

    private SearchFilters BuildSearchFilters()
    {
        var contentTypes = new List<ClipType>();
        
        if (FilterByText)
        {
            contentTypes.Add(ClipType.Text);
        }
        if (FilterByImage)
        {
            contentTypes.Add(ClipType.Image);
        }
        if (FilterByFiles)
        {
            contentTypes.Add(ClipType.Files);
        }

        DateRange? dateRange = null;
        if (DateFrom.HasValue || DateTo.HasValue)
        {
            dateRange = new DateRange(DateFrom, DateTo);
        }

        return new SearchFilters
        {
            ContentTypes = contentTypes.Any() ? contentTypes : null,
            DateRange = dateRange,
            Scope = SearchScope,
            CaseSensitive = IsCaseSensitive,
            IsRegex = IsRegex
        };
    }
}
