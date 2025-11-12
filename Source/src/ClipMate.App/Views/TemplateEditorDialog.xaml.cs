using System.Windows;
using ClipMate.App.ViewModels;

namespace ClipMate.App.Views;

/// <summary>
/// Interaction logic for TemplateEditorDialog.xaml
/// </summary>
public partial class TemplateEditorDialog : Window
{
    public TemplateEditorDialog()
    {
        InitializeComponent();
    }

    public TemplateEditorDialog(TemplateEditorViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is TemplateEditorViewModel viewModel)
        {
            await viewModel.LoadTemplatesCommand.ExecuteAsync(null);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
