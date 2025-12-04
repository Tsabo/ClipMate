using ClipMate.Core.Models;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for TemplateService CRUD operations.
/// </summary>
public partial class TemplateServiceTests
{
    [Test]
    public async Task CreateAsync_WithValidData_ShouldCreateTemplate()
    {
        // Arrange
        var service = CreateTemplateService();
        var name = "Email Signature";
        var content = "Best regards,\n{USERNAME}";

        _mockRepository.Setup(p => p.CreateAsync(It.IsAny<Template>(), CancellationToken.None))
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
                await service.CreateAsync(invalidName!, "content"))
            .Throws<ArgumentException>();
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
                await service.CreateAsync("name", invalidContent!))
            .Throws<ArgumentException>();
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
            Content = "Updated Content",
        };

        _mockRepository.Setup(p => p.UpdateAsync(template, CancellationToken.None)).ReturnsAsync(true);

        // Act
        await service.UpdateAsync(template);

        // Assert
        _mockRepository.Verify(p => p.UpdateAsync(It.Is<Template>(t =>
            t.Id == template.Id &&
            t.Name == template.Name &&
            t.Content == template.Content), CancellationToken.None), Times.Once);
    }

    [Test]
    public async Task DeleteAsync_WithValidId_ShouldDeleteTemplate()
    {
        // Arrange
        var service = CreateTemplateService();
        var templateId = Guid.NewGuid();

        _mockRepository.Setup(p => p.DeleteAsync(templateId, CancellationToken.None)).ReturnsAsync(true);

        // Act
        await service.DeleteAsync(templateId);

        // Assert
        _mockRepository.Verify(p => p.DeleteAsync(templateId, CancellationToken.None), Times.Once);
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
            Content = "Test Content",
        };

        _mockRepository.Setup(p => p.GetByIdAsync(template.Id, CancellationToken.None)).ReturnsAsync(template);

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
            new() { Id = Guid.NewGuid(), Name = "Template 3", Content = "Content 3" },
        };

        _mockRepository.Setup(p => p.GetAllAsync(CancellationToken.None)).ReturnsAsync(templates);

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
            new() { Id = Guid.NewGuid(), Name = "Template 2", Content = "Content 2", CollectionId = collectionId },
        };

        _mockRepository.Setup(p => p.GetByCollectionAsync(collectionId, CancellationToken.None)).ReturnsAsync(templates);

        // Act
        var result = await service.GetByCollectionAsync(collectionId);

        // Assert
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result.All(t => t.CollectionId == collectionId)).IsTrue();
    }
}
