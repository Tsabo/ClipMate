using System.Windows;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using ClipMate.Core.Models;
using ClipMate.Platform;
using Moq;
using TUnit.Core.Executors;

namespace ClipMate.Tests.Unit.Services;

public partial class HotkeyManagerTests
{
    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task UnregisterHotkey_WithRegisteredId_ShouldReturnTrue()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        mockInterop.Setup(p => p.RegisterHotKey(It.IsAny<HWND>(), It.IsAny<int>(), It.IsAny<HOT_KEY_MODIFIERS>(), It.IsAny<uint>()))
            .Returns(true);

        mockInterop.Setup(p => p.UnregisterHotKey(It.IsAny<HWND>(), It.IsAny<int>()))
            .Returns(true);

        var manager = new HotkeyManager(mockInterop.Object);
        var window = new Window();
        manager.Initialize(window);
        var callback = () => { };
        var hotkeyId = manager.RegisterHotkey(ModifierKeys.Control, 0x56, callback);

        // Act
        var result = manager.UnregisterHotkey(hotkeyId);

        // Assert
        await Assert.That(result).IsTrue();
        mockInterop.Verify(p => p.UnregisterHotKey(It.IsAny<HWND>(), hotkeyId), Times.Once);
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task UnregisterHotkey_WithUnregisteredId_ShouldReturnFalse()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        mockInterop.Setup(p => p.RegisterHotKey(It.IsAny<HWND>(), It.IsAny<int>(), It.IsAny<HOT_KEY_MODIFIERS>(), It.IsAny<uint>()))
            .Returns(true);

        var manager = new HotkeyManager(mockInterop.Object);
        var window = new Window();
        manager.Initialize(window);

        // Act
        var result = manager.UnregisterHotkey(999);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task UnregisterHotkey_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        var manager = new HotkeyManager(mockInterop.Object);
        manager.Dispose();

        // Act & Assert
        await Assert.That(() => manager.UnregisterHotkey(1))
            .Throws<ObjectDisposedException>();
    }
}
