using ClipMate.Core.Models;
using ClipMate.Platform.Services;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

public partial class HotkeyServiceTests
{
    [Test]
    public async Task IsHotkeyRegistered_WithRegisteredId_ShouldReturnTrue()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();
        A.CallTo(() => mockManager.RegisterHotkey(A<ModifierKeys>.Ignored, A<int>.Ignored, A<Action>.Ignored))
            .Returns(1);

        var service = new HotkeyService(mockManager);
        var action = () => { };
        service.RegisterHotkey(100, ModifierKeys.Control, 0x56, action);

        // Act
        var result = service.IsHotkeyRegistered(100);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsHotkeyRegistered_WithUnregisteredId_ShouldReturnFalse()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();
        var service = new HotkeyService(mockManager);

        // Act
        var result = service.IsHotkeyRegistered(999);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsHotkeyRegistered_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();
        var service = new HotkeyService(mockManager);
        service.Dispose();

        // Act & Assert
        await Assert.That(() => service.IsHotkeyRegistered(100))
            .Throws<ObjectDisposedException>();
    }
}
