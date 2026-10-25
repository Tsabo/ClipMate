using ClipMate.App.ViewModels;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Capture Special: choose which Formats on the clipboard to capture.
/// </summary>
public partial class CaptureSpecialDialog
{
    public CaptureSpecialDialog(CaptureSpecialViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
