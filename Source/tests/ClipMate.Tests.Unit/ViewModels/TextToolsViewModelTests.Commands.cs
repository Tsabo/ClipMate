using ClipMate.Core.Models;

namespace ClipMate.Tests.Unit.ViewModels;

public partial class TextToolsViewModelTests
{
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
}
