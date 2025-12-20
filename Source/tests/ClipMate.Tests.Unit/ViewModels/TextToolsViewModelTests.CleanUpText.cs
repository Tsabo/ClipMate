using ClipMate.Core.Models;

namespace ClipMate.Tests.Unit.ViewModels;

public partial class TextToolsViewModelTests
{
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
}
