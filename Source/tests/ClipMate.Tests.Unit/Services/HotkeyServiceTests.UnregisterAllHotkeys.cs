using ClipMate.Core.Models;
using ClipMate.Platform.Services;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public partial class HotkeyServiceTests
{
    #region UnregisterAllHotkeys Tests

    [Test]
    public async Task UnregisterAllHotkeys_ShouldClearAllRegistrations()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();
        mockManager.Setup(m => m.RegisterHotkey(It.IsAny<Platform.ModifierKeys>(), It.IsAny<int>(), It.IsAny<Action>()))
            .Returns(1);
        
        var service = new HotkeyService(mockManager.Object);
        var action = () => { };
        
        service.RegisterHotkey(100, Core.Models.ModifierKeys.Control, 0x56, action);
        service.RegisterHotkey(101, Core.Models.ModifierKeys.Alt, 0x43, action);

        // Act
        service.UnregisterAllHotkeys();

        // Assert
        await Assert.That(service.IsHotkeyRegistered(100)).IsFalse();
        await Assert.That(service.IsHotkeyRegistered(101)).IsFalse();
        mockManager.Verify(m => m.UnregisterAll(), Times.Once);
    }

    [Test]
    public async Task UnregisterAllHotkeys_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();
        var service = new HotkeyService(mockManager.Object);
        service.Dispose();

        // Act & Assert
        await Assert.That(() => service.UnregisterAllHotkeys())
            .Throws<ObjectDisposedException>();
    }

    #endregion
}
