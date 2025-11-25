using ClipMate.Core.Models;

namespace ClipMate.Tests.Unit.ViewModels;

public partial class TextToolsViewModelTests
{
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
}
