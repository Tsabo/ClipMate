using ClipMate.Core.Models;
using ClipMate.Platform.Services;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public partial class HotkeyServiceTests
{
    #region RegisterHotkey Tests

    [Test]
    public async Task RegisterHotkey_WithValidParameters_ShouldReturnTrue()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();
        mockManager.Setup(m => m.RegisterHotkey(It.IsAny<Platform.ModifierKeys>(), It.IsAny<int>(), It.IsAny<Action>()))
            .Returns(1);
        
        var service = new HotkeyService(mockManager.Object);
        var action = () => { };

        // Act
        var result = service.RegisterHotkey(100, Core.Models.ModifierKeys.Control, 0x56, action); // Ctrl+V

        // Assert
        await Assert.That(result).IsTrue();
        await Assert.That(service.IsHotkeyRegistered(100)).IsTrue();
    }

    [Test]
    public async Task RegisterHotkey_WithNullAction_ShouldThrowArgumentNullException()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();
        var service = new HotkeyService(mockManager.Object);

        // Act & Assert
        await Assert.That(() => service.RegisterHotkey(100, Core.Models.ModifierKeys.Control, 0x56, null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task RegisterHotkey_WhenAlreadyRegistered_ShouldUnregisterFirst()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();
        mockManager.Setup(m => m.RegisterHotkey(It.IsAny<Platform.ModifierKeys>(), It.IsAny<int>(), It.IsAny<Action>()))
            .Returns(1);
        
        var service = new HotkeyService(mockManager.Object);
        var action1 = () => { };
        var action2 = () => { };

        // Act - Register twice with same ID
        var result1 = service.RegisterHotkey(100, Core.Models.ModifierKeys.Control, 0x56, action1);
        var result2 = service.RegisterHotkey(100, Core.Models.ModifierKeys.Alt, 0x43, action2);

        // Assert
        await Assert.That(result1).IsTrue();
        await Assert.That(result2).IsTrue();
        await Assert.That(service.IsHotkeyRegistered(100)).IsTrue();
    }

    [Test]
    public async Task RegisterHotkey_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();
        var service = new HotkeyService(mockManager.Object);
        service.Dispose();
        var action = () => { };

        // Act & Assert
        await Assert.That(() => service.RegisterHotkey(100, Core.Models.ModifierKeys.Control, 0x56, action))
            .Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task RegisterHotkey_WithModifierCombination_ShouldConvertModifiersCorrectly()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();
        var capturedModifiers = Platform.ModifierKeys.None;
        
        mockManager.Setup(m => m.RegisterHotkey(It.IsAny<Platform.ModifierKeys>(), It.IsAny<int>(), It.IsAny<Action>()))
            .Callback<Platform.ModifierKeys, int, Action>((mods, key, action) => capturedModifiers = mods)
            .Returns(1);
        
        var service = new HotkeyService(mockManager.Object);
        var action = () => { };

        // Act
        var modifiers = Core.Models.ModifierKeys.Control | Core.Models.ModifierKeys.Alt | Core.Models.ModifierKeys.Shift;
        service.RegisterHotkey(100, modifiers, 0x56, action);

        // Assert
        var expectedModifiers = Platform.ModifierKeys.Control | Platform.ModifierKeys.Alt | Platform.ModifierKeys.Shift;
        await Assert.That(capturedModifiers).IsEqualTo(expectedModifiers);
    }

    #endregion
}
