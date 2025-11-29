using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using ClipMate.App.ViewModels;
using Wpf.Ui.Controls;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ClipMate.App.Views;

/// <summary>
/// ClipBar quick access window for clipboard history (Ctrl+Shift+V quick paste picker).
/// Provides a searchable popup for selecting and pasting individual clips.
/// This is distinct from the PowerPaste sequential automation feature.
/// </summary>
public partial class ClipBarWindow : FluentWindow
{
    private readonly ClipBarViewModel _viewModel;

    public ClipBarWindow(ClipBarViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        // Subscribe to close window flag
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClipBarViewModel.ShouldCloseWindow) && _viewModel.ShouldCloseWindow)
            Close();
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
