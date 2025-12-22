using System.Text.RegularExpressions;
using System.Windows.Controls;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;
using MessageBox = System.Windows.MessageBox;

namespace ClipMate.App.Views.Dialogs;

/// <summary>
/// Text Cleanup dialog for advanced text transformations.
/// </summary>
public partial class TextCleanupDialog
{
    private readonly ILogger<TextCleanupDialog> _logger;
    private readonly ITextTransformService _textTransformService;
    private string _inputText = string.Empty;

    public TextCleanupDialog(ITextTransformService textTransformService, ILogger<TextCleanupDialog> logger)
    {
        InitializeComponent();
        _textTransformService = textTransformService ?? throw new ArgumentNullException(nameof(textTransformService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Wire up Apply button click
        ApplyButton.Click += ApplyButton_Click;
        CancelButton.Click += CancelButton_Click;
    }

    /// <summary>
    /// Gets the result text after cleanup operations.
    /// </summary>
    public string? ResultText { get; private set; }

    /// <summary>
    /// Sets the input text to be cleaned up.
    /// </summary>
    public void SetInputText(string text) => _inputText = text;

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = _inputText;

            // Step 1: Strip characters
            if (!string.IsNullOrEmpty(CharactersToStripTextEdit.Text))
            {
                var selectedItem = (ComboBoxItem?)StripPositionComboBox.SelectedItem;
                var position = selectedItem?.Tag?.ToString() switch
                {
                    "Leading" => StripPosition.Leading,
                    "Trailing" => StripPosition.Trailing,
                    "Anywhere" => StripPosition.Anywhere,
                    var _ => StripPosition.Leading,
                };

                result = _textTransformService.StripCharacters(result, CharactersToStripTextEdit.Text, position);
            }

            if (StripWhitespaceCheckBox.IsChecked == true)
                result = result.Trim();

            // Step 2: Formatting
            if (RemoveExtraSpacesCheckBox.IsChecked == true)
                result = Regex.Replace(result, @"\s+", " ");

            if (TrimLinesCheckBox.IsChecked == true)
                result = _textTransformService.TrimText(result);

            if (RemoveExtraLineBreaksCheckBox.IsChecked == true)
            {
                var selectedItem = (ComboBoxItem?)LineBreakModeComboBox.SelectedItem;
                var mode = selectedItem?.Tag?.ToString() switch
                {
                    "PreserveParagraphs" => LineBreakMode.PreserveParagraphs,
                    "RemoveAll" => LineBreakMode.RemoveAll,
                    var _ => LineBreakMode.PreserveParagraphs,
                };

                result = _textTransformService.RemoveLineBreaks(result, mode);
            }

            // Step 3: Case conversion
            var caseItem = (ComboBoxItem?)CaseConversionComboBox.SelectedItem;
            CaseConversion? caseConversion = caseItem?.Tag?.ToString() switch
            {
                "Uppercase" => CaseConversion.Uppercase,
                "Lowercase" => CaseConversion.Lowercase,
                "TitleCase" => CaseConversion.TitleCase,
                "SentenceCase" => CaseConversion.SentenceCase,
                "InvertCase" => CaseConversion.InvertCase,
                var _ => null,
            };

            if (caseConversion.HasValue)
                result = _textTransformService.ConvertCase(result, caseConversion.Value);

            // Step 4: Find and replace
            if (!string.IsNullOrEmpty(FindTextEdit.Text))
            {
                result = _textTransformService.FindAndReplace(
                    result,
                    FindTextEdit.Text,
                    ReplaceTextEdit.Text ?? string.Empty,
                    UseRegexCheckBox.IsChecked == true,
                    CaseSensitiveCheckBox.IsChecked == true);
            }

            ResultText = result;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying text cleanup");
            MessageBox.Show($"Error applying text cleanup: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
