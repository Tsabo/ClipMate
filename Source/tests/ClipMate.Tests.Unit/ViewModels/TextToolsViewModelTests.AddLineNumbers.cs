using ClipMate.Core.Models;

namespace ClipMate.Tests.Unit.ViewModels;

public partial class TextToolsViewModelTests
{
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
}
