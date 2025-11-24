using ClipMate.Core.Models;
using Moq;
using TUnit.Core;
using TUnit.Assertions.Extensions;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for TemplateService ExpandTemplateAsync error cases.
/// </summary>
public partial class TemplateServiceTests
{
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
}
