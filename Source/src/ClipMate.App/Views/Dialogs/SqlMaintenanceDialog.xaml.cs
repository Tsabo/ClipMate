using System.ComponentModel;
using System.IO;
using ClipMate.App.ViewModels;
using ClipMate.Core.Services;
using DevExpress.Xpf.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WpfApplication = System.Windows.Application;
using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// SQL Maintenance dialog for executing raw SQL against the database.
/// Operates within a transaction that is committed on OK or rolled back on Cancel.
/// </summary>
public partial class SqlMaintenanceDialog
{
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<SqlMaintenanceDialog> _logger;
    private readonly ISqlMaintenanceService _sqlMaintenanceService;
    private readonly SqlMaintenanceViewModel _viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlMaintenanceDialog" /> class.
    /// </summary>
    public SqlMaintenanceDialog(SqlMaintenanceViewModel viewModel,
        ISqlMaintenanceService sqlMaintenanceService,
        IConfigurationService configurationService,
        ILogger<SqlMaintenanceDialog> logger)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _sqlMaintenanceService = sqlMaintenanceService;
        _configurationService = configurationService;
        _logger = logger;

        _viewModel.SqlQuery = """
                              select Collections.Title, count(Clips.Id) as Count, Del
                              from Collections, Clips
                              where Clips.CollectionId = Collections.Id
                              group by Collections.Title, Del
                              order by Collections.Title;
                              """;

        DataContext = _viewModel;

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Load Monaco Editor configuration
            var app = (App)WpfApplication.Current;
            var configService = app.ServiceProvider.GetRequiredService<IConfigurationService>();
            SqlEditor.EditorOptions = configService.Configuration.MonacoEditor;

            // Don't start transaction here - it will block the database and prevent clipboard monitoring
            // Transaction will be started automatically when executing write operations
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize SQL Maintenance dialog");
            DXMessageBox.Show(this,
                $"Failed to initialize: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        // Service will rollback uncommitted transaction on dispose
        try
        {
            await _sqlMaintenanceService.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing SQL maintenance service on close");
        }
    }

    private async void ExecuteSqlButton_Click(object sender, RoutedEventArgs e)
    {
        var sql = _viewModel.SqlQuery?.Trim();
        if (string.IsNullOrEmpty(sql))
        {
            _viewModel.StatusMessage = "Please enter a SQL query.";
            return;
        }

        // Check for dangerous keywords
        var dangerousKeywords = SqlMaintenanceViewModel.GetDangerousKeywords(sql);
        if (dangerousKeywords.Count > 0)
        {
            var result = DXMessageBox.Show(this,
                $"The query contains potentially dangerous commands:\n{string.Join(", ", dangerousKeywords)}\n\n" +
                "Are you sure you want to execute this query?",
                "Warning - Dangerous SQL",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;
        }

        _viewModel.IsExecuting = true;
        _viewModel.StatusMessage = "Executing...";

        try
        {
            await ExecuteSqlAsync(sql);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SQL: {Sql}", sql);
            _viewModel.StatusMessage = $"Error: {ex.Message}";
            _viewModel.ResultsTable = null;
            _viewModel.HasResults = false;
        }
        finally
        {
            _viewModel.IsExecuting = false;
        }
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        // Check if it's a SELECT query (returns results)
        var isSelect = sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
                       sql.TrimStart().StartsWith("PRAGMA", StringComparison.OrdinalIgnoreCase) ||
                       sql.TrimStart().StartsWith("EXPLAIN", StringComparison.OrdinalIgnoreCase);

        // Start transaction for write operations (if not already started)
        if (!isSelect && !_sqlMaintenanceService.HasActiveTransaction)
        {
            await _sqlMaintenanceService.BeginTransactionAsync();
            _logger.LogInformation("Transaction started for write operation");
        }

        if (isSelect)
        {
            // Execute as reader for SELECT queries
            var result = await _sqlMaintenanceService.ExecuteQueryAsync(sql);

            _viewModel.ResultsTable = result.Data;
            _viewModel.HasResults = result.HasResults;
            _viewModel.StatusMessage = $"{result.RowCount} rows selected";
        }
        else
        {
            // Execute as non-query for INSERT/UPDATE/DELETE etc.
            var rowsAffected = await _sqlMaintenanceService.ExecuteNonQueryAsync(sql);

            _viewModel.ResultsTable = null;
            _viewModel.HasResults = false;
            _viewModel.StatusMessage = $"{rowsAffected} rows affected";
        }
    }

    private async void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (_sqlMaintenanceService.HasActiveTransaction)
        {
            try
            {
                await _sqlMaintenanceService.CommitTransactionAsync();

                DXMessageBox.Show(this,
                    "Database changes have been committed successfully.",
                    "Commit Successful",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error committing transaction");
                DXMessageBox.Show(this,
                    $"Failed to commit transaction: {ex.Message}",
                    "Commit Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }
        }

        DialogResult = true;
        Close();
    }

    private async void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_sqlMaintenanceService.HasActiveTransaction)
        {
            try
            {
                await _sqlMaintenanceService.RollbackTransactionAsync();

                DXMessageBox.Show(this,
                    "Database rollback was successful.\nAll changes have been reverted.",
                    "Rollback Successful",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rolling back transaction");
                DXMessageBox.Show(this,
                    $"Warning: Rollback may have failed: {ex.Message}",
                    "Rollback Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        DialogResult = false;
        Close();
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Open help documentation for SQL Maintenance
        DXMessageBox.Show(this,
            "SQL Maintenance allows you to execute raw SQL queries against the database.\n\n" +
            "• Execute SQL: Runs the query within the current transaction\n" +
            "• OK: Commits all changes permanently\n" +
            "• Cancel: Rolls back all changes made during this session\n" +
            "• Save Result To File: Exports query results to a text file\n\n" +
            "WARNING: Use extreme caution! Incorrect SQL can corrupt your database.",
            "SQL Maintenance Help",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void SaveResultButton_Click(object sender, RoutedEventArgs e)
    {
        var exportConfig = _configurationService.Configuration.Export;
        var defaultDirectory = exportConfig.GetResolvedExportDirectory();

        // Ensure directory exists
        if (!Directory.Exists(defaultDirectory))
            Directory.CreateDirectory(defaultDirectory);

        var saveDialog = new WpfSaveFileDialog
        {
            Title = "Save SQL Results",
            Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            DefaultExt = ".txt",
            FileName = "ClipMate_SQL_Dump.txt",
            InitialDirectory = defaultDirectory,
        };

        if (saveDialog.ShowDialog() != true)
            return;

        try
        {
            var content = _viewModel.FormatResultsAsText();
            File.WriteAllText(saveDialog.FileName, content);

            _logger.LogInformation("SQL results saved to: {FilePath}", saveDialog.FileName);

            DXMessageBox.Show(this,
                $"Results saved to:\n{saveDialog.FileName}",
                "Save Successful",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving SQL results");
            DXMessageBox.Show(this,
                $"Failed to save results: {ex.Message}",
                "Save Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
