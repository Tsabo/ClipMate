using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using ClipMate.App.ViewModels;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Services;
using DevExpress.Xpf.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Dialog for advanced clip search with filters.
/// </summary>
public partial class SearchDialog
{
    private readonly ICollectionService _collectionService;
    private readonly ILogger<SearchDialog> _logger;
    private readonly ISearchService _searchService;
    private readonly SearchViewModel _viewModel;

    public SearchDialog(SearchViewModel viewModel, ISearchService searchService, ICollectionService collectionService, ILogger<SearchDialog> logger)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // "Search in" starts at what is selected in the collection tree: a local or Remote Database, collection or folder
        _viewModel.RefreshTarget();
        DataContext = _viewModel;

        // SQL validation and IntelliSense follow the Database chosen in "Search in"; Go! waits while a search runs
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateSqlDatabase();
        UpdateGoButton();

        // Closing the dialog stops a search that is still running (on the Peer too, for a Remote Database)
        Closed += (_, _) =>
        {
            _logger.LogInformation("Search dialog closed (searched: {Searched})", DialogResult == true);
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.CancelSearch("the search dialog was closed");
        };

        var app = (App)Application.Current;
        var configurationService = app.ServiceProvider.GetRequiredService<IConfigurationService>();

        // Load Monaco Editor configuration
        var monacoOptions = configurationService.Configuration.MonacoEditor;
        _logger.LogInformation("Monaco configuration loaded - EnableDebug: {EnableDebug}, Theme: {Theme}, FontSize: {FontSize}",
            monacoOptions.EnableDebug, monacoOptions.Theme, monacoOptions.FontSize);

        SqlEditor.EditorOptions = monacoOptions;

