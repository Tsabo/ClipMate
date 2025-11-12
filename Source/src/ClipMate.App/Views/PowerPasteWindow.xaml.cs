using System.Windows;
using System.Windows.Input;
using ClipMate.App.ViewModels;

namespace ClipMate.App.Views;

/// <summary>
/// PowerPaste quick access window for clipboard history.
/// </summary>
public partial class PowerPasteWindow : Window
{
    private readonly PowerPasteViewModel _viewModel;

    public PowerPasteWindow(PowerPasteViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        // Subscribe to close window flag
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PowerPasteViewModel.ShouldCloseWindow) && _viewModel.ShouldCloseWindow)
        {
            Close();
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Load recent clips when window opens
        await _viewModel.LoadRecentClipsAsync();

        // Focus search box
        SearchTextBox.Focus();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
                _viewModel.NavigateUpCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Down:
                _viewModel.NavigateDownCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Enter:
                if (_viewModel.SelectedIndex >= 0 && _viewModel.SelectedIndex < _viewModel.FilteredClips.Count)
                {
                    var selectedClip = _viewModel.FilteredClips[_viewModel.SelectedIndex];
                    _viewModel.SelectClipCommand.Execute(selectedClip);
                }
                e.Handled = true;
                break;

            case Key.Escape:
                _viewModel.CancelCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        base.OnClosed(e);
    }
}
