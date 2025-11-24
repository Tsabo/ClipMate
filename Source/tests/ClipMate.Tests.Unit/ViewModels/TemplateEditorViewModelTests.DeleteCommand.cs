using ClipMate.Core.Models;
using Moq;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Tests for TemplateEditorViewModel DeleteTemplateCommand.
/// </summary>
public partial class TemplateEditorViewModelTests
{
    [Test]
    public async Task DeleteTemplateCommand_WithSelectedTemplate_ShouldDeleteTemplate()
    {
        // Arrange
        var template = new Template
        {
            Id = Guid.NewGuid(),
            Name = "Template to Delete",
            Content = "Content"
        };

        _mockTemplateService.Setup(s => s.DeleteAsync(template.Id, default))
            .Returns(Task.CompletedTask);

        var viewModel = CreateViewModel();
        viewModel.SelectedTemplate = template;

        // Act
        await viewModel.DeleteTemplateCommand.ExecuteAsync(null);

        // Assert
        _mockTemplateService.Verify(s => s.DeleteAsync(template.Id, default), Times.Once);
    }

    [Test]
    public async Task DeleteTemplateCommand_WithNoSelectedTemplate_ShouldNotBeExecutable()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.SelectedTemplate = null;

        // Act
        var canExecute = viewModel.DeleteTemplateCommand.CanExecute(null);

        // Assert
        await Assert.That(canExecute).IsFalse();
    }

    [Test]
    public async Task DeleteTemplateCommand_AfterDeletion_ShouldClearFormAndReloadTemplates()
    {
        // Arrange
        var template = new Template
        {
            Id = Guid.NewGuid(),
            Name = "Template to Delete",
            Content = "Content"
        };

        _mockTemplateService.Setup(s => s.DeleteAsync(template.Id, default))
            .Returns(Task.CompletedTask);
        _mockTemplateService.Setup(s => s.GetAllAsync(default))
            .ReturnsAsync(new List<Template>());

        var viewModel = CreateViewModel();
        viewModel.SelectedTemplate = template;

        // Act
        await viewModel.DeleteTemplateCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(viewModel.SelectedTemplate).IsNull();
        await Assert.That(viewModel.TemplateName).IsEmpty();
        await Assert.That(viewModel.TemplateContent).IsEmpty();
    }
}
