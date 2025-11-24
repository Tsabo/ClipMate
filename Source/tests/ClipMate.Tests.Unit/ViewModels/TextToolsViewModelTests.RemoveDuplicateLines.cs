using ClipMate.Core.Models;

namespace ClipMate.Tests.Unit.ViewModels;

public partial class TextToolsViewModelTests
{
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
}
