using ClipMate.Core.Models;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Basic CRUD operation tests for ClipService.
/// </summary>
public partial class ClipServiceTests
{
    [Test]
    [Category("CRUD")]
    public async Task GetByIdAsync_WithValidId_ShouldReturnClip()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        var expectedClip = CreateTestClip(clipId);
        MockClipRepository.Setup(p => p.GetByIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedClip);

        var service = CreateClipService();

        // Act
        var result = await service.GetByIdAsync(TestDatabaseKey, clipId);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(clipId);
    }

    [Test]
    [Category("CRUD")]
    public async Task GetByIdAsync_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var clipId = Guid.NewGuid();
        MockClipRepository.Setup(p => p.GetByIdAsync(clipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Clip?)null);

        var service = CreateClipService();

        // Act
        var result = await service.GetByIdAsync(TestDatabaseKey, clipId);

        // Assert
        await Assert.That(result).IsNull();
    }
}
