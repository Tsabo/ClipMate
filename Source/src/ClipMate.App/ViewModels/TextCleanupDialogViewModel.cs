using System.Windows;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MessageBox = System.Windows.MessageBox;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the Text Cleanup dialog.
/// </summary>
public partial class TextCleanupDialogViewModel : ObservableObject
{
    private readonly ILogger<TextCleanupDialogViewModel> _logger;
    private readonly ITextTransformService _textTransformService;

    [ObservableProperty]
    private string _caseConversion = "NoChange";

    [ObservableProperty]
    private bool _caseSensitive;

    [ObservableProperty]
    private string _charactersToStrip = string.Empty;

    [ObservableProperty]
    private string _findText = string.Empty;

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private string _lineBreakMode = "KeepAll";

    [ObservableProperty]
    private bool _removeExtraLineBreaks;

    [ObservableProperty]
    private bool _removeExtraSpaces;

    [ObservableProperty]
    private string _replaceText = string.Empty;

    [ObservableProperty]
    private string _stripPosition = "Leading";

    [ObservableProperty]
    private bool _stripWhitespace;

    [ObservableProperty]
    private bool _trimLines;

    [ObservableProperty]
    private bool _useRegex;

    public TextCleanupDialogViewModel(ITextTransformService textTransformService, ILogger<TextCleanupDialogViewModel> logger)
    {
        _textTransformService = textTransformService ?? throw new ArgumentNullException(nameof(textTransformService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string ResultText { get; private set; } = string.Empty;

    public event EventHandler<bool>? CloseRequested;

    public void SetInputText(string text) => InputText = text;

    [RelayCommand]
    private void Apply()
    {
        try
        {
            var result = InputText;

            // Step 1: Strip characters
            if (!string.IsNullOrEmpty(CharactersToStrip))
            {
                var position = StripPosition switch
                {
                    "Leading" => Core.Models.StripPosition.Leading,
                    "Trailing" => Core.Models.StripPosition.Trailing,
                    "Anywhere" => Core.Models.StripPosition.Anywhere,
                    _ => Core.Models.StripPosition.Leading
                };

                result = _textTransformService.StripCharacters(result, CharactersToStrip, position);
            }

            if (StripWhitespace)
                result = _textTransformService.TrimText(result);

            // Step 2: Formatting
            if (RemoveExtraSpaces || RemoveExtraLineBreaks || TrimLines)
                result = _textTransformService.CleanUpText(result, RemoveExtraSpaces, RemoveExtraLineBreaks, TrimLines);

            if (LineBreakMode != "KeepAll")
            {
                var mode = LineBreakMode switch
                {
                    "PreserveParagraphs" => Core.Models.LineBreakMode.PreserveParagraphs,
                    "RemoveAll" => Core.Models.LineBreakMode.RemoveAll,
                    _ => Core.Models.LineBreakMode.PreserveParagraphs
                };

                result = _textTransformService.RemoveLineBreaks(result, mode);
            }

            // Step 3: Case conversion
            if (CaseConversion != "NoChange")
            {
                var conversion = CaseConversion switch
                {
                    "Uppercase" => Core.Models.CaseConversion.Uppercase,
                    "Lowercase" => Core.Models.CaseConversion.Lowercase,
                    "TitleCase" => Core.Models.CaseConversion.TitleCase,
                    "SentenceCase" => Core.Models.CaseConversion.SentenceCase,
                    "InvertCase" => Core.Models.CaseConversion.InvertCase,
                    _ => Core.Models.CaseConversion.Uppercase
                };

                result = _textTransformService.ConvertCase(result, conversion);
            }

            // Step 4: Find and replace
            if (!string.IsNullOrEmpty(FindText))
                result = _textTransformService.FindAndReplace(result, FindText, ReplaceText, UseRegex, CaseSensitive);

            ResultText = result;
            _logger.LogDebug("Text cleanup applied successfully");
            CloseRequested?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying text cleanup");
            MessageBox.Show($"Error applying text cleanup: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);
}
