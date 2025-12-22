using ClipMate.App.ViewModels;
using ClipMate.Core.Services;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Custom emoji picker dialog with search, categories, and recently used tracking.
/// </summary>
public partial class EmojiPickerWindow
{
    private readonly EmojiPickerViewModel _viewModel;

    public EmojiPickerWindow(IConfigurationService configurationService)
    {
        InitializeComponent();
        _viewModel = new EmojiPickerViewModel(configurationService);
        DataContext = _viewModel;
    }

    public EmojiPickerWindow(IConfigurationService configurationService, string? currentEmoji)
        : this(configurationService)
    {
        _viewModel.SelectedEmoji = currentEmoji;
    }

    public string? SelectedEmoji => _viewModel.SelectedEmoji;

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        // Execute the OK command in the ViewModel
        _viewModel.OkCommand.Execute(null);
        DialogResult = true;
        Close();
    }
}
