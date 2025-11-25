using ClipMate.Core.Models;
using Moq;
using TUnit.Core;
using TUnit.Assertions.Extensions;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for TemplateService UseCount tracking.
/// </summary>
public partial class TemplateServiceTests
{
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
}
