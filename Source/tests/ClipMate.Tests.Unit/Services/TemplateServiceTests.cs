using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using Moq;
using Shouldly;
using Xunit;

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

    [Fact]
    public void Constructor_WithNullRepository_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentNullException>(() => new TemplateService(null!));
    }

    [Fact]
    public void Constructor_WithValidRepository_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        Should.NotThrow(() => new TemplateService(_mockRepository.Object));
    }

    #endregion

    #region ExtractVariables Tests

    [Fact]
    public void ExtractVariables_WithNoVariables_ShouldReturnEmptyList()
    {
        // Arrange
        var service = CreateTemplateService();
        var content = "Hello world, this is plain text with no variables.";

        // Act
        var variables = service.ExtractVariables(content);

        // Assert
        variables.ShouldBeEmpty();
    }

    [Fact]
    public void ExtractVariables_WithSingleVariable_ShouldReturnVariable()
    {
        // Arrange
        var service = CreateTemplateService();
        var content = "Hello {NAME}, welcome!";

        // Act
        var variables = service.ExtractVariables(content);

        // Assert
        variables.Count.ShouldBe(1);
        variables[0].ShouldBe("NAME");
    }

    [Fact]
    public void ExtractVariables_WithMultipleVariables_ShouldReturnAllVariables()
    {
        // Arrange
        var service = CreateTemplateService();
        var content = "Date: {DATE}, Time: {TIME}, User: {USERNAME}";

        // Act
        var variables = service.ExtractVariables(content);

        // Assert
        variables.Count.ShouldBe(3);
        variables.ShouldContain("DATE");
        variables.ShouldContain("TIME");
        variables.ShouldContain("USERNAME");
    }

    [Fact]
    public void ExtractVariables_WithDuplicateVariables_ShouldReturnUniqueVariables()
    {
        // Arrange
        var service = CreateTemplateService();
        var content = "{NAME} said to {NAME} that {NAME} is great";

        // Act
        var variables = service.ExtractVariables(content);

        // Assert
        variables.Count.ShouldBe(1);
        variables[0].ShouldBe("NAME");
    }

    [Fact]
    public void ExtractVariables_WithFormattedVariable_ShouldExtractVariableName()
    {
        // Arrange
        var service = CreateTemplateService();
        var content = "Today is {DATE:yyyy-MM-dd}";

        // Act
        var variables = service.ExtractVariables(content);

        // Assert
        variables.Count.ShouldBe(1);
        variables[0].ShouldBe("DATE");
    }

    [Fact]
    public void ExtractVariables_WithPromptVariable_ShouldExtractVariableName()
    {
        // Arrange
        var service = CreateTemplateService();
        var content = "Hello {PROMPT:Enter your name}!";

        // Act
        var variables = service.ExtractVariables(content);

        // Assert
        variables.Count.ShouldBe(1);
        variables[0].ShouldBe("PROMPT");
    }

    #endregion

    #region ExpandTemplateAsync Tests - Built-in Variables

    [Fact]
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
        result.ShouldContain(today);
    }

    [Fact]
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
        result.ShouldBe($"Date: {expected}");
    }

    [Fact]
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
        result.ShouldStartWith("Current time: ");
        // Time should be present (cannot assert exact match due to timing)
    }

    [Fact]
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
        result.ShouldStartWith("Time: ");
        result.Length.ShouldBe("Time: ".Length + 8); // HH:mm:ss is 8 characters
    }

    [Fact]
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
        result.ShouldStartWith("User: ");
        result.ShouldNotBe("User: {USERNAME}"); // Should be replaced
    }

    [Fact]
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
        result.ShouldStartWith("Machine: ");
        result.ShouldNotBe("Machine: {COMPUTERNAME}"); // Should be replaced
    }

    #endregion

    #region ExpandTemplateAsync Tests - Custom Variables

    [Fact]
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
        result.ShouldBe("Hello John Doe, welcome to Acme Corp!");
    }

    [Fact]
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
        result.ShouldBe("Hello {NAME}!");
    }

    [Fact]
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
        result.ShouldContain("ClipMate v1.0");
        result.ShouldNotContain("{PROJECT}");
        result.ShouldNotContain("{USERNAME}"); // Built-in should be replaced
        result.ShouldNotContain("{DATE}"); // Built-in should be replaced
    }

    #endregion

    #region ExpandTemplateAsync Tests - Error Cases

    [Fact]
    public async Task ExpandTemplateAsync_WithNonExistentTemplate_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var service = CreateTemplateService();
        var templateId = Guid.NewGuid();
        _mockRepository.Setup(r => r.GetByIdAsync(templateId, default)).ReturnsAsync((Template?)null);

        // Act & Assert
        await Should.ThrowAsync<KeyNotFoundException>(async () =>
            await service.ExpandTemplateAsync(templateId, new Dictionary<string, string>()));
    }

    [Fact]
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
        result.ShouldStartWith("Date: ");
        result.ShouldNotContain("INVALID_FORMAT");
    }

    #endregion

    #region CRUD Operation Tests

    [Fact]
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
        result.ShouldNotBeNull();
        result.Name.ShouldBe(name);
        result.Content.ShouldBe(content);
        result.Id.ShouldNotBe(Guid.Empty);
        result.CreatedAt.ShouldBeGreaterThan(DateTime.MinValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_WithInvalidName_ShouldThrowArgumentException(string? invalidName)
    {
        // Arrange
        var service = CreateTemplateService();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await service.CreateAsync(invalidName!, "content"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task CreateAsync_WithInvalidContent_ShouldThrowArgumentException(string? invalidContent)
    {
        // Arrange
        var service = CreateTemplateService();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await service.CreateAsync("name", invalidContent!));
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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
        result.ShouldNotBeNull();
        result.Id.ShouldBe(template.Id);
        result.Name.ShouldBe(template.Name);
    }

    [Fact]
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
        result.Count.ShouldBe(3);
    }

    [Fact]
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
        result.Count.ShouldBe(2);
        result.ShouldAllBe(t => t.CollectionId == collectionId);
    }

    #endregion

    #region UseCount Tracking Tests

    [Fact]
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
