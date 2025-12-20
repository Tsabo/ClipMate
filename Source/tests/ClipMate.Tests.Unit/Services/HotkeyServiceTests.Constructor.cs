using ClipMate.Platform.Services;

namespace ClipMate.Tests.Unit.Services;

public partial class HotkeyServiceTests
{
    [Test]
    public async Task Constructor_WithNullManager_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => new HotkeyService(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithValidManager_ShouldCreateInstance()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();

        // Act
        var service = new HotkeyService(mockManager.Object);

        // Assert
        await Assert.That(service).IsNotNull();
    }
}
