using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Controls;
using ClipMate.App.Models;
using ClipMate.App.Services;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Services;
using ClipMate.Core.Services.Abstractions;
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
    private readonly DatabaseSearchRunner _runner;
    private readonly SearchInList _searchIn;
    private readonly ISearchService _searchService;
    private readonly ISearchTargetSource _searchTargetSource;
    private string? _allDatabasesProgress;
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
    private string? _selectedFormat;

    // Saved Queries
    [ObservableProperty]
    private SavedSearchQuery? _selectedSavedQuery;

    // SQL
    [ObservableProperty]
    private string _sqlQuery = "Select Clip.* from Clip\nWhere Clip.Del = False\nOrder By Clip.ID;";

    /// <summary>
    /// What the dialog searches: the Database (local or remote), collection or folder chosen in "Search in", which
    /// starts at the one selected in the collection tree. <c>null</c> (no Database at all) searches the active Database.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRemoteTarget))]
    [NotifyPropertyChangedFor(nameof(ShowsPeerFiltersNote))]
    [NotifyPropertyChangedFor(nameof(IsSqlAvailable))]
    [NotifyPropertyChangedFor(nameof(CanSearchDeletedAndShortcuts))]
    [NotifyPropertyChangedFor(nameof(SelectedSearchIn))]
    [NotifyPropertyChangedFor(nameof(SearchingDescription))]
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
        ICollectionTreeBuilder collectionTreeBuilder,
        ILogger<SearchViewModel> logger,
        TimeProvider? timeProvider = null)
    {
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _databases = databases ?? throw new ArgumentNullException(nameof(databases));
        _searchTargetSource = searchTargetSource ?? throw new ArgumentNullException(nameof(searchTargetSource));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(searchResultsCache);
        ArgumentNullException.ThrowIfNull(remoteSearchResultsCache);
        ArgumentNullException.ThrowIfNull(collectionTreeBuilder);

        _runner = new DatabaseSearchRunner(databases, searchService, collectionService, searchResultsCache, remoteSearchResultsCache, messenger, logger,
            timeProvider ?? TimeProvider.System);

        _searchIn = new SearchInList(databases, collectionTreeBuilder, logger);
        _searchIn.CollectionsAdded += added =>
        {
            // The collection or folder chosen before its Database's tree arrived is now listed
            if (added.Any(p => p.Searches(Target)))
                OnPropertyChanged(nameof(SelectedSearchIn));
        };

        // Subscribe to property changes to regenerate SQL
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(SqlQuery) && e.PropertyName != nameof(IsSearching) &&
                e.PropertyName != nameof(TotalMatches) && e.PropertyName != nameof(IsRemoteTarget) &&
                e.PropertyName != nameof(IsSqlAvailable) && e.PropertyName != nameof(SelectedSearchIn) &&
                e.PropertyName != nameof(SearchingDescription) && e.PropertyName != nameof(CanSearchDeletedAndShortcuts) &&
                e.PropertyName != nameof(ShowsPeerFiltersNote) && e.PropertyName != null)
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
    /// What "Search in" offers: "All Databases", then every Database, local and remote, each followed by its collections
    /// and folders as the collection tree lays them out. Filled by <see cref="LoadSearchInOptionsAsync" />.
    /// </summary>
    public ObservableCollection<SearchInOption> SearchInOptions => _searchIn.Options;

    /// <summary>
    /// Fills <see cref="SearchInOptions" />: every Database at once, then each one's collections and folders as its tree
    /// arrives, so a slow Peer holds up only its own. A Database that can't be read is grayed out. Call when the
    /// dialog opens; a load still running from the last time is dropped.
    /// </summary>
    public Task LoadSearchInOptionsAsync() => _searchIn.LoadAsync();

    /// <summary>
    /// Gets or sets the "Search in" entry chosen: the one that searches <see cref="Target" />, or <c>null</c> while its
    /// collection hasn't arrived yet. An unavailable entry can't be chosen.
    /// </summary>
    public SearchInOption? SelectedSearchIn
    {
        get => _searchIn.Find(Target);
        set
        {
            if (value is { IsAvailable: true })
                Target = value.Target;
            else if (value != null)
                _logger.LogDebug("Search in: {Path} not chosen: it is unavailable", value.Path);

            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets whether the target is a Remote Database, searched by its Peer.
    /// </summary>
    public bool IsRemoteTarget => Target is DatabaseSearchTarget { IsRemote: true };

    /// <summary>
    /// Gets whether the SQL panel is shown. No SQL is ever sent to a Peer, so it is hidden for Remote Databases and for
    /// "All Databases", which includes them.
    /// </summary>
    public bool IsSqlAvailable => IsSqlAvailableFor(Target);

    /// <summary>
    /// Gets whether the deleted-clips and shortcut filters apply. Peers never serve deleted clips or shortcuts, so they
    /// are disabled for Remote Databases; "All Databases" applies them to its local Databases.
    /// </summary>
    public bool CanSearchDeletedAndShortcuts => Target?.SearchesLocalDatabases ?? true;

    /// <summary>
    /// Gets whether the dialog notes that the deleted-clips and shortcut filters don't apply to Peers: for "All
    /// Databases", which searches local and Remote Databases alike.
    /// </summary>
    public bool ShowsPeerFiltersNote => Target is { SearchesLocalDatabases: true, SearchesPeers: true };

    /// <summary>
    /// Gets what the dialog says while a search runs: the Database searched, or for "All Databases" how many have
    /// answered and which are still searching.
    /// </summary>
    public string SearchingDescription => _allDatabasesProgress ?? $"Searching {Target?.Name ?? "the active Database"}…";

    /// <summary>
    /// Gets the local Database whose SQL the dialog validates and suggests: the chosen one, or the active Database when
    /// none is chosen. <c>null</c> when the search sends no SQL (a Remote Database, "All Databases") or there is no
    /// Database at all.
    /// </summary>
    public string? SqlDatabaseKey => Target is { } target ? target.SqlDatabaseKey : _collectionService.GetActiveDatabaseKey() is { Length: > 0 } key ? key : null;

    private static bool IsSqlAvailableFor(SearchTarget? target) => target is null || target.SqlDatabaseKey != null;

    /// <summary>
    /// Starts "Search in" at what is selected in the collection tree (a local or Remote Database, collection or folder),
    /// or at the active Database when nothing is. Call when the dialog opens; the user can then choose another.
    /// </summary>
    public void RefreshTarget()
    {
        Target = _searchTargetSource.GetSearchTarget() ?? ActiveDatabaseTarget();
        _logger.LogInformation("Search dialog opened on {Target}", Target?.Describe() ?? "the active Database");
    }

    /// <summary>
    /// The active Database, as a target, or <c>null</c> when there is none.
    /// </summary>
    private DatabaseSearchTarget? ActiveDatabaseTarget()
    {
        var key = _collectionService.GetActiveDatabaseKey();
        var database = string.IsNullOrEmpty(key) ? null : _databases.All.FirstOrDefault(p => p.Key == key);
        return database == null ? null : SearchTarget.For(database);
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
    /// A different Database chosen in "Search in" brings its own formats, and a Remote one (or "All Databases") hides
    /// the saved searches that need SQL.
    /// </summary>
    partial void OnTargetChanged(SearchTarget? oldValue, SearchTarget? newValue)
    {
        if (oldValue == null && newValue == null || oldValue != null && oldValue.SearchesSameDatabasesAs(newValue))
            return;

        LoadFormatsCommand.Execute(null);
        if (IsSqlAvailableFor(oldValue) != IsSqlAvailableFor(newValue))
            LoadSavedQueriesCommand.Execute(null);
    }

    /// <summary>
    /// Called when SelectedSavedQuery changes. Loads the saved filters and text into the search form. A saved search
    /// never changes "Search in"; the scope older ones stored is ignored.
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
    }

    private void UpdateSqlQuery()
    {
        var filters = BuildSearchFilters();
        // Pass empty string for query parameter since all search criteria are in filters
        SqlQuery = _searchService.BuildSqlQuery("", Target?.Narrow(filters) ?? filters);
    }

    /// <summary>
    /// What a search with the form as filled in is called: the first text searched for, else "(filtered search)".
    /// </summary>
    private string QueryDescription =>
        !string.IsNullOrWhiteSpace(SearchText) ? SearchText :
        !string.IsNullOrWhiteSpace(SearchClipText) ? SearchClipText :
        !string.IsNullOrWhiteSpace(SearchCreator) ? SearchCreator :
        !string.IsNullOrWhiteSpace(SearchSourceUrl) ? SearchSourceUrl :
        "(filtered search)";

    /// <summary>
    /// Searches what "Search in" chose with the current filters; each Database searched gets the matches in its Search
    /// Results node.
    /// </summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        _runningSearch?.Cancel("another search started");
        using var search = new RunningSearch();
        _runningSearch = search;
        try
        {
            IsSearching = true;
            var query = QueryDescription;
            var matches = await _runner.RunAsync(Target, BuildSearchFilters(), query, search, SetAllDatabasesProgress);
            TotalMatches = matches.TotalMatches;
            SearchResults.Clear();
            foreach (var item in matches.Clips)
                SearchResults.Add(item);

            _messenger.Send(new SearchResultsChangedEvent(query, SearchResults.ToList()));
        }
        finally
        {
            if (ReferenceEquals(_runningSearch, search))
            {
                _runningSearch = null;
                SetAllDatabasesProgress(null);
                IsSearching = false;
            }
        }
    }

    private void SetAllDatabasesProgress(string? progress)
    {
        _allDatabasesProgress = progress;
        OnPropertyChanged(nameof(SearchingDescription));
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

            // A saved search that relies on SQL cannot run against a Remote Database, nor "All Databases"
            SavedQueries.Clear();
            foreach (var item in queries.Where(p => IsSqlAvailable || !DependsOnSql(p)))
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
            // The formats of the local Databases searched; a Peer doesn't list its formats, so a Remote Database offers
            // none (one can still be typed)
            var databaseKeys = Target is { } target
                ? target.DatabasesIn(_databases).Where(p => p is { Remote: null, IsAvailable: true }).Select(p => p.Key).ToList()
                : new[] { _collectionService.GetActiveDatabaseKey() }.OfType<string>().Where(p => p.Length > 0).ToList();

            var formats = new List<string>();
            foreach (var item in databaseKeys)
                formats.AddRange((await _clipService.GetDistinctFormatsAsync(item)).Except(formats, StringComparer.OrdinalIgnoreCase).ToList());

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
    /// Saves the current search query with a name.
    /// </summary>
    [RelayCommand]
    private async Task SaveQueryAsync()
    {
        // Show input dialog to get query name
        var name = await ShowInputDialogAsync("Save Search Query", "Enter query name:");
        if (string.IsNullOrWhiteSpace(name))
            return;

        // A saved search keeps only its filters and text, so loading it never changes "Search in"; the deleted-clips and
        // shortcut filters are kept only while they apply
        var filters = CanSearchDeletedAndShortcuts ? BuildSearchFilters() : DatabaseSearchRunner.WithoutLocalOnlyFilters(BuildSearchFilters());
        var filtersJson = JsonSerializer.Serialize(filters);

        await _searchService.SaveSearchQueryAsync(name, QueryDescription, IsCaseSensitive, IsRegex, filtersJson);

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

    /// <summary>
    /// The filters for the form as filled in, for every collection; the target narrows them, and each Database leaves
    /// out what it can't apply.
    /// </summary>
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
            // All boxes checked (or none) means no type restriction, so rich text, HTML and custom clips still match
            ContentTypes = contentTypes.Count is > 0 and < 3
                ? contentTypes
                : null,
            DateRange = dateRange,
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
