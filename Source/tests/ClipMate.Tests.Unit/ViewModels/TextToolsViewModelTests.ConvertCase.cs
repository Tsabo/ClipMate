using ClipMate.Core.Models;

namespace ClipMate.Tests.Unit.ViewModels;

public partial class TextToolsViewModelTests
{
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
}
