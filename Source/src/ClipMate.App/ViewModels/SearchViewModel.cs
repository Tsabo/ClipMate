using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Controls;
using ClipMate.App.Models;
using ClipMate.App.Services;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Services;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Core.ValueObjects;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DevExpress.Xpf.Core;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
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
    private readonly IClipService _clipService;
    private readonly ICollectionService _collectionService;
    private readonly IDatabases _databases;
    private readonly ILogger<SearchViewModel> _logger;
    private readonly IMessenger _messenger;
    private readonly RemoteSearchResultsCache _remoteSearchResultsCache;
    private readonly SearchResultsCache _searchResultsCache;
    private readonly ISearchService _searchService;
    private readonly ISearchTargetSource _searchTargetSource;
    private RunningSearch? _runningSearch;

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

    /// <summary>
    /// What the dialog searches: the Database (local or remote), collection and folder selected in the collection tree.
    /// <c>null</c> searches the active Database.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRemoteTarget))]
    [NotifyPropertyChangedFor(nameof(IsSqlAvailable))]
    [NotifyPropertyChangedFor(nameof(CanSearchDeletedAndShortcuts))]
    [NotifyPropertyChangedFor(nameof(TargetDescription))]
    private SearchTarget? _target;

    [ObservableProperty]
    private int _totalMatches;

    public SearchViewModel(ISearchService searchService,
        ICollectionService collectionService,
        IClipService clipService,
        IMessenger messenger,
        SearchResultsCache searchResultsCache,
        IDatabases databases,
        RemoteSearchResultsCache remoteSearchResultsCache,
        ISearchTargetSource searchTargetSource,
        ILogger<SearchViewModel> logger)
    {
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _searchResultsCache = searchResultsCache ?? throw new ArgumentNullException(nameof(searchResultsCache));
        _databases = databases ?? throw new ArgumentNullException(nameof(databases));
        _remoteSearchResultsCache = remoteSearchResultsCache ?? throw new ArgumentNullException(nameof(remoteSearchResultsCache));
        _searchTargetSource = searchTargetSource ?? throw new ArgumentNullException(nameof(searchTargetSource));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Subscribe to property changes to regenerate SQL
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(SqlQuery) && e.PropertyName != nameof(IsSearching) &&
                e.PropertyName != nameof(TotalMatches) && e.PropertyName != nameof(IsRemoteTarget) &&
                e.PropertyName != nameof(IsSqlAvailable) && e.PropertyName != nameof(TargetDescription) &&
                e.PropertyName != nameof(CanSearchDeletedAndShortcuts) && e.PropertyName != null)
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
    /// The scopes the target allows: all collections, plus the selected collection and folder when there are.
    /// </summary>
    public ObservableCollection<SearchScopeOption> AvailableScopes { get; } = [new(SearchScope.AllCollections, "All collections")];

    /// <summary>
    /// Gets whether the target is a Remote Database, searched by its Peer.
    /// </summary>
    public bool IsRemoteTarget => Target?.IsRemote == true;

    /// <summary>
    /// Gets whether the SQL panel is shown. No SQL is ever sent to a Peer, so it is hidden for Remote Databases.
    /// </summary>
    public bool IsSqlAvailable => !IsRemoteTarget;

    /// <summary>
    /// Gets whether the deleted-clips and shortcut filters apply. Peers never serve deleted clips or shortcuts, so they
    /// are disabled for Remote Databases.
    /// </summary>
    public bool CanSearchDeletedAndShortcuts => !IsRemoteTarget;

    /// <summary>
    /// Gets the name of the Database the dialog searches, for display.
    /// </summary>
    public string TargetDescription => Target?.DatabaseName ?? "Active database";

    /// <summary>
    /// Takes the target (the Database selected in the collection tree, local or remote) from the current selection, and
    /// starts the scope at all collections of it, as searches did before the scope list existed. The selected collection
    /// and folder are offered as narrower scopes. Call when the dialog opens.
    /// </summary>
    public void RefreshTarget()
    {
        Target = _searchTargetSource.GetSearchTarget();
        SearchScope = SearchScope.AllCollections;
        _logger.LogInformation("Search dialog opened on {Target}", DescribeTarget(Target));
    }

    /// <summary>
    /// Describes a search target for the log: the Database key and name, and the selected collection and folder.
    /// </summary>
    private static string DescribeTarget(SearchTarget? target)
    {
        if (target == null)
            return "the active Database";

        var description = $"Database {target.DatabaseKey} ('{target.DatabaseName}'{(target.IsRemote ? ", remote" : string.Empty)})";
        if (target.CollectionName != null)
            description += $", collection '{target.CollectionName}'";

        if (target.FolderName != null)
            description += $", folder '{target.FolderName}'";

        return description;
    }

    /// <summary>
    /// Cancels a search that is running; a Remote Search stops on the Peer too.
    /// </summary>
    /// <param name="reason">Who canceled it, for the log, e.g. "the search dialog was closed".</param>
    public void CancelSearch(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        _runningSearch?.Cancel(reason);
    }

    /// <summary>
    /// Gets whether a saved search relies on hand-written SQL (a stored query that is a SELECT statement rather than
    /// filters). No SQL is ever sent to a Peer, so such searches cannot run against a Remote Database.
    /// </summary>
    public static bool DependsOnSql(SavedSearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return string.IsNullOrWhiteSpace(query.FiltersJson) && query.Query.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Names the criteria a search uses, for the log. Never the text searched for, which may be private.
    /// </summary>
    private static string DescribeCriteria(SearchFilters filters)
    {
        var criteria = new List<string>();
        if (filters.TitleQuery != null)
            criteria.Add("title");

        if (filters.TextContentQuery != null)
            criteria.Add("clip text");

        if (filters.CreatorQuery != null)
            criteria.Add("creator");

        if (filters.SourceUrlQuery != null)
            criteria.Add("source URL");

        if (filters.Format != null)
            criteria.Add("format");

        if (filters.ContentTypes != null)
            criteria.Add($"types {string.Join("/", filters.ContentTypes)}");

        if (filters.DateRange != null)
            criteria.Add("date range");

        if (filters.EncryptedOnly == true)
            criteria.Add("encrypted only");

        if (filters.HasShortcutOnly == true)
            criteria.Add("with shortcut only");

        if (filters.IncludeDeleted)
            criteria.Add("including deleted");

        if (filters.IsRegex)
            criteria.Add("regex");

        if (filters.CaseSensitive)
            criteria.Add("case-sensitive");

        return criteria.Count == 0 ? "everything" : string.Join(", ", criteria);
    }

    partial void OnTargetChanged(SearchTarget? value)
    {
        AvailableScopes.Clear();
        AvailableScopes.Add(new SearchScopeOption(SearchScope.AllCollections, "All collections"));
        if (value?.CollectionId != null)
            AvailableScopes.Add(new SearchScopeOption(SearchScope.CurrentCollection, $"Collection: {value.CollectionName}"));

        if (value?.FolderId != null)
            AvailableScopes.Add(new SearchScopeOption(SearchScope.SpecificFolder, $"Folder: {value.FolderName}"));
    }

    /// <summary>
    /// The scope actually searched: one the target cannot offer (no collection or folder selected) falls back to all
    /// collections.
    /// </summary>
    private SearchScope EffectiveScope => AvailableScopes.Any(p => p.Scope == SearchScope) ? SearchScope : SearchScope.AllCollections;

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

        // Search scope: the collection or folder it means comes from the current selection, not the saved IDs
        SearchScope = AvailableScopes.Any(p => p.Scope == filters.Scope) ? filters.Scope : SearchScope.AllCollections;
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
        _runningSearch?.Cancel("another search started");
        using var search = new RunningSearch();
        _runningSearch = search;
        var searchedKey = Target?.DatabaseKey ?? "the active Database";
        var started = Stopwatch.GetTimestamp();
        try
        {
            IsSearching = true;

            var filters = BuildSearchFilters();

            // Use a descriptive query string - check all text search fields
            var queryDescription = !string.IsNullOrWhiteSpace(SearchText) ? SearchText :
                !string.IsNullOrWhiteSpace(SearchClipText) ? SearchClipText :
                !string.IsNullOrWhiteSpace(SearchCreator) ? SearchCreator :
                !string.IsNullOrWhiteSpace(SearchSourceUrl) ? SearchSourceUrl :
                "(filtered search)";

            _logger.LogInformation("Searching {DatabaseKey}, scope {Scope}, for {Criteria}", searchedKey, filters.Scope, DescribeCriteria(filters));

            IReadOnlyList<Clip> clips;
            string? databaseKey;
            if (Target is { } target)
            {
                // The Database searches itself: a local one with SQL, a Remote one on its Peer (criteria only, never SQL)
                var database = _databases.Get(target.DatabaseKey);
                var matches = await database.SearchAsync(filters, cancellationToken: search.Token);
                search.Token.ThrowIfCancellationRequested();
                _logger.LogInformation("Searched {DatabaseKey}: {Count} of {TotalMatches} matches in {ElapsedMs:F0} ms", searchedKey,
                    matches.Clips.Count, matches.TotalMatches, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                clips = matches.Clips;
                TotalMatches = matches.TotalMatches;
                databaseKey = target.DatabaseKey;

                // A Remote Database's results are kept with their clips, as they can't be read again by ID; the one
                // place search reads Remote (spec Q22 exception, see database-seam ticket 09)
                if (database.Remote is { } remote)
                    _remoteSearchResultsCache.SetResults(remote.Address, new RemoteSearchResult(queryDescription, clips, matches.TotalMatches));
                else
                    _searchResultsCache.SetResults(databaseKey, new SearchResult(queryDescription, clips.Select(p => p.Id).ToList()));
            }
            else
            {
                // Pass empty string for query parameter since all search criteria are in filters
                var results = await _searchService.SearchAsync("", filters, search.Token);
                _logger.LogInformation("Searched {DatabaseKey}: {Count} of {TotalMatches} matches in {ElapsedMs:F0} ms", searchedKey,
                    results.Clips.Count, results.TotalMatches, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                clips = results.Clips;
                TotalMatches = results.TotalMatches;
                databaseKey = _collectionService.GetActiveDatabaseKey();
                if (!string.IsNullOrEmpty(databaseKey))
                    _searchResultsCache.SetResults(databaseKey, new SearchResult(queryDescription, clips.Select(p => p.Id).ToList()));
            }

            SearchResults.Clear();
            foreach (var item in clips)
                SearchResults.Add(item);

            // Send messenger event with search results
            _messenger.Send(new SearchResultsChangedEvent(queryDescription, SearchResults.ToList()));

            if (!string.IsNullOrEmpty(databaseKey))
                _messenger.Send(new SearchExecutedEvent(databaseKey, queryDescription, clips.Select(p => p.Id).ToList()));
        }
        catch (OperationCanceledException) when (search.Token.IsCancellationRequested)
        {
            _logger.LogInformation("Search of {DatabaseKey} canceled after {ElapsedMs:F0} ms: {Reason}", searchedKey,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds, search.CancelReason);
            throw;
        }
        catch (DatabaseUnavailableException ex)
        {
            _logger.LogInformation("Search of {DatabaseKey} failed after {ElapsedMs:F0} ms: {Reason}", searchedKey,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Search of {DatabaseKey} failed after {ElapsedMs:F0} ms", searchedKey, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
        finally
        {
            if (ReferenceEquals(_runningSearch, search))
            {
                _runningSearch = null;
                IsSearching = false;
            }
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

            // A saved search that relies on SQL cannot run against a Remote Database
            SavedQueries.Clear();
            foreach (var item in queries.Where(p => !IsRemoteTarget || !DependsOnSql(p)))
                SavedQueries.Add(item);
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

            var formats = await _clipService.GetDistinctFormatsAsync(activeDatabaseKey);

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

        var scope = EffectiveScope;
        var remote = IsRemoteTarget;
        return new SearchFilters
        {
            // All boxes checked (or none) means no type restriction, so rich text, HTML and custom clips still match
            ContentTypes = contentTypes.Count is > 0 and < 3
                ? contentTypes
                : null,
            DateRange = dateRange,
            Scope = scope,
            CollectionId = scope == SearchScope.CurrentCollection ? Target?.CollectionId : null,
            FolderId = scope == SearchScope.SpecificFolder ? Target?.FolderId : null,
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
            // Peers never serve deleted clips or shortcuts
            HasShortcutOnly = HasShortcutOnly && !remote
                ? true
                : null,
            IncludeDeleted = IncludeDeletedClips && !remote,
        };
    }

    /// <summary>
    /// The search that is running, and who canceled it, so the log can say.
    /// </summary>
    private sealed class RunningSearch : IDisposable
    {
        private readonly CancellationTokenSource _cancellation = new();

        public CancellationToken Token => _cancellation.Token;

        /// <summary>
        /// Who canceled the search, or <c>null</c> while it has not been.
        /// </summary>
        public string? CancelReason { get; private set; }

        public void Cancel(string reason)
        {
            CancelReason ??= reason;
            _cancellation.Cancel();
        }

        public void Dispose() => _cancellation.Dispose();
    }
}
