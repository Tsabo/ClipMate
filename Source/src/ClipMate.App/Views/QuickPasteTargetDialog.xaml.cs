using System.Windows;
using DevExpress.Xpf.Editors;

namespace ClipMate.App.Views;

/// <summary>
/// Dialog for adding/editing QuickPaste target specifications (PROCESSNAME:CLASSNAME).
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
