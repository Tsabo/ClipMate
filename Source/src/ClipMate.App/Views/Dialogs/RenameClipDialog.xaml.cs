namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Interaction logic for RenameClipDialog.xaml
/// </summary>
public partial class RenameClipDialog
{
    public RenameClipDialog()
    {
        InitializeComponent();

        // Focus the Title field when dialog opens
        Loaded += (_, _) => TitleTextBox.Focus();
    }
}
