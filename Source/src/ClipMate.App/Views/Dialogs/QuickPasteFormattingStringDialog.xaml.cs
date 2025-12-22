using ClipMate.Core.Models.Configuration;
using DevExpress.Xpf.Editors;
using MessageBox = System.Windows.MessageBox;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Dialog for creating/editing QuickPaste formatting strings.
/// </summary>
public partial class QuickPasteFormattingStringDialog
{
    public QuickPasteFormattingStringDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        UpdatePreview();
    }

    /// <summary>
    /// Creates a dialog with a pre-filled formatting string.
    /// </summary>
    /// <param name="formattingString">The formatting string to edit.</param>
    public QuickPasteFormattingStringDialog(QuickPasteFormattingString? formattingString)
        : this()
    {
        if (formattingString == null)
            return;

        TitleTextBox.Text = formattingString.Title;
        PreambleTextBox.Text = formattingString.Preamble;
        PasteKeystrokesTextBox.Text = formattingString.PasteKeystrokes;
        PostambleTextBox.Text = formattingString.Postamble;
        TitleTriggerTextBox.Text = formattingString.TitleTrigger;
        UpdatePreview();
    }

    /// <summary>
    /// Gets the formatting string created/edited by the user.
    /// </summary>
    public QuickPasteFormattingString? FormattingString { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e) => TitleTextBox.Focus();

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        FormattingString = new QuickPasteFormattingString
        {
            Title = TitleTextBox.Text?.Trim() ?? string.Empty,
            Preamble = PreambleTextBox.Text ?? string.Empty,
            PasteKeystrokes = PasteKeystrokesTextBox.Text ?? string.Empty,
            Postamble = PostambleTextBox.Text ?? string.Empty,
            TitleTrigger = TitleTriggerTextBox.Text ?? string.Empty,
        };
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "QuickPaste Formatting Strings allow you to customize how clips are pasted.\n\n" +
            "Modifiers:\n" +
            "  ^ = Ctrl key\n" +
            "  ~ = Alt key\n" +
            "  @ = Shift key\n\n" +
            "Special Keys:\n" +
            "  {TAB}, {ENTER}, {ESC}, {HOME}, {END}, {DELETE}, {INSERT}, etc.\n\n" +
            "Macros:\n" +
            "  #DATE# - Current date\n" +
            "  #TIME# - Current time\n" +
            "  #URL# - Clip URL\n" +
            "  #TITLE# - Clip title\n" +
            "  #CREATOR# - Clip creator\n" +
            "  #SEQUENCE# - Auto-incrementing number\n" +
            "  #PAUSE# - 10ms delay\n\n" +
            "Example:\n" +
            "  Preamble: (empty)\n" +
            "  Paste Keys: ^v\n" +
            "  Postamble: {TAB}#DATE#{ENTER}\n" +
            "  Result: Pastes clip, presses TAB, inserts date, presses ENTER",
            "QuickPaste Formatting String Help",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OnFieldChanged(object sender, EditValueChangedEventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        var format = new QuickPasteFormattingString
        {
            Title = TitleTextBox.Text ?? string.Empty,
            Preamble = PreambleTextBox.Text ?? string.Empty,
            PasteKeystrokes = PasteKeystrokesTextBox.Text ?? string.Empty,
            Postamble = PostambleTextBox.Text ?? string.Empty,
            TitleTrigger = TitleTriggerTextBox.Text ?? string.Empty,
        };

        PreviewTextBox.Text = format.ToRegistryFormat();
    }
}
