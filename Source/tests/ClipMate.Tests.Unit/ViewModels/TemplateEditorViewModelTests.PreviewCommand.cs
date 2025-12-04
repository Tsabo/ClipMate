using ClipMate.Core.Models;
using Moq;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Tests for TemplateEditorViewModel PreviewTemplateCommand.
/// </summary>
public partial class TemplateEditorViewModelTests
{
    [Test]
    public async Task PreviewTemplateCommand_WithValidContent_ShouldExpandVariables()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var expandedContent = $"Hello World! Today is {DateTime.Now:yyyy-MM-dd}";

        _mockTemplateService.Setup(p => p.ExpandTemplateAsync(templateId, It.IsAny<Dictionary<string, string>>(), CancellationToken.None))
            .ReturnsAsync(expandedContent);

        var viewModel = CreateViewModel();
        viewModel.SelectedTemplate = new Template
        {
            Id = templateId,
            Content = "Hello {NAME}! Today is {DATE:yyyy-MM-dd}",
        };

        // Act
        await viewModel.PreviewTemplateCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(viewModel.PreviewText).IsNotEmpty();
    }

    [Test]
    public async Task PreviewTemplateCommand_WithNoSelectedTemplate_ShouldNotBeExecutable()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.SelectedTemplate = null;

        // Act
        var canExecute = viewModel.PreviewTemplateCommand.CanExecute(null);

        // Assert
        await Assert.That(canExecute).IsFalse();
    }
}
