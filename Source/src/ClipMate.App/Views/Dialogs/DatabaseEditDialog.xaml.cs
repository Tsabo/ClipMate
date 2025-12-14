using System.IO;
using System.Windows;
using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;
using DevExpress.Xpf.Dialogs;
using DXMessageBox = DevExpress.Xpf.Core.DXMessageBox;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Dialog for editing database configuration properties.
/// </summary>
public partial class DatabaseEditDialog
{
    private readonly DatabaseEditViewModel _viewModel;

    /// <summary>
    /// Initializes a new instance for creating a new database.
    /// </summary>
    public DatabaseEditDialog()
    {
        InitializeComponent();
        _viewModel = new DatabaseEditViewModel();
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Initializes a new instance for editing an existing database.
    /// </summary>
    /// <param name="config">The database configuration to edit.</param>
    public DatabaseEditDialog(DatabaseConfiguration config)
        : this()
    {
        _viewModel.LoadFrom(config);
    }

    /// <summary>
    /// Gets the edited database configuration, or null if cancelled.
    /// </summary>
    public DatabaseConfiguration? DatabaseConfig { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e) => TitleTextBox.Focus();

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(_viewModel.DatabaseName))
        {
            DXMessageBox.Show(this, "Please enter a database name.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);

            TitleTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(_viewModel.DatabaseFilePath))
        {
            DXMessageBox.Show(this, "Please enter a database directory.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);

            DirectoryTextBox.Focus();
            return;
        }

        // Validate file path (expand environment variables first)
        var expandedPath = Environment.ExpandEnvironmentVariables(_viewModel.DatabaseFilePath);
        var directory = Path.GetDirectoryName(expandedPath);

        // Check if directory is valid (either exists or can be created)
        try
        {
            // Try to create the directory if it doesn't exist
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                var result = DXMessageBox.Show(this,
                    $"The directory '{directory}' does not exist.\n\nWould you like to create it?",
                    "Directory Not Found",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                    Directory.CreateDirectory(directory);
                else if (result == MessageBoxResult.Cancel)
                    return;
                // If No, continue but warn that database may not be accessible
                else
                {
                    DXMessageBox.Show(this,
                        "Warning: The database directory does not exist. The database may not be accessible until the directory is created.",
                        "Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }

            // Test write permissions
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                var testFile = Path.Combine(directory, $".clipmate_test_{Guid.NewGuid():N}.tmp");
                try
                {
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                }
                catch (UnauthorizedAccessException)
                {
                    DXMessageBox.Show(this,
                        $"You do not have write permissions for the directory:\n{directory}\n\nPlease choose a different location or adjust permissions.",
                        "Permission Denied",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    DirectoryTextBox.Focus();
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            DXMessageBox.Show(this,
                $"Invalid file path:\n{ex.Message}",
                "Invalid Path",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            DirectoryTextBox.Focus();
            return;
        }

        // Create the configuration object
        DatabaseConfig = _viewModel.ToConfiguration();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        // Sanitize database name for default filename
        var sanitizedName = _viewModel.DatabaseName.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace(".", "")
            .Replace(",", "");

        using var saveDialog = new DXSaveFileDialog();

        saveDialog.Title = "Select Database File Location";
        saveDialog.Filter = "SQLite Database (*.db)|*.db|All Files (*.*)|*.*";
        saveDialog.DefaultExt = ".db";
        saveDialog.FileName = string.IsNullOrWhiteSpace(sanitizedName)
            ? "clipmate.db"
            : $"{sanitizedName}.db";

        saveDialog.InitialDirectory = string.IsNullOrWhiteSpace(_viewModel.DatabaseFilePath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipMate")
            : Path.GetDirectoryName(Environment.ExpandEnvironmentVariables(_viewModel.DatabaseFilePath));

        if (saveDialog.ShowDialog() == true)
            _viewModel.DatabaseFilePath = saveDialog.FileName;
    }

    private void ExpandButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_viewModel.DatabaseFilePath))
        {
            var expanded = Environment.ExpandEnvironmentVariables(_viewModel.DatabaseFilePath);
            _viewModel.DatabaseFilePath = expanded;
            DXMessageBox.Show(this,
                $"Expanded path:\n{expanded}",
                "Environment Variables Expanded",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
