using ClipMate.App.ViewModels;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// About dialog showing version, credits, and project information.
/// </summary>
public partial class AboutDialog
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AboutDialog"/> class.
    /// </summary>
    /// <param name="viewModel">The view model.</param>
    public AboutDialog(AboutDialogViewModel viewModel)
    {
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
    }
}
