using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using Moq;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for TemplateService variable expansion and template management.
/// Tests cover: variable extraction, expansion with built-in variables ({DATE}, {TIME}, {USERNAME}, {COMPUTERNAME}),
/// date/time format strings, prompt variables, CRUD operations, and error handling.
/// </summary>
public class TemplateServiceTests
{
    private readonly Mock<ITemplateRepository> _mockRepository;

    public TemplateServiceTests()
    {
        _mockRepository = new Mock<ITemplateRepository>();
    }

    private TemplateService CreateTemplateService()
    {
        return new TemplateService(_mockRepository.Object);
    }

    #region Constructor Tests

    [Test]
    public async Task Constructor_WithNullRepository_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        await Assert.That(() => new TemplateService(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithValidRepository_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        await Assert.That(() => new TemplateService(_mockRepository.Object)).ThrowsNothing();
    }

    #endregion

    #region ExtractVariables Tests

    [Test]
    public async Task ExtractVariables_WithNoVariables_ShouldReturnEmptyList()
    {
        // Arrange
        var service = CreateTemplateService();
        var content = "Hello world, this is plain text with no variables.";

        // Act
        var variables = service.ExtractVariables(content);

        // Assert
        await Assert.That(variables).IsEmpty();
    }

    [Test]
    public async Task ExtractVariables_WithSingleVariable_ShouldReturnVariable()
    {
        // Arrange
        var service = CreateTemplateService();
        var content = "Hello {NAME}, welcome!";

        // Act
        var variables = service.ExtractVariables(content);

        // Assert
        await Assert.That(variables.Count).IsEqualTo(1);
        await Assert.That(variables[0]).IsEqualTo("NAME");
    }

    [Test]
    public async Task ExtractVariables_WithMultipleVariables_ShouldReturnAllVariables()
    {
        // Arrange
        var service = CreateTemplateService();
        var content = "Date: {DATE}, Time: {TIME}, User: {USERNAME}";

        // Act
        var variables = service.ExtractVariables(content);

        // Assert
        await Assert.That(variables.Count).IsEqualTo(3);
        await Assert.That(variables).Contains("DATE");
        await Assert.That(variables).Contains("TIME");
        await Assert.That(variables).Contains("USERNAME");
    }

    [Test]
    public async Task ExtractVariables_WithDuplicateVariables_ShouldReturnUniqueVariables()
    {
        // Arrange
        var service = CreateTemplateService();
        var content = "{NAME} said to {NAME} that {NAME} is great";

        // Act
        var variables = service.ExtractVariables(content);

        // Assert
        await Assert.That(variables.Count).IsEqualTo(1);
        await Assert.That(variables[0]).IsEqualTo("NAME");
    }

    [Test]
    public async Task ExtractVariables_WithFormattedVariable_ShouldExtractVariableName()
    {
        // Arrange
        var service = CreateTemplateService();
        var content = "Today is {DATE:yyyy-MM-dd}";

        // Act
        var variables = service.ExtractVariables(content);

        // Assert
        await Assert.That(variables.Count).IsEqualTo(1);
        await Assert.That(variables[0]).IsEqualTo("DATE");
    }

    [Test]
    public async Task ExtractVariables_WithPromptVariable_ShouldExtractVariableName()
    {
        // Arrange
        var service = CreateTemplateService();
        var content = "Hello {PROMPT:Enter your name}!";

        // Act
        var variables = service.ExtractVariables(content);

        // Assert
        await Assert.That(variables.Count).IsEqualTo(1);
        await Assert.That(variables[0]).IsEqualTo("PROMPT");
    }

    #endregion

    #region ExpandTemplateAsync Tests - Built-in Variables

    [Test]
    public async Task ExpandTemplateAsync_WithDateVariable_ShouldReplaceWithCurrentDate()
    {
        // Arrange
        var service = CreateTemplateService();
        var template = new Template
        {
            Id = Guid.NewGuid(),
            Content = "Today is {DATE}"
        };
        _mockRepository.Setup(r => r.GetByIdAsync(template.Id, default)).ReturnsAsync(template);

        // Act
        var result = await service.ExpandTemplateAsync(template.Id, new Dictionary<string, string>());

        // Assert
        var today = DateTime.Now.ToShortDateString();
        await Assert.That(result).Contains(today);
    }

    [Test]
    public async Task ExpandTemplateAsync_WithDateFormat_ShouldRespectFormatString()
    {
        // Arrange
        var service = CreateTemplateService();
        var template = new Template
        {
            Id = Guid.NewGuid(),
            Content = "Date: {DATE:yyyy-MM-dd}"
        };
        _mockRepository.Setup(r => r.GetByIdAsync(template.Id, default)).ReturnsAsync(template);

        // Act
        var result = await service.ExpandTemplateAsync(template.Id, new Dictionary<string, string>());

        // Assert
        var expected = DateTime.Now.ToString("yyyy-MM-dd");
        await Assert.That(result).IsEqualTo($"Date: {expected}");
    }

    [Test]
    public async Task ExpandTemplateAsync_WithTimeVariable_ShouldReplaceWithCurrentTime()
    {
        // Arrange
        var service = CreateTemplateService();
        var template = new Template
        {
            Id = Guid.NewGuid(),
            Content = "Current time: {TIME}"
        };
        _mockRepository.Setup(r => r.GetByIdAsync(template.Id, default)).ReturnsAsync(template);

        // Act
        var result = await service.ExpandTemplateAsync(template.Id, new Dictionary<string, string>());

        // Assert
        await Assert.That(result).StartsWith("Current time: ");
        // Time should be present (cannot assert exact match due to timing)
    }

    [Test]
    public async Task ExpandTemplateAsync_WithTimeFormat_ShouldRespectFormatString()
    {
        // Arrange
        var service = CreateTemplateService();
        var template = new Template
        {
            Id = Guid.NewGuid(),
            Content = "Time: {TIME:HH:mm:ss}"
        };
        _mockRepository.Setup(r => r.GetByIdAsync(template.Id, default)).ReturnsAsync(template);

        // Act
        var result = await service.ExpandTemplateAsync(template.Id, new Dictionary<string, string>());

        // Assert
        await Assert.That(result).StartsWith("Time: ");
        await Assert.That(result.Length).IsEqualTo("Time: ".Length + 8); // HH:mm:ss is 8 characters
    }

    [Test]
    public async Task ExpandTemplateAsync_WithUsernameVariable_ShouldReplaceWithCurrentUser()
    {
        // Arrange
        var service = CreateTemplateService();
        var template = new Template
        {
            Id = Guid.NewGuid(),
            Content = "User: {USERNAME}"
        };
        _mockRepository.Setup(r => r.GetByIdAsync(template.Id, default)).ReturnsAsync(template);

        // Act
        var result = await service.ExpandTemplateAsync(template.Id, new Dictionary<string, string>());

        // Assert
        await Assert.That(result).StartsWith("User: ");
        await Assert.That(result).IsNotEqualTo("User: {USERNAME}"); // Should be replaced
    }

    [Test]
    public async Task ExpandTemplateAsync_WithComputerNameVariable_ShouldReplaceWithMachineName()
    {
        // Arrange
        var service = CreateTemplateService();
        var template = new Template
        {
            Id = Guid.NewGuid(),
            Content = "Machine: {COMPUTERNAME}"
        };
        _mockRepository.Setup(r => r.GetByIdAsync(template.Id, default)).ReturnsAsync(template);

        // Act
        var result = await service.ExpandTemplateAsync(template.Id, new Dictionary<string, string>());

        // Assert
        await Assert.That(result).StartsWith("Machine: ");
        await Assert.That(result).IsNotEqualTo("Machine: {COMPUTERNAME}"); // Should be replaced
    }

    #endregion

    #region ExpandTemplateAsync Tests - Custom Variables

    [Test]
    public async Task ExpandTemplateAsync_WithCustomVariable_ShouldReplaceWithProvidedValue()
    {
        // Arrange
        var service = CreateTemplateService();
        var template = new Template
        {
            Id = Guid.NewGuid(),
            Content = "Hello {NAME}, welcome to {COMPANY}!"
        };
        _mockRepository.Setup(r => r.GetByIdAsync(template.Id, default)).ReturnsAsync(template);

        var variables = new Dictionary<string, string>
        {
            { "NAME", "John Doe" },
            { "COMPANY", "Acme Corp" }
        };

        // Act
        var result = await service.ExpandTemplateAsync(template.Id, variables);

        // Assert
        await Assert.That(result).IsEqualTo("Hello John Doe, welcome to Acme Corp!");
    }

    [Test]
    public async Task ExpandTemplateAsync_WithMissingCustomVariable_ShouldLeaveVariableUnchanged()
    {
        // Arrange
        var service = CreateTemplateService();
        var template = new Template
        {
            Id = Guid.NewGuid(),
            Content = "Hello {NAME}!"
        };
        _mockRepository.Setup(r => r.GetByIdAsync(template.Id, default)).ReturnsAsync(template);

        // Act
        var result = await service.ExpandTemplateAsync(template.Id, new Dictionary<string, string>());

        // Assert
        await Assert.That(result).IsEqualTo("Hello {NAME}!");
    }

    [Test]
    public async Task ExpandTemplateAsync_WithMixedVariables_ShouldReplaceAllCorrectly()
    {
        // Arrange
        var service = CreateTemplateService();
        var template = new Template
        {
            Id = Guid.NewGuid(),
            Content = "Report by {USERNAME} on {DATE} for {PROJECT}"
        };
        _mockRepository.Setup(r => r.GetByIdAsync(template.Id, default)).ReturnsAsync(template);

        var variables = new Dictionary<string, string>
        {
            { "PROJECT", "ClipMate v1.0" }
        };

        // Act
        var result = await service.ExpandTemplateAsync(template.Id, variables);

        // Assert
        await Assert.That(result).Contains("ClipMate v1.0");
        await Assert.That(result).DoesNotContain("{PROJECT}");
        await Assert.That(result).DoesNotContain("{USERNAME}"); // Built-in should be replaced
        await Assert.That(result).DoesNotContain("{DATE}"); // Built-in should be replaced
    }

    #endregion

    #region ExpandTemplateAsync Tests - Error Cases

    [Test]
    public async Task ExpandTemplateAsync_WithNonExistentTemplate_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var service = CreateTemplateService();
        var templateId = Guid.NewGuid();
        _mockRepository.Setup(r => r.GetByIdAsync(templateId, default)).ReturnsAsync((Template?)null);

        // Act & Assert
        await Assert.That(async () =>
            await service.ExpandTemplateAsync(templateId, new Dictionary<string, string>())).Throws<KeyNotFoundException>();
    }

