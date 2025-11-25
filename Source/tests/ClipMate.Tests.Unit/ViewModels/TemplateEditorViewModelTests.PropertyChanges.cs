using ClipMate.Core.Models;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Tests for TemplateEditorViewModel property changes.
/// </summary>
public partial class TemplateEditorViewModelTests
{
    [Test]
    public async Task TemplateName_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var newName = "Test Template";

        // Act
        viewModel.TemplateName = newName;

        // Assert
        await Assert.That(viewModel.TemplateName).IsEqualTo(newName);
    }

    [Test]
    public async Task TemplateContent_WhenSet_ShouldUpdateProperty()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var newContent = "Hello {NAME}!";

        // Act
        viewModel.TemplateContent = newContent;

        // Assert
        await Assert.That(viewModel.TemplateContent).IsEqualTo(newContent);
    }

    [Test]
    public async Task SelectedTemplate_WhenSet_ShouldPopulateFormFields()
    {
        // Arrange
        var viewModel = CreateViewModel();
        var template = new Template
        {
            Id = Guid.NewGuid(),
            Name = "Email Signature",
            Content = "Best regards,\n{USERNAME}",
            Description = "Standard email signature"
        };

        // Act
        viewModel.SelectedTemplate = template;

        // Assert
        await Assert.That(viewModel.TemplateName).IsEqualTo(template.Name);
        await Assert.That(viewModel.TemplateContent).IsEqualTo(template.Content);
        await Assert.That(viewModel.TemplateDescription).IsEqualTo(template.Description);
    }
}
