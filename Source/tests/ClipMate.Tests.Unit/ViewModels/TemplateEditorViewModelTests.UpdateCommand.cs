using ClipMate.Core.Models;
using Moq;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Tests for TemplateEditorViewModel UpdateTemplateCommand.
/// </summary>
public partial class TemplateEditorViewModelTests
{
    [Test]
    public async Task UpdateTemplateCommand_WithSelectedTemplate_ShouldUpdateTemplate()
    {
        // Arrange
        var template = new Template
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            Content = "Original Content",
        };

        _mockTemplateService.Setup(p => p.UpdateAsync(It.IsAny<Template>(), CancellationToken.None))
            .Returns(Task.CompletedTask);

        var viewModel = CreateViewModel();
        viewModel.SelectedTemplate = template;
        viewModel.TemplateName = "Updated Name";
        viewModel.TemplateContent = "Updated Content";

        // Act
        await viewModel.UpdateTemplateCommand.ExecuteAsync(null);

        // Assert
        _mockTemplateService.Verify(p => p.UpdateAsync(
            It.Is<Template>(t => t.Name == "Updated Name" && t.Content == "Updated Content"),
            CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task UpdateTemplateCommand_WithNoSelectedTemplate_ShouldNotBeExecutable()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.SelectedTemplate = null;

        // Act
        var canExecute = viewModel.UpdateTemplateCommand.CanExecute(null);

        // Assert
        await Assert.That(canExecute).IsFalse();
    }
}