    [Test]
    public async Task ExpandTemplateAsync_WithInvalidDateFormat_ShouldUseDefaultFormat()
    {
        // Arrange
        var service = CreateTemplateService();
        var template = new Template
        {
            Id = Guid.NewGuid(),
            Content = "Date: {DATE:INVALID_FORMAT}"
        };
        _mockRepository.Setup(r => r.GetByIdAsync(template.Id, default)).ReturnsAsync(template);

        // Act
        var result = await service.ExpandTemplateAsync(template.Id, new Dictionary<string, string>());

        // Assert
        await Assert.That(result).StartsWith("Date: ");
        await Assert.That(result).DoesNotContain("INVALID_FORMAT");
    }

    #endregion

    #region CRUD Operation Tests

    [Test]
    public async Task CreateAsync_WithValidData_ShouldCreateTemplate()
    {
        // Arrange
        var service = CreateTemplateService();
        var name = "Email Signature";
        var content = "Best regards,\n{USERNAME}";

        _mockRepository.Setup(r => r.CreateAsync(It.IsAny<Template>(), default))
            .ReturnsAsync((Template t, CancellationToken _) => t);

        // Act
        var result = await service.CreateAsync(name, content);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Name).IsEqualTo(name);
        await Assert.That(result.Content).IsEqualTo(content);
        await Assert.That(result.Id).IsNotEqualTo(Guid.Empty);
        await Assert.That(result.CreatedAt).IsGreaterThan(DateTime.MinValue);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task CreateAsync_WithInvalidName_ShouldThrowArgumentException(string? invalidName)
    {
        // Arrange
        var service = CreateTemplateService();

        // Act & Assert
        await Assert.That(async () =>
            await service.CreateAsync(invalidName!, "content")).Throws<ArgumentException>();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    public async Task CreateAsync_WithInvalidContent_ShouldThrowArgumentException(string? invalidContent)
    {
        // Arrange
        var service = CreateTemplateService();

        // Act & Assert
        await Assert.That(async () =>
            await service.CreateAsync("name", invalidContent!)).Throws<ArgumentException>();
    }

    [Test]
    public async Task UpdateAsync_WithValidTemplate_ShouldUpdateTemplate()
    {
        // Arrange
        var service = CreateTemplateService();
        var template = new Template
        {
            Id = Guid.NewGuid(),
            Name = "Updated Name",
            Content = "Updated Content"
        };

        _mockRepository.Setup(r => r.UpdateAsync(template, default)).ReturnsAsync(true);

        // Act
        await service.UpdateAsync(template);

        // Assert
        _mockRepository.Verify(r => r.UpdateAsync(It.Is<Template>(t => 
            t.Id == template.Id && 
            t.Name == template.Name && 
            t.Content == template.Content), default), Times.Once);
    }

    [Test]
    public async Task DeleteAsync_WithValidId_ShouldDeleteTemplate()
    {
        // Arrange
        var service = CreateTemplateService();
        var templateId = Guid.NewGuid();

        _mockRepository.Setup(r => r.DeleteAsync(templateId, default)).ReturnsAsync(true);

        // Act
        await service.DeleteAsync(templateId);

        // Assert
        _mockRepository.Verify(r => r.DeleteAsync(templateId, default), Times.Once);
    }

    [Test]
    public async Task GetByIdAsync_WithValidId_ShouldReturnTemplate()
    {
        // Arrange
        var service = CreateTemplateService();
        var template = new Template
        {
            Id = Guid.NewGuid(),
            Name = "Test Template",
            Content = "Test Content"
        };

        _mockRepository.Setup(r => r.GetByIdAsync(template.Id, default)).ReturnsAsync(template);

        // Act
        var result = await service.GetByIdAsync(template.Id);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(template.Id);
        await Assert.That(result.Name).IsEqualTo(template.Name);
    }

    [Test]
    public async Task GetAllAsync_ShouldReturnAllTemplates()
    {
        // Arrange
        var service = CreateTemplateService();
        var templates = new List<Template>
        {
            new() { Id = Guid.NewGuid(), Name = "Template 1", Content = "Content 1" },
            new() { Id = Guid.NewGuid(), Name = "Template 2", Content = "Content 2" },
            new() { Id = Guid.NewGuid(), Name = "Template 3", Content = "Content 3" }
        };

        _mockRepository.Setup(r => r.GetAllAsync(default)).ReturnsAsync(templates);

        // Act
        var result = await service.GetAllAsync();

        // Assert
        await Assert.That(result.Count).IsEqualTo(3);
    }

    [Test]
    public async Task GetByCollectionAsync_WithValidCollection_ShouldReturnTemplatesInCollection()
    {
        // Arrange
        var service = CreateTemplateService();
        var collectionId = Guid.NewGuid();
        var templates = new List<Template>
        {
            new() { Id = Guid.NewGuid(), Name = "Template 1", Content = "Content 1", CollectionId = collectionId },
            new() { Id = Guid.NewGuid(), Name = "Template 2", Content = "Content 2", CollectionId = collectionId }
        };

        _mockRepository.Setup(r => r.GetByCollectionAsync(collectionId, default)).ReturnsAsync(templates);

        // Act
        var result = await service.GetByCollectionAsync(collectionId);

        // Assert
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result.All(t => t.CollectionId == collectionId)).IsTrue();
    }

    #endregion

    #region UseCount Tracking Tests

    [Test]
    public async Task ExpandTemplateAsync_WhenCalled_ShouldIncrementUseCount()
    {
        // Arrange
        var service = CreateTemplateService();
        var template = new Template
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Content = "Hello {NAME}",
            UseCount = 5
        };
        _mockRepository.Setup(r => r.GetByIdAsync(template.Id, default)).ReturnsAsync(template);
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<Template>(), default)).ReturnsAsync(true);

        // Act
        await service.ExpandTemplateAsync(template.Id, new Dictionary<string, string> { { "NAME", "Test" } });

        // Assert
        _mockRepository.Verify(r => r.UpdateAsync(
            It.Is<Template>(t => t.Id == template.Id && t.UseCount == 6), 
            default), 
            Times.Once);
    }

    #endregion
}
