using ClipMate.Core.Models;

namespace ClipMate.Tests.Unit.ViewModels;

public partial class TextToolsViewModelTests
{
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
}
