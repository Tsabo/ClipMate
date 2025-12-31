using System.ComponentModel;
using ClipMate.App.ViewModels;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Modeless dialog for tracing paste operations.
/// </summary>
public partial class PasteTraceDialog
{
    private readonly PasteTraceViewModel _viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="PasteTraceDialog" /> class.
    /// </summary>
    /// <param name="viewModel">The view model.</param>
    public PasteTraceDialog(PasteTraceViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
        InitializeComponent();
    }

    private void OnClosing(object sender, CancelEventArgs e)
    {
        // Stop trace if running when dialog closes
        if (_viewModel.IsTracing && _viewModel.StopTraceCommand.CanExecute(null))
            _viewModel.StopTraceCommand.Execute(null);
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e) => Close();
}
