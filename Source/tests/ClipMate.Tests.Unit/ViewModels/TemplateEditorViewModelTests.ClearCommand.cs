using ClipMate.Core.Models;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Tests for TemplateEditorViewModel ClearFormCommand.
/// </summary>
public partial class TemplateEditorViewModelTests
{
    [Test]
    public async Task ClearFormCommand_ShouldClearAllFormFields()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.TemplateName = "Test Name";
        viewModel.TemplateContent = "Test Content";
        viewModel.TemplateDescription = "Test Description";
        viewModel.SelectedTemplate = new Template { Id = Guid.NewGuid() };

        // Act
        viewModel.ClearFormCommand.Execute(null);

        // Assert
        await Assert.That(viewModel.TemplateName).IsEmpty();
        await Assert.That(viewModel.TemplateContent).IsEmpty();
        await Assert.That(viewModel.TemplateDescription).IsEmpty();
        await Assert.That(viewModel.SelectedTemplate).IsNull();
    }
}
