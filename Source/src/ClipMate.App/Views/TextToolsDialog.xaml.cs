using System.Windows;
using ClipMate.App.ViewModels;
using Wpf.Ui.Controls;

namespace ClipMate.App.Views;

/// <summary>
/// Interaction logic for TextToolsDialog.xaml
/// </summary>
public partial class TextToolsDialog : FluentWindow
{
    public TextToolsDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Constructor for dependency injection with ViewModel.
    /// </summary>
    /// <param name="viewModel">The TextToolsViewModel instance.</param>
    public TextToolsDialog(TextToolsViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
