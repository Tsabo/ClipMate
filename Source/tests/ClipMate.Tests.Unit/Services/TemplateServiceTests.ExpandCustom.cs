using ClipMate.Core.Models;
using Moq;
using TUnit.Core;
using TUnit.Assertions.Extensions;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for TemplateService ExpandTemplateAsync method with custom variables.
/// </summary>
public partial class TemplateServiceTests
{
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
}
