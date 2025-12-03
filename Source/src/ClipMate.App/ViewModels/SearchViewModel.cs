using System.Collections.ObjectModel;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Services;
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
    private readonly IServiceScopeFactory _serviceScopeFactory;

    [ObservableProperty]
    private DateTime? _dateFrom;

    [ObservableProperty]
    private DateTime? _dateTo;

    [ObservableProperty]
    private bool _filterByFiles = true;

    [ObservableProperty]
    private bool _filterByImage = true;

    [ObservableProperty]
    private bool _filterByText = true;

    [ObservableProperty]
    private bool _isCaseSensitive;

    [ObservableProperty]
    private bool _isRegex;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private SearchScope _searchScope = SearchScope.AllCollections;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _totalMatches;

    public SearchViewModel(IServiceScopeFactory serviceScopeFactory, IMessenger messenger)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
    }

    /// <summary>
    /// Collection of search results.
    /// </summary>
    public ObservableCollection<Clip> SearchResults { get; } = [];

    /// <summary>
    /// Collection of recent search queries.
    /// </summary>
    public ObservableCollection<string> SearchHistory { get; } = [];

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
            SearchResults results;
            using (var scope = CreateScope())
            {
                var searchService = scope.ServiceProvider.GetRequiredService<ISearchService>();
                results = await searchService.SearchAsync(SearchText, filters);
            }

            SearchResults.Clear();
            foreach (var item in results.Clips)
                SearchResults.Add(item);

            TotalMatches = results.TotalMatches;

            // Send messenger event with search results
            _messenger.Send(new SearchResultsChangedEvent(SearchText, SearchResults.ToList()));
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
        foreach (var query in history)
            SearchHistory.Add(query);
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
        if (DateFrom.HasValue || DateTo.HasValue)
            dateRange = new DateRange(DateFrom, DateTo);

        return new SearchFilters
        {
            ContentTypes = contentTypes.Any()
                ? contentTypes
                : null,
            DateRange = dateRange,
            Scope = SearchScope,
            CaseSensitive = IsCaseSensitive,
            IsRegex = IsRegex
        };
    }
}
