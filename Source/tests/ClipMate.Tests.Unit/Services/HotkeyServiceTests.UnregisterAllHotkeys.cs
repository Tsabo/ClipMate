using ClipMate.Core.Models;
using ClipMate.Platform.Services;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public partial class HotkeyServiceTests
{
    [Test]
    public async Task UnregisterAllHotkeys_ShouldClearAllRegistrations()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();
        mockManager.Setup(p => p.RegisterHotkey(It.IsAny<ModifierKeys>(), It.IsAny<int>(), It.IsAny<Action>()))
            .Returns(1);

        var service = new HotkeyService(mockManager.Object);
        var action = () => { };

        service.RegisterHotkey(100, ModifierKeys.Control, 0x56, action);
        service.RegisterHotkey(101, ModifierKeys.Alt, 0x43, action);

        // Act
        service.UnregisterAllHotkeys();

        // Assert
        await Assert.That(service.IsHotkeyRegistered(100)).IsFalse();
        await Assert.That(service.IsHotkeyRegistered(101)).IsFalse();
        mockManager.Verify(p => p.UnregisterAll(), Times.Once);
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
}
