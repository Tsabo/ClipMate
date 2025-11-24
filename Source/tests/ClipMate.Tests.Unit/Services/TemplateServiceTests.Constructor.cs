using ClipMate.Core.Services;
using TUnit.Core;
using TUnit.Assertions.Extensions;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Constructor validation tests for TemplateService.
/// </summary>
public partial class TemplateServiceTests
{
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
}
