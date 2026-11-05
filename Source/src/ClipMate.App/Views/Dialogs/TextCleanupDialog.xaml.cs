using ClipMate.App.ViewModels;
using ClipMate.Core.Models.Configuration;
using MessageBox = System.Windows.MessageBox;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Text Cleanup Options dialog: the choices for cleaning up the text of clips.
/// </summary>
public partial class TextCleanupDialog
{
    private readonly TextCleanupDialogViewModel _viewModel;

    public TextCleanupDialog(TextCleanupDialogViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = viewModel;
        InitializeComponent();

        OkButton.Click += OkButton_Click;
    }

    /// <summary>
    /// Gets the choices made in the dialog.
    /// </summary>
    public TextCleanupConfiguration Options => _viewModel.Options;

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var problem = _viewModel.Validate();
        if (problem != null)
        {
            MessageBox.Show(this, problem, "Text Cleanup Options", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _ = _viewModel.SaveAsync();
        DialogResult = true;
        Close();
    }
}
