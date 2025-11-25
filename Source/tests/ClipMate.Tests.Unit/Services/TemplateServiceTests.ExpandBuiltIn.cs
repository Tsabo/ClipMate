using ClipMate.Core.Models;
using Moq;
using TUnit.Core;
using TUnit.Assertions.Extensions;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for TemplateService ExpandTemplateAsync method with built-in variables.
/// </summary>
public partial class TemplateServiceTests
{
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
}
