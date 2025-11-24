using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Unit tests for TextToolsViewModel.
/// User Story 6: Text Processing Tools
/// </summary>
public class TextToolsViewModelTests
{
    private readonly TextTransformService _textTransformService;
    private readonly TextToolsViewModel _viewModel;

    public TextToolsViewModelTests()
    {
        _textTransformService = new TextTransformService();
        _viewModel = new TextToolsViewModel(_textTransformService);
    }

    #region Constructor Tests

    [Test]
    public async Task Constructor_WithValidService_ShouldCreateInstance()
    {
        // Arrange & Act
        var vm = new TextToolsViewModel(_textTransformService);

        // Assert
        await Assert.That(vm).IsNotNull();
        await Assert.That(vm.InputText).IsEmpty();
        await Assert.That(vm.OutputText).IsEmpty();
    }

    [Test]
    public async Task Constructor_WithNullService_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => new TextToolsViewModel(null!))
            .Throws<ArgumentNullException>();
    }

    #endregion

    #region Property Change Tests

    [Test]
    public async Task InputText_WhenChanged_ShouldRaisePropertyChanged()
    {
        // Arrange
        var propertyChangedRaised = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TextToolsViewModel.InputText))
                propertyChangedRaised = true;
        };

        // Act
        _viewModel.InputText = "Test input";

        // Assert
        await Assert.That(propertyChangedRaised).IsTrue();
        await Assert.That(_viewModel.InputText).IsEqualTo("Test input");
    }

    [Test]
    public async Task SelectedTool_WhenChanged_ShouldUpdateProperty()
    {
        // Arrange
        var initialTool = _viewModel.SelectedTool;

        // Act
        _viewModel.SelectedTool = TextTool.SortLines;

        // Assert
        await Assert.That(_viewModel.SelectedTool).IsEqualTo(TextTool.SortLines);
        await Assert.That(_viewModel.SelectedTool).IsNotEqualTo(initialTool);
    }

    #endregion

    #region ConvertCase Tests

    [Test]
    public async Task ApplyTransform_ConvertCaseUppercase_ShouldConvertToUppercase()
    {
        // Arrange
        _viewModel.InputText = "hello world";
        _viewModel.SelectedTool = TextTool.ConvertCase;
        _viewModel.CaseConversionMode = CaseConversion.Uppercase;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.OutputText).IsEqualTo("HELLO WORLD");
    }

    [Test]
    public async Task ApplyTransform_ConvertCaseLowercase_ShouldConvertToLowercase()
    {
        // Arrange
        _viewModel.InputText = "HELLO WORLD";
        _viewModel.SelectedTool = TextTool.ConvertCase;
        _viewModel.CaseConversionMode = CaseConversion.Lowercase;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.OutputText).IsEqualTo("hello world");
    }

    [Test]
    public async Task ApplyTransform_ConvertCaseTitleCase_ShouldConvertToTitleCase()
    {
        // Arrange
        _viewModel.InputText = "hello world";
        _viewModel.SelectedTool = TextTool.ConvertCase;
        _viewModel.CaseConversionMode = CaseConversion.TitleCase;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.OutputText).IsEqualTo("Hello World");
    }

    #endregion

    #region SortLines Tests

    [Test]
    public async Task ApplyTransform_SortLinesAlphabetically_ShouldSortLines()
    {
        // Arrange
        _viewModel.InputText = "Zebra\nApple\nBanana";
        _viewModel.SelectedTool = TextTool.SortLines;
        _viewModel.SortMode = SortMode.Alphabetical;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.OutputText).IsEqualTo("Apple\nBanana\nZebra");
    }

    [Test]
    public async Task ApplyTransform_SortLinesNumerically_ShouldSortNumerically()
    {
        // Arrange
        _viewModel.InputText = "10\n2\n100\n21";
        _viewModel.SelectedTool = TextTool.SortLines;
        _viewModel.SortMode = SortMode.Numerical;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.OutputText).IsEqualTo("2\n10\n21\n100");
    }

    #endregion

    #region RemoveDuplicateLines Tests

    [Test]
    public async Task ApplyTransform_RemoveDuplicateLines_ShouldRemoveDuplicates()
    {
        // Arrange
        _viewModel.InputText = "Apple\nBanana\nApple\nCherry";
        _viewModel.SelectedTool = TextTool.RemoveDuplicateLines;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.OutputText).IsEqualTo("Apple\nBanana\nCherry");
    }

    [Test]
    public async Task ApplyTransform_RemoveDuplicateLinesCaseSensitive_ShouldKeepDifferentCase()
    {
        // Arrange
        _viewModel.InputText = "Apple\napple\nAPPLE";
        _viewModel.SelectedTool = TextTool.RemoveDuplicateLines;
        _viewModel.CaseSensitive = true;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.OutputText).IsEqualTo("Apple\napple\nAPPLE");
    }

    #endregion

    #region AddLineNumbers Tests

    [Test]
    public async Task ApplyTransform_AddLineNumbers_ShouldAddNumbers()
    {
        // Arrange
        _viewModel.InputText = "Line one\nLine two\nLine three";
        _viewModel.SelectedTool = TextTool.AddLineNumbers;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.OutputText).IsEqualTo("1. Line one\n2. Line two\n3. Line three");
    }

    [Test]
    public async Task ApplyTransform_AddLineNumbersWithCustomFormat_ShouldUseFormat()
    {
        // Arrange
        _viewModel.InputText = "Line one\nLine two";
        _viewModel.SelectedTool = TextTool.AddLineNumbers;
        _viewModel.LineNumberFormat = "[{0:D3}] ";

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.OutputText).IsEqualTo("[001] Line one\n[002] Line two");
    }

    #endregion

    #region FindAndReplace Tests

    [Test]
    public async Task ApplyTransform_FindAndReplaceLiteral_ShouldReplaceText()
    {
        // Arrange
        _viewModel.InputText = "The quick brown fox";
        _viewModel.SelectedTool = TextTool.FindAndReplace;
        _viewModel.FindText = "quick";
        _viewModel.ReplaceText = "fast";

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.OutputText).IsEqualTo("The fast brown fox");
    }

    [Test]
    public async Task ApplyTransform_FindAndReplaceRegex_ShouldReplacePattern()
    {
        // Arrange
        _viewModel.InputText = "Contact: john@example.com";
        _viewModel.SelectedTool = TextTool.FindAndReplace;
        _viewModel.FindText = @"\S+@\S+\.\S+";
        _viewModel.ReplaceText = "[EMAIL]";
        _viewModel.UseRegex = true;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.OutputText).IsEqualTo("Contact: [EMAIL]");
    }

    #endregion

    #region CleanUpText Tests

    [Test]
    public async Task ApplyTransform_CleanUpTextRemoveSpaces_ShouldCollapseSpaces()
    {
        // Arrange
        _viewModel.InputText = "Text  with   multiple    spaces";
        _viewModel.SelectedTool = TextTool.CleanUpText;
        _viewModel.RemoveExtraSpaces = true;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.OutputText).IsEqualTo("Text with multiple spaces");
    }

    [Test]
    public async Task ApplyTransform_CleanUpTextTrimLines_ShouldTrimLines()
    {
        // Arrange
        _viewModel.InputText = "  Line 1  \n  Line 2  ";
        _viewModel.SelectedTool = TextTool.CleanUpText;
        _viewModel.TrimLines = true;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.OutputText).IsEqualTo("Line 1\nLine 2");
    }

    #endregion

    #region Command Tests

    [Test]
    public async Task ApplyTransformCommand_WithEmptyInput_ShouldSetOutputToEmpty()
    {
        // Arrange
        _viewModel.InputText = string.Empty;
        _viewModel.SelectedTool = TextTool.ConvertCase;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.OutputText).IsEmpty();
    }

    [Test]
    public async Task ClearCommand_ShouldClearInputAndOutput()
    {
        // Arrange
        _viewModel.InputText = "Test input";
        _viewModel.OutputText = "Test output";

        // Act
        _viewModel.ClearCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.InputText).IsEmpty();
        await Assert.That(_viewModel.OutputText).IsEmpty();
    }

    [Test]
    public async Task CopyToClipboardCommand_WithOutput_ShouldCopyOutput()
    {
        // Arrange
        _viewModel.OutputText = "Test output";

        // Act
        var canExecute = _viewModel.CopyToClipboardCommand.CanExecute(null);

        // Assert
        await Assert.That(canExecute).IsTrue();
    }

    [Test]
    public async Task CopyToClipboardCommand_WithEmptyOutput_ShouldNotExecute()
    {
        // Arrange
        _viewModel.OutputText = string.Empty;

        // Act
        var canExecute = _viewModel.CopyToClipboardCommand.CanExecute(null);

        // Assert
        await Assert.That(canExecute).IsFalse();
    }

    #endregion

    #region Preview Tests

    [Test]
    public async Task PreviewTransform_ShouldUpdateOutputWithoutApplying()
    {
        // Arrange
        _viewModel.InputText = "hello world";
        _viewModel.SelectedTool = TextTool.ConvertCase;
        _viewModel.CaseConversionMode = CaseConversion.Uppercase;

        // Act
        _viewModel.PreviewTransformCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.OutputText).IsEqualTo("HELLO WORLD");
    }

    [Test]
    public async Task SelectedTool_WhenChanged_ShouldAllowTransformation()
    {
        // Arrange
        _viewModel.InputText = "hello world";
        _viewModel.CaseConversionMode = CaseConversion.Uppercase;
        _viewModel.SelectedTool = TextTool.ConvertCase;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        await Assert.That(_viewModel.OutputText).IsEqualTo("HELLO WORLD");
    }

    #endregion
}
