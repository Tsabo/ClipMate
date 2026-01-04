using System.Diagnostics;
using System.Text.Json;
using ClipMate.App.ViewModels;
using ClipMate.Core.Services;
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

        DataContext = _viewModel;

        var app = (App)Application.Current;
        var configurationService = app.ServiceProvider.GetRequiredService<IConfigurationService>();

        // Load Monaco Editor configuration
        var monacoOptions = configurationService.Configuration.MonacoEditor;
        _logger.LogInformation("Monaco configuration loaded - EnableDebug: {EnableDebug}, Theme: {Theme}, FontSize: {FontSize}",
            monacoOptions.EnableDebug, monacoOptions.Theme, monacoOptions.FontSize);

        SqlEditor.EditorOptions = monacoOptions;

        // Set database key for SQL validation
        var databaseKey = _collectionService.GetActiveDatabaseKey();
        if (!string.IsNullOrEmpty(databaseKey))
            SqlEditor.DatabaseKey = databaseKey;

        // Load formats, collections, and saved queries when dialog opens
        Loaded += async (_, _) =>
        {
            if (_viewModel.LoadFormatsCommand.CanExecute(null))
                await _viewModel.LoadFormatsCommand.ExecuteAsync(null);

            if (_viewModel.LoadCollectionsCommand.CanExecute(null))
                await _viewModel.LoadCollectionsCommand.ExecuteAsync(null);

            if (_viewModel.LoadSavedQueriesCommand.CanExecute(null))
                await _viewModel.LoadSavedQueriesCommand.ExecuteAsync(null);

            // Initialize SQL IntelliSense after editor is loaded
            await InitializeSqlIntelliSenseAsync();
        };
    }

    private async void GoButton_Click(object sender, RoutedEventArgs e)
    {
        // Validate SQL query if user has modified it
        // Get active database key for validation
        var databaseKey = _collectionService.GetActiveDatabaseKey();
        if (string.IsNullOrEmpty(databaseKey))
        {
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
            DXMessageBox.Show(
                $"Invalid SQL query:\n\n{errorMessage}\n\nPlease correct the query and try again.",
                "SQL Validation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        // Execute search
        if (!_viewModel.SearchCommand.CanExecute(null))
            return;

        try
        {
            await _viewModel.SearchCommand.ExecuteAsync(null);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            // Show error but keep dialog open so user can fix the SQL
            DXMessageBox.Show(
                $"Search execution failed:\n\n{ex.Message}\n\nPlease correct the query and try again.",
                "Search Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

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
                Debug.WriteLine($"Monaco editor not initialized after {maxWaitTime.TotalSeconds}s");
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
                Debug.WriteLine("Failed to register SQL IntelliSense");
            }
            else
            {
                _logger.LogInformation("SQL IntelliSense registered successfully");
                Debug.WriteLine("SQL IntelliSense registered successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing SQL IntelliSense");
            Debug.WriteLine($"Error initializing SQL IntelliSense: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
