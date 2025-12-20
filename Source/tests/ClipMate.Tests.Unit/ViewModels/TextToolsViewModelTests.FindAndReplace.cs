using ClipMate.Core.Models;

namespace ClipMate.Tests.Unit.ViewModels;

public partial class TextToolsViewModelTests
{
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
}
