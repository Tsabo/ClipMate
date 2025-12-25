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
    private readonly SearchViewModel _viewModel;
    private IServiceScope? _validationScope;

    public SearchDialog(SearchViewModel viewModel)
    {
        InitializeComponent();

        // Get services from DI container
        var app = (App)Application.Current;
        var serviceProvider = app.ServiceProvider;

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
        var logger = serviceProvider.GetRequiredService<ILogger<SearchDialog>>();
        var configurationService = serviceProvider.GetRequiredService<IConfigurationService>();
        var collectionService = serviceProvider.GetRequiredService<ICollectionService>();

        // Load Monaco Editor configuration
        var monacoOptions = configurationService.Configuration.MonacoEditor;
        logger.LogInformation("Monaco configuration loaded - EnableDebug: {EnableDebug}, Theme: {Theme}, FontSize: {FontSize}",
            monacoOptions.EnableDebug, monacoOptions.Theme, monacoOptions.FontSize);

        SqlEditor.EditorOptions = monacoOptions;

        // Set database key for SQL validation
        var databaseKey = collectionService.GetActiveDatabaseKey();
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

        // Dispose validation scope when dialog closes
        Closed += (_, _) =>
        {
            _validationScope?.Dispose();
            _validationScope = null;
        };
    }

    private async void GoButton_Click(object sender, RoutedEventArgs e)
    {
        // Validate SQL query if user has modified it
        var app = (App)Application.Current;
        using var scope = app.ServiceProvider.CreateScope();
        var searchService = scope.ServiceProvider.GetRequiredService<ISearchService>();
        var collectionService = scope.ServiceProvider.GetRequiredService<ICollectionService>();

        // Get active database key for validation
        var databaseKey = collectionService.GetActiveDatabaseKey();
        if (string.IsNullOrEmpty(databaseKey))
        {
            DXMessageBox.Show(
                "No active database selected. Please select a database first.",
                "Database Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        var (isValid, errorMessage) = await searchService.ValidateSqlQueryAsync(_viewModel.SqlQuery, databaseKey);

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
        // TODO: Show help documentation
        DXMessageBox.Show(
            "Enter search criteria and click Go! to find clips.\n\n" +
            "Use filters to narrow your search by format, date range, or collection.\n" +
            "SQL tab allows advanced queries for power users.",
            "Search Help",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async Task InitializeSqlIntelliSenseAsync()
    {
        try
        {
            var app = (App)Application.Current;
            // Create a scope that lives for the lifetime of the dialog
            _validationScope = app.ServiceProvider.CreateScope();
            var logger = _validationScope.ServiceProvider.GetRequiredService<ILogger<SearchDialog>>();

            logger.LogDebug("InitializeSqlIntelliSenseAsync started");

            // Wait for Monaco editor to be fully initialized
            var maxWaitTime = TimeSpan.FromSeconds(5);
            var startTime = DateTime.Now;

            while (!SqlEditor.IsInitialized && DateTime.Now - startTime < maxWaitTime)
            {
                logger.LogDebug("Waiting for Monaco editor initialization...");
                await Task.Delay(100);
            }

            if (!SqlEditor.IsInitialized)
            {
                logger.LogWarning("Monaco editor not initialized after {Timeout}s, skipping SQL IntelliSense", maxWaitTime.TotalSeconds);
                Debug.WriteLine($"Monaco editor not initialized after {maxWaitTime.TotalSeconds}s");
                return;
            }

            logger.LogDebug("Monaco editor initialized, extracting schema");

            // Get DbContext to extract schema (from the long-lived scope)
            var dbContext = _validationScope.ServiceProvider.GetRequiredService<ClipMateDbContext>();
            var searchService = _validationScope.ServiceProvider.GetRequiredService<ISearchService>();

            // Extract schema
            var schema = SqlSchemaProvider.GetSchema(dbContext);
            var schemaJson = JsonSerializer.Serialize(schema);

            logger.LogDebug("Schema extracted: {TableCount} tables, {FunctionCount} functions, {KeywordCount} keywords",
                schema.Tables.Count, schema.Functions.Count, schema.Keywords.Count);

            // Set SearchService for validation (it will remain valid until dialog closes)
            SqlEditor.SearchService = searchService;

            // Register IntelliSense
            logger.LogDebug("Calling RegisterSqlIntelliSenseAsync with schema JSON length: {Length}", schemaJson.Length);
            var success = await SqlEditor.RegisterSqlIntelliSenseAsync(schemaJson);

            if (!success)
            {
                logger.LogWarning("Failed to register SQL IntelliSense");
                Debug.WriteLine("Failed to register SQL IntelliSense");
            }
            else
            {
                logger.LogInformation("SQL IntelliSense registered successfully");
                Debug.WriteLine("SQL IntelliSense registered successfully");
            }
        }
        catch (Exception ex)
        {
            var app = (App)Application.Current;
            using var scope = app.ServiceProvider.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SearchDialog>>();

            logger.LogError(ex, "Error initializing SQL IntelliSense");
            Debug.WriteLine($"Error initializing SQL IntelliSense: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
