using ClipMate.Core.Models;
using Moq;

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
            UseCount = 5,
        };

        _mockRepository.Setup(p => p.GetByIdAsync(template.Id, CancellationToken.None)).ReturnsAsync(template);
        _mockRepository.Setup(p => p.UpdateAsync(It.IsAny<Template>(), CancellationToken.None)).ReturnsAsync(true);

        // Act
        await service.ExpandTemplateAsync(template.Id, new Dictionary<string, string> { { "NAME", "Test" } });

        // Assert
        _mockRepository.Verify(p => p.UpdateAsync(
                It.Is<Template>(t => t.Id == template.Id && t.UseCount == 6),
                CancellationToken.None),
            Times.Once);
    }
}
