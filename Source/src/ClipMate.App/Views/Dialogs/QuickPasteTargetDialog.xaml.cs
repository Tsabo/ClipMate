using DevExpress.Xpf.Editors;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Dialog for adding/editing QuickPaste target specifications (PROCESSNAME:CLASSNAME), bad window titles, or program
/// names.
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
    /// Creates a dialog for a program name, such as an Outbound Filter exclusion.
    /// </summary>
    /// <param name="currentProgram">The program name to start from.</param>
    public static QuickPasteTargetDialog ForProgram(string currentProgram = "")
    {
        var dialog = new QuickPasteTargetDialog(currentProgram);
        dialog.SpecificationHelp.Visibility = Visibility.Collapsed;
        dialog.ProgramHelp.Visibility = Visibility.Visible;
        dialog.TargetItem.Label = "Program:";

        return dialog;
    }

    /// <summary>
    /// Gets what the user entered: a PROCESSNAME:CLASSNAME specification, or a window title in
    /// <see cref="ForWindowTitle" /> mode.
    /// </summary>
    public string EnteredText { get; private set; } = string.Empty;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TargetTextBox.Focus();
        TargetTextBox.SelectAll();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e) => EnteredText = TargetTextBox.Text?.Trim() ?? string.Empty;

    private void TargetTextBox_EditValueChanged(object sender, EditValueChangedEventArgs e)
    {
        // Real-time validation could be added here if needed
    }
}
