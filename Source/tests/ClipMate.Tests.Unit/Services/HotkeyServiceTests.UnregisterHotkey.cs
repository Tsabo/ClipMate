using ClipMate.Core.Models;
using ClipMate.Platform.Services;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public partial class HotkeyServiceTests
{
    #region UnregisterHotkey Tests

    [Test]
    public async Task UnregisterHotkey_WithRegisteredId_ShouldReturnTrue()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();
        mockManager.Setup(m => m.RegisterHotkey(It.IsAny<Platform.ModifierKeys>(), It.IsAny<int>(), It.IsAny<Action>()))
            .Returns(1);
        
        var service = new HotkeyService(mockManager.Object);
        var action = () => { };
        service.RegisterHotkey(100, Core.Models.ModifierKeys.Control, 0x56, action);

        // Act
        var result = service.UnregisterHotkey(100);

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(service.IsHotkeyRegistered(100)).IsFalse();
    }

    [Test]
    public async Task UnregisterHotkey_WithUnregisteredId_ShouldReturnFalse()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();
        var service = new HotkeyService(mockManager.Object);

        // Act
        var result = service.UnregisterHotkey(999);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task UnregisterHotkey_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();
        var service = new HotkeyService(mockManager.Object);
        service.Dispose();

        // Act & Assert
        await Assert.That(() => service.UnregisterHotkey(100))
            .Throws<ObjectDisposedException>();
    }

    #endregion
}
