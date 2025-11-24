using ClipMate.App.ViewModels;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using Moq;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace ClipMate.Tests.Unit.ViewModels;

/// <summary>
/// Unit tests for TemplateEditorViewModel covering template management functionality.
/// Tests cover: property changes, CRUD commands, validation, preview functionality, and service integration.
/// </summary>
public class TemplateEditorViewModelTests
{
    private readonly Mock<ITemplateService> _mockTemplateService;

    public TemplateEditorViewModelTests()
    {
        _mockTemplateService = new Mock<ITemplateService>();
    }

    private TemplateEditorViewModel CreateViewModel()
    {
        return new TemplateEditorViewModel(_mockTemplateService.Object);
    }

    #region Constructor Tests

    [Test]
    public async Task Constructor_WithNullTemplateService_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        await Assert.That(() => new TemplateEditorViewModel(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithValidService_ShouldInitializeProperties()
    {
        // Arrange & Act
        var viewModel = CreateViewModel();

        // Assert
        await Assert.That(viewModel.TemplateName).IsEmpty();
        await Assert.That(viewModel.TemplateContent).IsEmpty();
        await Assert.That(viewModel.TemplateDescription).IsEmpty();
        await Assert.That(viewModel.Templates).IsNotNull();
        await Assert.That(viewModel.Templates.Count).IsEqualTo(0);
    }

    #endregion

    #region Property Change Tests

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

    #endregion

    #region LoadTemplatesCommand Tests

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

    #endregion

    #region CreateTemplateCommand Tests

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

    #endregion

    #region UpdateTemplateCommand Tests

    [Test]
    public async Task UpdateTemplateCommand_WithSelectedTemplate_ShouldUpdateTemplate()
    {
        // Arrange
        var template = new Template
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            Content = "Original Content"
        };

        _mockTemplateService.Setup(s => s.UpdateAsync(It.IsAny<Template>(), default))
            .Returns(Task.CompletedTask);

        var viewModel = CreateViewModel();
        viewModel.SelectedTemplate = template;
        viewModel.TemplateName = "Updated Name";
        viewModel.TemplateContent = "Updated Content";

        // Act
        await viewModel.UpdateTemplateCommand.ExecuteAsync(null);

        // Assert
        _mockTemplateService.Verify(s => s.UpdateAsync(
            It.Is<Template>(t => t.Name == "Updated Name" && t.Content == "Updated Content"),
            default), Times.Once);
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

    #endregion

    #region DeleteTemplateCommand Tests

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

    #endregion

    #region ClearFormCommand Tests

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

    #endregion

    #region PreviewTemplateCommand Tests

    [Test]
    public async Task PreviewTemplateCommand_WithValidContent_ShouldExpandVariables()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var expandedContent = $"Hello World! Today is {DateTime.Now:yyyy-MM-dd}";
        
        _mockTemplateService.Setup(s => s.ExpandTemplateAsync(templateId, It.IsAny<Dictionary<string, string>>(), default))
            .ReturnsAsync(expandedContent);

        var viewModel = CreateViewModel();
        viewModel.SelectedTemplate = new Template 
        { 
            Id = templateId, 
            Content = "Hello {NAME}! Today is {DATE:yyyy-MM-dd}" 
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

    #endregion

    #region Variable Extraction Tests

    [Test]
    public async Task ExtractedVariables_ShouldUpdateWhenContentChanges()
    {
        // Arrange
        _mockTemplateService.Setup(s => s.ExtractVariables(It.IsAny<string>()))
            .Returns((string content) => content.Contains("{NAME}") && content.Contains("{DATE}")
                ? new List<string> { "NAME", "DATE" }
                : new List<string>());

        var viewModel = CreateViewModel();

        // Act
        viewModel.TemplateContent = "Hello {NAME}, today is {DATE}";

        // Assert
        await Assert.That(viewModel.ExtractedVariables).IsNotNull();
        await Assert.That(viewModel.ExtractedVariables.Count).IsGreaterThan(0);
    }

    #endregion

    #region Validation Tests

    [Test]
    public async Task IsFormValid_WithValidNameAndContent_ShouldBeTrue()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.TemplateName = "Valid Template";
        viewModel.TemplateContent = "Valid Content";

        // Act
        var isValid = viewModel.IsFormValid;

        // Assert
        await Assert.That(isValid).IsTrue();
    }

    [Test]
    public async Task IsFormValid_WithEmptyName_ShouldBeFalse()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.TemplateName = "";
        viewModel.TemplateContent = "Valid Content";

        // Act
        var isValid = viewModel.IsFormValid;

        // Assert
        await Assert.That(isValid).IsFalse();
    }

    [Test]
    public async Task IsFormValid_WithEmptyContent_ShouldBeFalse()
    {
        // Arrange
        var viewModel = CreateViewModel();
        viewModel.TemplateName = "Valid Name";
        viewModel.TemplateContent = "";

        // Act
        var isValid = viewModel.IsFormValid;

        // Assert
        await Assert.That(isValid).IsFalse();
    }

    #endregion
}
