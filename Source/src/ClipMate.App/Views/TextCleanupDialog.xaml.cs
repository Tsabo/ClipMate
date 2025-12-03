using System.Windows;
using System.Windows.Controls;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using DevExpress.Xpf.Editors;
using Microsoft.Extensions.Logging;
using Wpf.Ui.Controls;

namespace ClipMate.App.Views;

/// <summary>
/// Text Cleanup dialog for advanced text transformations.
/// </summary>
public partial class TextCleanupDialog : FluentWindow
{
    private readonly ITextTransformService _textTransformService;
    private readonly ILogger<TextCleanupDialog> _logger;
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
    /// Sets the input text to be cleaned up.
    /// </summary>
    public void SetInputText(string text)
    {
        _inputText = text;
    }

    /// <summary>
    /// Gets the result text after cleanup operations.
    /// </summary>
    public string? ResultText { get; private set; }

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
                    _ => StripPosition.Leading
                };
                result = _textTransformService.StripCharacters(result, CharactersToStripTextEdit.Text, position);
            }

            if (StripWhitespaceCheckBox.IsChecked == true)
            {
                result = result.Trim();
            }

            // Step 2: Formatting
            if (RemoveExtraSpacesCheckBox.IsChecked == true)
            {
                result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ");
            }

            if (TrimLinesCheckBox.IsChecked == true)
            {
                result = _textTransformService.TrimText(result);
            }

            if (RemoveExtraLineBreaksCheckBox.IsChecked == true)
            {
                var selectedItem = (ComboBoxItem?)LineBreakModeComboBox.SelectedItem;
                var mode = selectedItem?.Tag?.ToString() switch
                {
                    "PreserveParagraphs" => LineBreakMode.PreserveParagraphs,
                    "RemoveAll" => LineBreakMode.RemoveAll,
                    _ => LineBreakMode.PreserveParagraphs
                };
                result = _textTransformService.RemoveLineBreaks(result, mode);
            }

            // Step 3: Case conversion
            var caseItem = (ComboBoxItem?)CaseConversionComboBox.SelectedItem;
            var caseConversion = caseItem?.Tag?.ToString() switch
            {
                "Uppercase" => CaseConversion.Uppercase,
                "Lowercase" => CaseConversion.Lowercase,
                "TitleCase" => CaseConversion.TitleCase,
                "SentenceCase" => CaseConversion.SentenceCase,
                "InvertCase" => CaseConversion.InvertCase,
                _ => (CaseConversion?)null
            };

            if (caseConversion.HasValue)
            {
                result = _textTransformService.ConvertCase(result, caseConversion.Value);
            }

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
            System.Windows.MessageBox.Show($"Error applying text cleanup: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
