using ClipMate.Core.Models;
using Moq;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Tests for TemplateEditorViewModel CreateTemplateCommand.
/// </summary>
public partial class TemplateEditorViewModelTests
{
    [Test]
    public async Task CreateTemplateCommand_WithValidData_ShouldCreateTemplate()
    {
        // Arrange
        var createdTemplate = new Template
        {
            Id = Guid.NewGuid(),
            Name = "New Template",
            Content = "Template Content"
        };

        _mockTemplateService.Setup(s => s.CreateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Guid?>(),
            default))
            .ReturnsAsync(createdTemplate);

        var viewModel = CreateViewModel();
        viewModel.TemplateName = "New Template";
        viewModel.TemplateContent = "Template Content";

        // Act
        await viewModel.CreateTemplateCommand.ExecuteAsync(null);

        // Assert
        _mockTemplateService.Verify(s => s.CreateAsync(
            "New Template",
            "Template Content",
            It.IsAny<string>(),
            It.IsAny<Guid?>(),
            default), Times.Once);
    }

    [Test]
    public async Task CreateTemplateCommand_WithEmptyName_ShouldNotBeExecutable()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.TemplateName = "";
        viewModel.TemplateContent = "Some content";

        // Act
        var canExecute = viewModel.CreateTemplateCommand.CanExecute(null);

        // Assert
        await Assert.That(canExecute).IsFalse();
    }

    [Test]
    public async Task CreateTemplateCommand_WithEmptyContent_ShouldNotBeExecutable()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.TemplateName = "Template Name";
        viewModel.TemplateContent = "";

        // Act
        var canExecute = viewModel.CreateTemplateCommand.CanExecute(null);

        // Assert
        await Assert.That(canExecute).IsFalse();
    }

    [Test]
    public async Task CreateTemplateCommand_AfterCreation_ShouldReloadTemplates()
    {
        // Arrange
        var createdTemplate = new Template
        {
            Id = Guid.NewGuid(),
            Name = "New Template",
            Content = "Content"
        };

        _mockTemplateService.Setup(s => s.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), default))
            .ReturnsAsync(createdTemplate);
        _mockTemplateService.Setup(s => s.GetAllAsync(default))
            .ReturnsAsync(new List<Template> { createdTemplate });

        var viewModel = CreateViewModel();
        viewModel.TemplateName = "New Template";
        viewModel.TemplateContent = "Content";

        // Act
        await viewModel.CreateTemplateCommand.ExecuteAsync(null);

        // Assert
        _mockTemplateService.Verify(s => s.GetAllAsync(default), Times.AtLeastOnce);
    }
}
