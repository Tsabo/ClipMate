using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClipMate.Core.Models;
using ClipMate.Core.Services;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the Text Tools dialog.
/// User Story 6: Text Processing Tools
/// </summary>
public partial class TextToolsViewModel : ObservableObject
{
    private readonly TextTransformService _textTransformService;

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private string _outputText = string.Empty;

    [ObservableProperty]
    private TextTool _selectedTool = TextTool.ConvertCase;

    [ObservableProperty]
    private CaseConversion _caseConversionMode = CaseConversion.Uppercase;

    [ObservableProperty]
    private SortMode _sortMode = SortMode.Alphabetical;

    [ObservableProperty]
    private bool _caseSensitive = false;

    [ObservableProperty]
    private string _lineNumberFormat = "{0}. ";

    [ObservableProperty]
    private string _findText = string.Empty;

    [ObservableProperty]
    private string _replaceText = string.Empty;

    [ObservableProperty]
    private bool _useRegex = false;

    [ObservableProperty]
    private bool _removeExtraSpaces = false;

    [ObservableProperty]
    private bool _removeExtraLineBreaks = false;

    [ObservableProperty]
    private bool _trimLines = false;

    [ObservableProperty]
    private TextFormat _sourceFormat = TextFormat.Plain;

    [ObservableProperty]
    private TextFormat _targetFormat = TextFormat.Html;

    /// <summary>
    /// Initializes a new instance of the TextToolsViewModel class.
    /// </summary>
    /// <param name="textTransformService">The text transformation service.</param>
    /// <exception cref="ArgumentNullException">Thrown when textTransformService is null.</exception>
    public TextToolsViewModel(TextTransformService textTransformService)
    {
        _textTransformService = textTransformService ?? throw new ArgumentNullException(nameof(textTransformService));
        
        // Subscribe to property changes to trigger auto-preview
        PropertyChanged += OnPropertyChanged;
    }

    /// <summary>
    /// Applies the selected transformation to the input text.
    /// </summary>
    [RelayCommand]
    private void ApplyTransform()
    {
        if (string.IsNullOrEmpty(InputText))
        {
            OutputText = string.Empty;
            return;
        }

        try
        {
            OutputText = SelectedTool switch
            {
                TextTool.ConvertCase => _textTransformService.ConvertCase(InputText, CaseConversionMode),
                TextTool.SortLines => _textTransformService.SortLines(InputText, SortMode),
                TextTool.RemoveDuplicateLines => _textTransformService.RemoveDuplicateLines(InputText, CaseSensitive),
                TextTool.AddLineNumbers => _textTransformService.AddLineNumbers(InputText, LineNumberFormat),
                TextTool.FindAndReplace => _textTransformService.FindAndReplace(
                    InputText, FindText, ReplaceText, UseRegex, CaseSensitive),
                TextTool.CleanUpText => _textTransformService.CleanUpText(
                    InputText, RemoveExtraSpaces, RemoveExtraLineBreaks, TrimLines),
                TextTool.ConvertFormat => _textTransformService.ConvertFormat(InputText, SourceFormat, TargetFormat),
                _ => InputText
            };
        }
        catch (ArgumentException ex)
        {
            // Handle invalid regex patterns or other errors
            OutputText = $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Previews the transformation without applying it.
    /// </summary>
    [RelayCommand]
    private void PreviewTransform()
    {
        ApplyTransform();
    }

    /// <summary>
    /// Clears both input and output text.
    /// </summary>
    [RelayCommand]
    private void Clear()
    {
        InputText = string.Empty;
        OutputText = string.Empty;
    }

    /// <summary>
    /// Copies the output text to the clipboard.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCopyToClipboard))]
    private void CopyToClipboard()
    {
        if (!string.IsNullOrEmpty(OutputText))
        {
            try
            {
                Clipboard.SetText(OutputText);
            }
            catch
            {
                // Clipboard operations can fail - ignore for unit tests
            }
        }
    }

    private bool CanCopyToClipboard()
    {
        return !string.IsNullOrEmpty(OutputText);
    }

    /// <summary>
    /// Swaps input and output text (useful for chaining transformations).
    /// </summary>
    [RelayCommand]
    private void SwapInputOutput()
    {
        (InputText, OutputText) = (OutputText, InputText);
    }

    /// <summary>
    /// Handles property changes to trigger auto-preview.
    /// </summary>
    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Auto-preview when tool selection changes or input changes
        if (e.PropertyName == nameof(SelectedTool) && !string.IsNullOrEmpty(InputText))
        {
            ApplyTransform();
        }
    }

    partial void OnOutputTextChanged(string value)
    {
        // Notify CopyToClipboardCommand that CanExecute might have changed
        CopyToClipboardCommand.NotifyCanExecuteChanged();
    }
}
