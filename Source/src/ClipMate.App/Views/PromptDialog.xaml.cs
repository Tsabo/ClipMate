using System.Windows;
using System.Windows.Input;

namespace ClipMate.App.Views;

/// <summary>
/// Interaction logic for PromptDialog.xaml
/// Provides a simple input dialog for {PROMPT:label} template variables.
/// </summary>
public partial class PromptDialog : Window
{
    /// <summary>
    /// Gets the user's input value. Null if dialog was cancelled.
    /// </summary>
    public string? UserInput { get; private set; }

    public PromptDialog()
    {
        InitializeComponent();
    }

    public PromptDialog(string promptLabel) : this()
    {
        PromptLabel.Text = promptLabel;
        Title = $"Enter {promptLabel}";
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        InputTextBox.Focus();
        InputTextBox.SelectAll();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        UserInput = InputTextBox.Text;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        UserInput = null;
        DialogResult = false;
        Close();
    }

    private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OkButton_Click(sender, e);
        }
        else if (e.Key == Key.Escape)
        {
            CancelButton_Click(sender, e);
        }
    }
}
