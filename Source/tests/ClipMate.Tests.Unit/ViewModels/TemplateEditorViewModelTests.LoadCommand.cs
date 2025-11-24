using ClipMate.Core.Models;
using Moq;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Tests for TemplateEditorViewModel LoadTemplatesCommand.
/// </summary>
public partial class TemplateEditorViewModelTests
{
    [Test]
    public async Task LoadTemplatesCommand_ShouldLoadAllTemplates()
    {
        // Arrange
        var templates = new List<Template>
        {
            new() { Id = Guid.NewGuid(), Name = "Template 1", Content = "Content 1" },
            new() { Id = Guid.NewGuid(), Name = "Template 2", Content = "Content 2" },
            new() { Id = Guid.NewGuid(), Name = "Template 3", Content = "Content 3" }
        };
        _mockTemplateService.Setup(s => s.GetAllAsync(default)).ReturnsAsync(templates);

        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoadTemplatesCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(viewModel.Templates.Count).IsEqualTo(3);
    }

    [Test]
    public async Task LoadTemplatesCommand_WhenError_ShouldSetErrorMessage()
    {
        // Arrange
        _mockTemplateService.Setup(s => s.GetAllAsync(default))
            .ThrowsAsync(new Exception("Database error"));

        var viewModel = CreateViewModel();

        // Act
        await viewModel.LoadTemplatesCommand.ExecuteAsync(null);

        // Assert
        await Assert.That(viewModel.ErrorMessage).IsNotEmpty();
        await Assert.That(viewModel.ErrorMessage!.ToLower().Contains("error")).IsTrue();
    }
}