        // Load "Search in", formats and saved queries when the dialog opens. "Search in" lists each Database at once
        // and fills in a Peer's collections when they arrive, so the rest doesn't wait for it.
        Loaded += async (_, _) =>
        {
            var loadingSearchIn = _viewModel.LoadSearchInOptionsAsync();

            if (_viewModel.LoadFormatsCommand.CanExecute(null))
                await _viewModel.LoadFormatsCommand.ExecuteAsync(null);

            if (_viewModel.LoadSavedQueriesCommand.CanExecute(null))
                await _viewModel.LoadSavedQueriesCommand.ExecuteAsync(null);

            await loadingSearchIn;

            // Initialize SQL IntelliSense after editor is loaded
            await InitializeSqlIntelliSenseAsync();
        };
    }

    private async void GoButton_Click(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("Search pressed: {Kind} search of {DatabaseKey}", _viewModel.IsRemoteTarget ? "Remote" : "local",
            _viewModel.Target?.DatabaseKey ?? "the active Database");

        if (_viewModel.IsRemoteTarget)
        {
            await SearchRemoteDatabaseAsync();
            return;
        }

        // Validate the SQL against the Database chosen in "Search in"
        var databaseKey = SqlDatabaseKey;
        if (string.IsNullOrEmpty(databaseKey))
        {
            _logger.LogInformation("Search not run: no active Database");
            DXMessageBox.Show(
                "No active database selected. Please select a database first.",
                "Database Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        var (isValid, errorMessage) = await _searchService.ValidateSqlQueryAsync(_viewModel.SqlQuery, databaseKey);

        if (!isValid)
        {
            _logger.LogInformation("Search not run: invalid SQL ({Error})", errorMessage);
            DXMessageBox.Show(
                $"Invalid SQL query:\n\n{errorMessage}\n\nPlease correct the query and try again.",
                "SQL Validation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        // Execute search
        if (!_viewModel.SearchCommand.CanExecute(null))
        {
            _logger.LogInformation("Search not run: another search is still running");
            return;
        }

        try
        {
            await _viewModel.SearchCommand.ExecuteAsync(null);
            DialogResult = true;
            Close();
        }
        catch (OperationCanceledException)
        {
            // The dialog was closed while searching; the view model logged who canceled it
        }
        catch (Exception ex)
        {
            // The view model logged the failure
            // Show error but keep dialog open so user can fix the SQL
            DXMessageBox.Show(
                $"Search execution failed:\n\n{ex.Message}\n\nPlease correct the query and try again.",
                "Search Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Runs a Remote Search. There is no SQL to validate: the Peer builds the query from the filters.
    /// </summary>
    private async Task SearchRemoteDatabaseAsync()
    {
        if (!_viewModel.SearchCommand.CanExecute(null))
        {
            _logger.LogInformation("Remote Search not run: another search is still running");
            return;
        }

        try
        {
            await _viewModel.SearchCommand.ExecuteAsync(null);
            DialogResult = true;
            Close();
        }
        catch (OperationCanceledException)
        {
            // The dialog was closed while searching; the view model logged who canceled it
        }
        catch (DatabaseUnavailableException ex)
        {
            // The view model logged the failure
            DXMessageBox.Show(ex.Message, "Remote Search", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            // The view model logged the failure, with the exception
            DXMessageBox.Show($"Search execution failed:\n\n{ex.Message}", "Search Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Cancel stops a running search and keeps the dialog open, so the search can be changed; otherwise it closes the
    /// dialog.
    /// </summary>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsSearching)
        {
            _viewModel.CancelSearch("Cancel was pressed");
            return;
        }

        DialogResult = false;
        Close();
    }

    /// <summary>
    /// The local Database whose SQL the dialog validates and suggests: the chosen one, or the active Database when none
    /// is chosen. <c>null</c> for a Remote Database, which is never sent SQL.
    /// </summary>
    private string? SqlDatabaseKey => _viewModel.Target is { } target
        ? target.IsRemote ? null : target.DatabaseKey
        : _collectionService.GetActiveDatabaseKey();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SearchViewModel.Target))
            UpdateSqlDatabase();
        else if (e.PropertyName == nameof(SearchViewModel.IsSearching))
            UpdateGoButton();
    }

    private void UpdateSqlDatabase()
    {
        if (SqlDatabaseKey is { Length: > 0 } databaseKey)
            SqlEditor.DatabaseKey = databaseKey;
    }

    private void UpdateGoButton() => GoButton.IsEnabled = !_viewModel.IsSearching;

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://jeremy.browns.info/ClipMate/user-interface/main-toolbar/search/",
            UseShellExecute = true,
        });
    }

    private async Task InitializeSqlIntelliSenseAsync()
    {
        try
        {
            _logger.LogDebug("InitializeSqlIntelliSenseAsync started");

            // Wait for Monaco editor to be fully initialized
            var maxWaitTime = TimeSpan.FromSeconds(5);
            var startTime = DateTime.Now;

            while (!SqlEditor.IsInitialized && DateTime.Now - startTime < maxWaitTime)
            {
                _logger.LogDebug("Waiting for Monaco editor initialization...");
                await Task.Delay(100);
            }

            if (!SqlEditor.IsInitialized)
            {
                _logger.LogWarning("Monaco editor not initialized after {Timeout}s, skipping SQL IntelliSense", maxWaitTime.TotalSeconds);
                return;
            }

            _logger.LogDebug("Monaco editor initialized, extracting schema");

            // Get DbContext to extract schema
            var app = (App)Application.Current;
            var dbContext = app.ServiceProvider.GetRequiredService<ClipMateDbContext>();

            // Extract schema
            var schema = SqlSchemaProvider.GetSchema(dbContext);
            var schemaJson = JsonSerializer.Serialize(schema);

            _logger.LogDebug("Schema extracted: {TableCount} tables, {FunctionCount} functions, {KeywordCount} keywords",
                schema.Tables.Count, schema.Functions.Count, schema.Keywords.Count);

            // Set SearchService for validation
            SqlEditor.SearchService = _searchService;

            // Register IntelliSense
            _logger.LogDebug("Calling RegisterSqlIntelliSenseAsync with schema JSON length: {Length}", schemaJson.Length);
            var success = await SqlEditor.RegisterSqlIntelliSenseAsync(schemaJson);

            if (!success)
            {
                _logger.LogWarning("Failed to register SQL IntelliSense");
            }
            else
            {
                _logger.LogInformation("SQL IntelliSense registered successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing SQL IntelliSense");
        }
    }
}
