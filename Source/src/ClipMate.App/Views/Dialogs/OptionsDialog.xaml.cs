using ClipMate.App.ViewModels;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Options dialog for configuring application settings.
/// </summary>
public partial class OptionsDialog
{
    public OptionsDialog(OptionsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var viewModel = (OptionsViewModel)DataContext;
            await viewModel.LoadConfigurationAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to load configuration: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
