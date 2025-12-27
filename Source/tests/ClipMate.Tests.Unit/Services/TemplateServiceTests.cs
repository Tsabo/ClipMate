using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for TemplateService (Test-Driven Development).
/// </summary>
public class TemplateServiceTests
{
    private readonly Mock<IConfigurationService> _mockConfigService;
    private readonly Mock<ILogger<TemplateService>> _mockLogger;
    private string _testTemplatesDir = null!;

    public TemplateServiceTests()
    {
        _mockConfigService = new Mock<IConfigurationService>();
        _mockLogger = new Mock<ILogger<TemplateService>>();
    }

    [Before(Test)]
    public void Setup()
    {
        // Create temp directory for test templates
        _testTemplatesDir = Path.Combine(Path.GetTempPath(), $"ClipMateTemplates_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testTemplatesDir);

        // Setup configuration to return test directory (use real instances, not mocks)
        var config = new ClipMateConfiguration
        {
            Directories = new DirectoriesConfiguration
            {
                TemplatesDirectory = _testTemplatesDir,
            },
        };

        _mockConfigService.Setup(p => p.Configuration).Returns(config);
    }

    [After(Test)]
    public void Cleanup()
    {
        // Delete test templates directory
        if (Directory.Exists(_testTemplatesDir))
            Directory.Delete(_testTemplatesDir, true);
    }

    [Test]
    public async Task GetAllTemplatesAsync_EmptyDirectory_ReturnsEmptyList()
    {
        // Arrange
        var service = new TemplateService(_mockConfigService.Object, _mockLogger.Object);

        // Act
        var templates = await service.GetAllTemplatesAsync();

        // Assert
        await Assert.That(templates).IsEmpty();
    }

    [Test]
    public async Task GetAllTemplatesAsync_WithTemplateFiles_ReturnsTemplates()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testTemplatesDir, "template1.txt"), "Content 1");
        await File.WriteAllTextAsync(Path.Combine(_testTemplatesDir, "template2.txt"), "Content 2");
        var service = new TemplateService(_mockConfigService.Object, _mockLogger.Object);

        // Act
        var templates = await service.GetAllTemplatesAsync();

        // Assert
        await Assert.That(templates).Count().IsEqualTo(2);
        await Assert.That(templates.Any(p => p.Name == "template1")).IsTrue();
        await Assert.That(templates.Any(p => p.Name == "template2")).IsTrue();
    }

    [Test]
    public async Task GetAllTemplatesAsync_IgnoresNonTxtFiles()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testTemplatesDir, "template1.txt"), "Content 1");
        await File.WriteAllTextAsync(Path.Combine(_testTemplatesDir, "readme.md"), "Readme");
        await File.WriteAllTextAsync(Path.Combine(_testTemplatesDir, "config.ini"), "Config");
        var service = new TemplateService(_mockConfigService.Object, _mockLogger.Object);

        // Act
        var templates = await service.GetAllTemplatesAsync();

        // Assert
        await Assert.That(templates).Count().IsEqualTo(1);
        await Assert.That(templates[0].Name).IsEqualTo("template1");
    }

    [Test]
    public async Task GetTemplateByNameAsync_ExistingTemplate_ReturnsTemplate()
    {
        // Arrange
        var content = "Test template content";
        await File.WriteAllTextAsync(Path.Combine(_testTemplatesDir, "mytemplate.txt"), content);
        var service = new TemplateService(_mockConfigService.Object, _mockLogger.Object);

        // Act
        var template = await service.GetTemplateByNameAsync("mytemplate");

        // Assert
        await Assert.That(template).IsNotNull();
        await Assert.That(template!.Name).IsEqualTo("mytemplate");
        await Assert.That(template.Content).IsEqualTo(content);
    }

    [Test]
    public async Task GetTemplateByNameAsync_NonExistingTemplate_ReturnsNull()
    {
        // Arrange
        var service = new TemplateService(_mockConfigService.Object, _mockLogger.Object);

        // Act
        var template = await service.GetTemplateByNameAsync("nonexistent");

        // Assert
        await Assert.That(template).IsNull();
    }

    [Test]
    public async Task MergeClipWithTemplate_ReplacesSingleTag()
    {
        // Arrange
        var service = new TemplateService(_mockConfigService.Object, _mockLogger.Object);
        var template = new FileTemplate
        {
            Name = "test",
            Content = "Title: #TITLE#",
        };

        var clip = new Clip
        {
            Title = "MyClip",
        };

        // Act
        var result = service.MergeClipWithTemplate(template, clip, 1);

        // Assert
        await Assert.That(result).IsEqualTo("Title: MyClip");
    }

    [Test]
    public async Task MergeClipWithTemplate_ReplacesMultipleTags()
    {
        // Arrange
        var service = new TemplateService(_mockConfigService.Object, _mockLogger.Object);
        var template = new FileTemplate
        {
            Name = "test",
            Content = "Title: #TITLE#\nURL: #URL#\nCreator: #CREATOR#",
        };

        var clip = new Clip
        {
            Title = "TestClip",
            SourceUrl = "https://example.com",
            SourceApplicationName = "NOTEPAD",
        };

        // Act
        var result = service.MergeClipWithTemplate(template, clip, 1);

        // Assert
        await Assert.That(result).Contains("Title: TestClip");
        await Assert.That(result).Contains("URL: https://example.com");
        await Assert.That(result).Contains("Creator: NOTEPAD");
    }

    [Test]
    public async Task MergeClipWithTemplate_ReplacesClipTag()
    {
        // Arrange
        var service = new TemplateService(_mockConfigService.Object, _mockLogger.Object);
        var template = new FileTemplate
        {
            Name = "test",
            Content = "Content:\n#CLIP#\n---",
        };

        var clip = new Clip
        {
            TextContent = "This is the clip content",
        };

        // Act
        var result = service.MergeClipWithTemplate(template, clip, 1);

        // Assert
        await Assert.That(result).Contains("This is the clip content");
    }

    [Test]
    public async Task MergeClipWithTemplate_ReplacesSequenceTag()
    {
        // Arrange
        var service = new TemplateService(_mockConfigService.Object, _mockLogger.Object);
        var template = new FileTemplate
        {
            Name = "test",
            Content = "Item #SEQUENCE#: #TITLE#",
        };

        var clip = new Clip
        {
            Title = "First",
        };

        // Act
        var result = service.MergeClipWithTemplate(template, clip, 42);

        // Assert
        await Assert.That(result).IsEqualTo("Item 42: First");
    }

    [Test]
    public async Task MergeClipWithTemplate_ReplacesDateTimeTags()
    {
        // Arrange
        var service = new TemplateService(_mockConfigService.Object, _mockLogger.Object);
        var template = new FileTemplate
        {
            Name = "test",
            Content = "Captured: #DATE# at #TIME#",
        };

        var captureDate = new DateTimeOffset(2025, 12, 26, 15, 30, 0, TimeSpan.Zero);
        var clip = new Clip
        {
            CapturedAt = captureDate,
        };

        // Act
        var result = service.MergeClipWithTemplate(template, clip, 1);

        // Assert
        await Assert.That(result).Contains("Captured:");
        await Assert.That(result).Contains("12/26/2025"); // Or locale-specific format
    }

    [Test]
    public async Task RefreshTemplatesAsync_ReloadsTemplates()
    {
        // Arrange
        var service = new TemplateService(_mockConfigService.Object, _mockLogger.Object);
        var templates1 = await service.GetAllTemplatesAsync();
        await Assert.That(templates1).IsEmpty();

        // Add a template file
        await File.WriteAllTextAsync(Path.Combine(_testTemplatesDir, "newtemplate.txt"), "New content");

        // Act
        await service.RefreshTemplatesAsync();
        var templates2 = await service.GetAllTemplatesAsync();

        // Assert
        await Assert.That(templates2).Count().IsEqualTo(1);
        await Assert.That(templates2[0].Name).IsEqualTo("newtemplate");
    }
}
