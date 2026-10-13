using System.ComponentModel;
using System.IO;
using ClipMate.App.ViewModels;
using DevExpress.Xpf.Dialogs;
using TextBox = System.Windows.Controls.TextBox;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Dialog for importing an original ClipMate 7.x database.
/// </summary>
public partial class ClipMate7ImportDialog
{
    private readonly ClipMate7ImportViewModel _viewModel;

    public ClipMate7ImportDialog(ClipMate7ImportViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.CloseRequested += (_, _) => Close();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e) => await _viewModel.InitializeAsync();

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // Don't close mid-import; cancel instead (the import rolls back)
        if (!_viewModel.IsProcessing)
            return;

        e.Cancel = true;
        _viewModel.AbortCommand.Execute(null);
    }

    private async void BrowseSourceFolder_Click(object sender, RoutedEventArgs e)
    {
        string? folder = null;
        using (var folderDialog = new DXFolderBrowserDialog())
        {
            folderDialog.Description = "Select the ClipMate 7 database folder";
            folderDialog.SelectedPath = Directory.Exists(_viewModel.SourceFolder)
                ? _viewModel.SourceFolder
                : ClipMate7ImportViewModel.DefaultSourceFolder;

            if (folderDialog.ShowDialog() == true)
                folder = folderDialog.SelectedPath;
        }

        if (folder is not null)
            await _viewModel.SetSourceFolderAsync(folder);
    }

    private void BrowseNewDatabase_Click(object sender, RoutedEventArgs e)
    {
        using var saveDialog = new DXSaveFileDialog();

        saveDialog.Title = "Select New Database File";
        saveDialog.Filter = "SQLite Database (*.db)|*.db|All Files (*.*)|*.*";
        saveDialog.DefaultExt = ".db";
        saveDialog.FileName = Path.GetFileName(_viewModel.NewDatabaseFilePath);
        saveDialog.InitialDirectory = Path.GetDirectoryName(Environment.ExpandEnvironmentVariables(_viewModel.NewDatabaseFilePath));

        if (saveDialog.ShowDialog() == true)
            _viewModel.NewDatabaseFilePath = saveDialog.FileName;
    }

    private void LogTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ((TextBox)sender).ScrollToEnd();
}
