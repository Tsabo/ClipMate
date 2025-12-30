using ClipMate.App.ViewModels;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Dialogs;

namespace ClipMate.App.Views.Dialogs;

public partial class FlatFileExportDialog
{
    private readonly FlatFileExportViewModel _viewModel;

    public FlatFileExportDialog(FlatFileExportViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            DXMessageBox.Show(this, $"Error initializing: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BrowseExportDirectory_Click(object sender, RoutedEventArgs e)
    {
        DXFolderBrowserDialog? folderDialog = null;
        try
        {
            folderDialog = new DXFolderBrowserDialog();
            folderDialog.Description = "Select export directory";
            folderDialog.ShowNewFolderButton = true;
            folderDialog.SelectedPath = string.IsNullOrWhiteSpace(_viewModel.FlatFileExportDirectory)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : Environment.ExpandEnvironmentVariables(_viewModel.FlatFileExportDirectory);

            if (folderDialog.ShowDialog() == true)
                _viewModel.SetFlatFileExportDirectory(folderDialog.SelectedPath);
        }
        finally
        {
            (folderDialog as IDisposable)?.Dispose();
        }
    }
}
