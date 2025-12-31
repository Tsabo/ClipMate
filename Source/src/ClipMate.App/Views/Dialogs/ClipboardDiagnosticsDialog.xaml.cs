using ClipMate.App.ViewModels;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Dialog for displaying clipboard diagnostic information.
/// </summary>
public partial class ClipboardDiagnosticsDialog
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClipboardDiagnosticsDialog" /> class.
    /// </summary>
    /// <param name="viewModel">The view model.</param>
    public ClipboardDiagnosticsDialog(ClipboardDiagnosticsViewModel viewModel)
    {
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ClipboardDiagnosticsViewModel vm)
            vm.RefreshCommand.Execute(null);
    }
}
