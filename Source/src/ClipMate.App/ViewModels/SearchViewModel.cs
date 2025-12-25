using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Controls;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Services;
using ClipMate.Core.ValueObjects;
using ClipMate.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DevExpress.Xpf.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Application = System.Windows.Application;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the search panel with advanced filtering.
/// Sends SearchResultsChangedEvent via messenger when results change.
/// </summary>
public partial class SearchViewModel : ObservableObject
{
    private readonly ICollectionService _collectionService;
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly IMessenger _messenger;
    private readonly SearchResultsCache _searchResultsCache;
    private readonly ISearchService _searchService;

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
    private SavedSearchQuery? _selectedSavedQuery;

    // SQL
    [ObservableProperty]
    private string _sqlQuery = "Select Clip.* from Clip\nWhere Clip.Del = False\nOrder By Clip.ID;";

    [ObservableProperty]
    private int _totalMatches;

    public SearchViewModel(ISearchService searchService,
        ICollectionService collectionService,
        IDatabaseContextFactory contextFactory,
        IMessenger messenger,
        SearchResultsCache searchResultsCache)
    {
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _searchResultsCache = searchResultsCache ?? throw new ArgumentNullException(nameof(searchResultsCache));

        // Subscribe to property changes to regenerate SQL
        PropertyChanged += (_, e) =>
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
    /// Collection of saved search queries.
    /// </summary>
    public ObservableCollection<SavedSearchQuery> SavedQueries { get; } = [];

    /// <summary>
    /// Collection of available clipboard formats.
    /// </summary>
    public ObservableCollection<string> AvailableFormats { get; } = [];

    /// <summary>
    /// Collection of available collections for filtering.
    /// </summary>
    public ObservableCollection<string> AvailableCollections { get; } = [];

    /// <summary>
    /// Called when SelectedSavedQuery changes. Loads the saved filters into the search form.
    /// </summary>
    partial void OnSelectedSavedQueryChanged(SavedSearchQuery? value)
    {
        if (value == null)
            return;

        // Deserialize filters from JSON
        SearchFilters? filters = null;
        if (!string.IsNullOrWhiteSpace(value.FiltersJson))
        {
            try
            {
                filters = JsonSerializer.Deserialize<SearchFilters>(value.FiltersJson);
            }
            catch
            {
                // If deserialization fails, just use the basic query properties
            }
        }

        // Load basic query properties
        SearchText = value.Query;
        IsCaseSensitive = value.IsCaseSensitive;
        IsRegex = value.IsRegex;

        // Load filters if available
        if (filters == null)
            return;

        // Title query
        if (!string.IsNullOrWhiteSpace(filters.TitleQuery))
        {
            SearchText = filters.TitleQuery;
            SearchTitleEnabled = true;
        }
        else
            SearchTitleEnabled = false;

        // Text content query
        if (!string.IsNullOrWhiteSpace(filters.TextContentQuery))
        {
            SearchClipText = filters.TextContentQuery;
            SearchClipTextEnabled = true;
        }
        else
        {
            SearchClipText = string.Empty;
            SearchClipTextEnabled = false;
        }

        // Creator query
        if (!string.IsNullOrWhiteSpace(filters.CreatorQuery))
        {
            SearchCreator = filters.CreatorQuery;
            SearchCreatorEnabled = true;
        }
        else
        {
            SearchCreator = string.Empty;
            SearchCreatorEnabled = false;
        }

        // Source URL query
        if (!string.IsNullOrWhiteSpace(filters.SourceUrlQuery))
        {
            SearchSourceUrl = filters.SourceUrlQuery;
            SearchSourceUrlEnabled = true;
        }
        else
        {
            SearchSourceUrl = string.Empty;
            SearchSourceUrlEnabled = false;
        }

        // Format
        if (!string.IsNullOrWhiteSpace(filters.Format))
        {
            SelectedFormat = filters.Format;
            HasFormatEnabled = true;
        }
        else
        {
            SelectedFormat = null;
            HasFormatEnabled = false;
        }

        // Date range
        if (filters.DateRange != null)
        {
            if (filters.DateRange.From.HasValue)
            {
                DateFrom = filters.DateRange.From.Value;
                CapturedAfterEnabled = true;
            }
            else
            {
                DateFrom = null;
                CapturedAfterEnabled = false;
            }

            if (filters.DateRange.To.HasValue)
            {
                DateTo = filters.DateRange.To.Value;
                CapturedBeforeEnabled = true;
            }
            else
            {
                DateTo = null;
                CapturedBeforeEnabled = false;
            }
        }
        else
        {
            DateFrom = null;
            DateTo = null;
            CapturedAfterEnabled = false;
            CapturedBeforeEnabled = false;
        }

        // Content types
        if (filters.ContentTypes != null)
        {
            var types = filters.ContentTypes.ToList();
            FilterByText = types.Contains(ClipType.Text);
            FilterByImage = types.Contains(ClipType.Image);
            FilterByFiles = types.Contains(ClipType.Files);
        }
        else
        {
            FilterByText = true;
            FilterByImage = true;
            FilterByFiles = true;
        }

        // Checkboxes
        EncryptedOnly = filters.EncryptedOnly == true;
        HasShortcutOnly = filters.HasShortcutOnly == true;
        IncludeDeletedClips = filters.IncludeDeleted;

        // Search scope and collection
        SearchScope = filters.Scope;
    }

    private void UpdateSqlQuery()
    {
        var filters = BuildSearchFilters();
        // Pass empty string for query parameter since all search criteria are in filters
        SqlQuery = _searchService.BuildSqlQuery("", filters);
    }

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
            // Pass empty string for query parameter since all search criteria are in filters
            SearchResults results = await _searchService.SearchAsync("", filters);
            string? databaseKey = _collectionService.GetActiveDatabaseKey();

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
    /// Loads saved search queries.
    /// </summary>
    [RelayCommand]
    private async Task LoadSavedQueriesAsync()
    {
        try
        {
            var queries = await _searchService.GetSavedQueriesAsync();

            SavedQueries.Clear();
            foreach (var query in queries)
                SavedQueries.Add(query);
        }
        catch (Exception)
        {
            // Database error - just clear the list
            SavedQueries.Clear();
        }
    }

    /// <summary>
    /// Loads available clipboard formats from the database.
    /// </summary>
    [RelayCommand]
    private async Task LoadFormatsAsync()
    {
        try
        {
            var activeDatabaseKey = _collectionService.GetActiveDatabaseKey();

            if (string.IsNullOrEmpty(activeDatabaseKey))
                return;

            var dbContext = _contextFactory.GetOrCreateContext(activeDatabaseKey);

            // Check if database exists and has the ClipData table
            if (!await dbContext.Database.CanConnectAsync())
                return;

            var formats = await dbContext.ClipData
                .Select(p => p.FormatName)
                .Distinct()
                .OrderBy(p => p)
                .ToListAsync();

            AvailableFormats.Clear();
            foreach (var item in formats)
                AvailableFormats.Add(item);
        }
        catch (SqliteException)
        {
            // Database doesn't exist yet or table not created - this is ok, just leave formats empty
            AvailableFormats.Clear();
        }
        catch (Exception)
        {
            // Other errors - also just clear the list
            AvailableFormats.Clear();
        }
    }

    /// <summary>
    /// Loads available collections from the database.
    /// </summary>
    [RelayCommand]
    private async Task LoadCollectionsAsync()
    {
        try
        {
            var collections = await _collectionService.GetAllAsync();

            AvailableCollections.Clear();
            foreach (var item in collections.Where(p => !p.IsVirtual).OrderBy(p => p.Title))
                AvailableCollections.Add(item.Title);
        }
        catch (Exception)
        {
            // Database error - just clear the list
            AvailableCollections.Clear();
        }
    }

    /// <summary>
    /// Saves the current search query with a name.
    /// </summary>
    [RelayCommand]
    private async Task SaveQueryAsync()
    {
        // Show input dialog to get query name
        var name = await ShowInputDialogAsync("Save Search Query", "Enter query name:");
        if (string.IsNullOrWhiteSpace(name))
            return;

        // Generate a descriptive query from current filters
        var queryDescription = !string.IsNullOrWhiteSpace(SearchText) ? SearchText :
            !string.IsNullOrWhiteSpace(SearchClipText) ? SearchClipText :
            !string.IsNullOrWhiteSpace(SearchCreator) ? SearchCreator :
            !string.IsNullOrWhiteSpace(SearchSourceUrl) ? SearchSourceUrl :
            "(filtered search)";

        // Serialize filters to JSON
        var filters = BuildSearchFilters();
        var filtersJson = JsonSerializer.Serialize(filters);

        await _searchService.SaveSearchQueryAsync(name, queryDescription, IsCaseSensitive, IsRegex, filtersJson);

        // Reload saved queries
        if (LoadSavedQueriesCommand.CanExecute(null))
            await LoadSavedQueriesCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Renames the selected saved query.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteQueryOperations))]
    private async Task RenameQueryAsync()
    {
        if (SelectedSavedQuery == null)
            return;

        var newName = await ShowInputDialogAsync("Rename Search Query", "Enter new name:", SelectedSavedQuery.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == SelectedSavedQuery.Name)
            return;

        await _searchService.RenameSearchQueryAsync(SelectedSavedQuery.Name, newName);

        // Reload saved queries
        if (LoadSavedQueriesCommand.CanExecute(null))
            await LoadSavedQueriesCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Deletes the selected saved query.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecuteQueryOperations))]
    private async Task DeleteQueryAsync()
    {
        if (SelectedSavedQuery == null)
            return;

        // Show confirmation dialog
        var result = DXMessageBox.Show(
            $"Are you sure you want to delete the saved query '{SelectedSavedQuery.Name}'?",
            "Delete Query",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        await _searchService.DeleteSearchQueryAsync(SelectedSavedQuery.Name);

        // Reload saved queries
        if (LoadSavedQueriesCommand.CanExecute(null))
            await LoadSavedQueriesCommand.ExecuteAsync(null);
    }

    private bool CanExecuteQueryOperations() => SelectedSavedQuery != null;

    private async Task<string?> ShowInputDialogAsync(string title, string prompt, string defaultValue = "")
    {
        return await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var dialog = new ThemedWindow
            {
                Title = title,
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var stackPanel = new StackPanel
            {
                Margin = new Thickness(15),
                VerticalAlignment = VerticalAlignment.Center,
            };

            var label = new TextBlock
            {
                Text = prompt,
                Margin = new Thickness(0, 0, 0, 5),
            };

            var textBox = new TextBox
            {
                Text = defaultValue,
                MinWidth = 350,
            };

            textBox.SelectAll();

            stackPanel.Children.Add(label);
            stackPanel.Children.Add(textBox);
            grid.Children.Add(stackPanel);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(15, 0, 15, 15),
            };

            Grid.SetRow(buttonPanel, 1);

            var okButton = new SimpleButton
            {
                Content = "OK",
                Width = 75,
                Margin = new Thickness(0, 0, 10, 0),
            };

            var cancelButton = new SimpleButton
            {
                Content = "Cancel",
                Width = 75,
            };

            okButton.Click += (_, _) =>
            {
                dialog.DialogResult = true;
                dialog.Close();
            };

            cancelButton.Click += (_, _) =>
            {
                dialog.DialogResult = false;
                dialog.Close();
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            grid.Children.Add(buttonPanel);
            dialog.Content = grid;

            var result = dialog.ShowDialog();
            return result == true
                ? textBox.Text
                : null;
        });
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
