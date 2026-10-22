using DevExpress.Xpf.Editors;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Dialog for adding/editing QuickPaste target specifications (PROCESSNAME:CLASSNAME), or bad window titles.
/// </summary>
public partial class QuickPasteTargetDialog
{
    public QuickPasteTargetDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Creates a dialog with a pre-filled target specification.
    /// </summary>
    /// <param name="currentTarget">The current target specification to edit.</param>
    public QuickPasteTargetDialog(string currentTarget)
        : this()
    {
        TargetTextBox.Text = currentTarget;
    }

    /// <summary>
    /// Creates a dialog for a bad window title instead of a PROCESSNAME:CLASSNAME specification.
    /// </summary>
    /// <param name="currentTitle">The title to edit, or empty to add one.</param>
    public static QuickPasteTargetDialog ForWindowTitle(string currentTitle = "")
    {
        var dialog = new QuickPasteTargetDialog(currentTitle);
        dialog.SpecificationHelp.Visibility = Visibility.Collapsed;
        dialog.TitleHelp.Visibility = Visibility.Visible;
        dialog.TargetItem.Label = "Title:";

        return dialog;
    }

    /// <summary>
    /// Gets the target specification entered by the user.
    /// </summary>
    public string TargetSpecification { get; private set; } = string.Empty;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TargetTextBox.Focus();
        TargetTextBox.SelectAll();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e) => TargetSpecification = TargetTextBox.Text?.Trim() ?? string.Empty;

    private void TargetTextBox_EditValueChanged(object sender, EditValueChangedEventArgs e)
    {
        // Real-time validation could be added here if needed
    }
}
