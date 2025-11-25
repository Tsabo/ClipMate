using System.Windows;
using DevExpress.Xpf.Core;
using ClipMate.App.ViewModels;
using ClipMate.Core.Services;

namespace ClipMate.App.Views;

/// <summary>
/// Custom emoji picker dialog with search, categories, and recently used tracking.
/// </summary>
public partial class EmojiPickerWindow : ThemedWindow
{
    private readonly EmojiPickerViewModel _viewModel;

    public string? SelectedEmoji => _viewModel.SelectedEmoji;

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

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        // Execute the OK command in the ViewModel
        _viewModel.OkCommand.Execute(null);
        DialogResult = true;
        Close();
    }
}
