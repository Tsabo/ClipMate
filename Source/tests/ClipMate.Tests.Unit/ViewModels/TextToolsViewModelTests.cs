using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using Shouldly;
using Xunit;

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

    [Fact]
    public void Constructor_WithValidService_ShouldCreateInstance()
    {
        // Arrange & Act
        var vm = new TextToolsViewModel(_textTransformService);

        // Assert
        vm.ShouldNotBeNull();
        vm.InputText.ShouldBeEmpty();
        vm.OutputText.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WithNullService_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new TextToolsViewModel(null!));
    }

    #endregion

    #region Property Change Tests

    [Fact]
    public void InputText_WhenChanged_ShouldRaisePropertyChanged()
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
        propertyChangedRaised.ShouldBeTrue();
        _viewModel.InputText.ShouldBe("Test input");
    }

    [Fact]
    public void SelectedTool_WhenChanged_ShouldUpdateProperty()
    {
        // Arrange
        var initialTool = _viewModel.SelectedTool;

        // Act
        _viewModel.SelectedTool = TextTool.SortLines;

        // Assert
        _viewModel.SelectedTool.ShouldBe(TextTool.SortLines);
        _viewModel.SelectedTool.ShouldNotBe(initialTool);
    }

    #endregion

    #region ConvertCase Tests

    [Fact]
    public void ApplyTransform_ConvertCaseUppercase_ShouldConvertToUppercase()
    {
        // Arrange
        _viewModel.InputText = "hello world";
        _viewModel.SelectedTool = TextTool.ConvertCase;
        _viewModel.CaseConversionMode = CaseConversion.Uppercase;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        _viewModel.OutputText.ShouldBe("HELLO WORLD");
    }

    [Fact]
    public void ApplyTransform_ConvertCaseLowercase_ShouldConvertToLowercase()
    {
        // Arrange
        _viewModel.InputText = "HELLO WORLD";
        _viewModel.SelectedTool = TextTool.ConvertCase;
        _viewModel.CaseConversionMode = CaseConversion.Lowercase;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        _viewModel.OutputText.ShouldBe("hello world");
    }

    [Fact]
    public void ApplyTransform_ConvertCaseTitleCase_ShouldConvertToTitleCase()
    {
        // Arrange
        _viewModel.InputText = "hello world";
        _viewModel.SelectedTool = TextTool.ConvertCase;
        _viewModel.CaseConversionMode = CaseConversion.TitleCase;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        _viewModel.OutputText.ShouldBe("Hello World");
    }

    #endregion

    #region SortLines Tests

    [Fact]
    public void ApplyTransform_SortLinesAlphabetically_ShouldSortLines()
    {
        // Arrange
        _viewModel.InputText = "Zebra\nApple\nBanana";
        _viewModel.SelectedTool = TextTool.SortLines;
        _viewModel.SortMode = SortMode.Alphabetical;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        _viewModel.OutputText.ShouldBe("Apple\nBanana\nZebra");
    }

    [Fact]
    public void ApplyTransform_SortLinesNumerically_ShouldSortNumerically()
    {
        // Arrange
        _viewModel.InputText = "10\n2\n100\n21";
        _viewModel.SelectedTool = TextTool.SortLines;
        _viewModel.SortMode = SortMode.Numerical;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        _viewModel.OutputText.ShouldBe("2\n10\n21\n100");
    }

    #endregion

    #region RemoveDuplicateLines Tests

    [Fact]
    public void ApplyTransform_RemoveDuplicateLines_ShouldRemoveDuplicates()
    {
        // Arrange
        _viewModel.InputText = "Apple\nBanana\nApple\nCherry";
        _viewModel.SelectedTool = TextTool.RemoveDuplicateLines;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        _viewModel.OutputText.ShouldBe("Apple\nBanana\nCherry");
    }

    [Fact]
    public void ApplyTransform_RemoveDuplicateLinesCaseSensitive_ShouldKeepDifferentCase()
    {
        // Arrange
        _viewModel.InputText = "Apple\napple\nAPPLE";
        _viewModel.SelectedTool = TextTool.RemoveDuplicateLines;
        _viewModel.CaseSensitive = true;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        _viewModel.OutputText.ShouldBe("Apple\napple\nAPPLE");
    }

    #endregion

    #region AddLineNumbers Tests

    [Fact]
    public void ApplyTransform_AddLineNumbers_ShouldAddNumbers()
    {
        // Arrange
        _viewModel.InputText = "Line one\nLine two\nLine three";
        _viewModel.SelectedTool = TextTool.AddLineNumbers;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        _viewModel.OutputText.ShouldBe("1. Line one\n2. Line two\n3. Line three");
    }

    [Fact]
    public void ApplyTransform_AddLineNumbersWithCustomFormat_ShouldUseFormat()
    {
        // Arrange
        _viewModel.InputText = "Line one\nLine two";
        _viewModel.SelectedTool = TextTool.AddLineNumbers;
        _viewModel.LineNumberFormat = "[{0:D3}] ";

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        _viewModel.OutputText.ShouldBe("[001] Line one\n[002] Line two");
    }

    #endregion

    #region FindAndReplace Tests

    [Fact]
    public void ApplyTransform_FindAndReplaceLiteral_ShouldReplaceText()
    {
        // Arrange
        _viewModel.InputText = "The quick brown fox";
        _viewModel.SelectedTool = TextTool.FindAndReplace;
        _viewModel.FindText = "quick";
        _viewModel.ReplaceText = "fast";

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        _viewModel.OutputText.ShouldBe("The fast brown fox");
    }

    [Fact]
    public void ApplyTransform_FindAndReplaceRegex_ShouldReplacePattern()
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
        _viewModel.OutputText.ShouldBe("Contact: [EMAIL]");
    }

    #endregion

    #region CleanUpText Tests

    [Fact]
    public void ApplyTransform_CleanUpTextRemoveSpaces_ShouldCollapseSpaces()
    {
        // Arrange
        _viewModel.InputText = "Text  with   multiple    spaces";
        _viewModel.SelectedTool = TextTool.CleanUpText;
        _viewModel.RemoveExtraSpaces = true;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        _viewModel.OutputText.ShouldBe("Text with multiple spaces");
    }

    [Fact]
    public void ApplyTransform_CleanUpTextTrimLines_ShouldTrimLines()
    {
        // Arrange
        _viewModel.InputText = "  Line 1  \n  Line 2  ";
        _viewModel.SelectedTool = TextTool.CleanUpText;
        _viewModel.TrimLines = true;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        _viewModel.OutputText.ShouldBe("Line 1\nLine 2");
    }

    #endregion

    #region Command Tests

    [Fact]
    public void ApplyTransformCommand_WithEmptyInput_ShouldSetOutputToEmpty()
    {
        // Arrange
        _viewModel.InputText = string.Empty;
        _viewModel.SelectedTool = TextTool.ConvertCase;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        _viewModel.OutputText.ShouldBeEmpty();
    }

    [Fact]
    public void ClearCommand_ShouldClearInputAndOutput()
    {
        // Arrange
        _viewModel.InputText = "Test input";
        _viewModel.OutputText = "Test output";

        // Act
        _viewModel.ClearCommand.Execute(null);

        // Assert
        _viewModel.InputText.ShouldBeEmpty();
        _viewModel.OutputText.ShouldBeEmpty();
    }

    [Fact]
    public void CopyToClipboardCommand_WithOutput_ShouldCopyOutput()
    {
        // Arrange
        _viewModel.OutputText = "Test output";

        // Act
        var canExecute = _viewModel.CopyToClipboardCommand.CanExecute(null);

        // Assert
        canExecute.ShouldBeTrue();
    }

    [Fact]
    public void CopyToClipboardCommand_WithEmptyOutput_ShouldNotExecute()
    {
        // Arrange
        _viewModel.OutputText = string.Empty;

        // Act
        var canExecute = _viewModel.CopyToClipboardCommand.CanExecute(null);

        // Assert
        canExecute.ShouldBeFalse();
    }

    #endregion

    #region Preview Tests

    [Fact]
    public void PreviewTransform_ShouldUpdateOutputWithoutApplying()
    {
        // Arrange
        _viewModel.InputText = "hello world";
        _viewModel.SelectedTool = TextTool.ConvertCase;
        _viewModel.CaseConversionMode = CaseConversion.Uppercase;

        // Act
        _viewModel.PreviewTransformCommand.Execute(null);

        // Assert
        _viewModel.OutputText.ShouldBe("HELLO WORLD");
    }

    [Fact]
    public void SelectedTool_WhenChanged_ShouldAllowTransformation()
    {
        // Arrange
        _viewModel.InputText = "hello world";
        _viewModel.CaseConversionMode = CaseConversion.Uppercase;
        _viewModel.SelectedTool = TextTool.ConvertCase;

        // Act
        _viewModel.ApplyTransformCommand.Execute(null);

        // Assert
        _viewModel.OutputText.ShouldBe("HELLO WORLD");
    }

    #endregion
}
