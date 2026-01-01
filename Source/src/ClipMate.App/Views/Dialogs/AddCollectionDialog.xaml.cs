using DevExpress.Xpf.Core;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Dialog for creating a new collection with positioning options.
/// </summary>
public partial class AddCollectionDialog : ThemedWindow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddCollectionDialog"/> class.
    /// </summary>
    /// <param name="selectedCollectionName">The name of the currently selected collection, or null if none.</param>
    public AddCollectionDialog(string? selectedCollectionName = null)
    {
        InitializeComponent();

        // Update the "below selected" radio button text
        if (!string.IsNullOrEmpty(selectedCollectionName))
        {
            BelowSelectedText.Text = $"Below the currently selected collection: [{selectedCollectionName}]";
            BelowSelectedRadio.IsChecked = true;
        }
        else
        {
            BelowSelectedText.Text = "Below the currently selected collection: [None]";
            TopLevelRadio.IsChecked = true;
            BelowSelectedRadio.IsEnabled = false;
        }

        // Focus the text box when dialog opens
        Loaded += (_, _) =>
        {
            CollectionNameTextBox.Focus();
            CollectionNameTextBox.SelectAll();
        };
    }

    /// <summary>
    /// Gets the entered collection name.
    /// </summary>
    public string CollectionName => CollectionNameTextBox.Text?.Trim() ?? string.Empty;

    /// <summary>
    /// Gets a value indicating whether the collection should be created below the selected collection.
    /// </summary>
    public bool PositionBelowSelected => BelowSelectedRadio.IsChecked == true;

    /// <summary>
    /// Gets a value indicating whether the collection should be created at the top level.
    /// </summary>
    public bool PositionAtTopLevel => TopLevelRadio.IsChecked == true;

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(CollectionName))
            return;

        DXMessageBox.Show(
            this,
            "Please enter a collection name.",
            "Validation Error",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);
        e.Handled = true;
        CollectionNameTextBox.Focus();
    }
}
