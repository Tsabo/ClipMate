using ClipMate.App.ViewModels;
using ClipMate.Core.Services;
using DevExpress.Xpf.Core;
using Microsoft.Extensions.DependencyInjection;
using Application = System.Windows.Application;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Dialog for advanced clip search with filters.
/// </summary>
public partial class SearchDialog
{
    private readonly SearchViewModel _viewModel;

    public SearchDialog(SearchViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        // Load formats, collections, and saved queries when dialog opens
        Loaded += async (_, _) =>
        {
            if (_viewModel.LoadFormatsCommand.CanExecute(null))
                await _viewModel.LoadFormatsCommand.ExecuteAsync(null);

            if (_viewModel.LoadCollectionsCommand.CanExecute(null))
                await _viewModel.LoadCollectionsCommand.ExecuteAsync(null);

            if (_viewModel.LoadSavedQueriesCommand.CanExecute(null))
                await _viewModel.LoadSavedQueriesCommand.ExecuteAsync(null);
        };
    }

    private async void GoButton_Click(object sender, RoutedEventArgs e)
    {
        // Validate SQL query if user has modified it
        var app = (App)Application.Current;
        using var scope = app.ServiceProvider.CreateScope();
        var searchService = scope.ServiceProvider.GetRequiredService<ISearchService>();

        var (isValid, errorMessage) = await searchService.ValidateSqlQueryAsync(_viewModel.SqlQuery);

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
}
