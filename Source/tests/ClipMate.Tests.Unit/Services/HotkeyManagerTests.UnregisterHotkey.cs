using ClipMate.Platform;
using ClipMate.Platform.Interop;
using Moq;
using System.Windows;
using TUnit.Core.Executors;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace ClipMate.Tests.Unit.Services;

public partial class HotkeyManagerTests
{
    #region UnregisterHotkey Tests

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task UnregisterHotkey_WithRegisteredId_ShouldReturnTrue()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        mockInterop.Setup(w => w.RegisterHotKey(It.IsAny<HWND>(), It.IsAny<int>(), It.IsAny<HOT_KEY_MODIFIERS>(), It.IsAny<uint>()))
            .Returns(true);
        mockInterop.Setup(w => w.UnregisterHotKey(It.IsAny<HWND>(), It.IsAny<int>()))
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
        mockInterop.Verify(w => w.UnregisterHotKey(It.IsAny<HWND>(), hotkeyId), Times.Once);
    }

    [Test]
    public async Task UnregisterHotkey_WithUnregisteredId_ShouldReturnFalse()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        mockInterop.Setup(w => w.RegisterHotKey(It.IsAny<HWND>(), It.IsAny<int>(), It.IsAny<HOT_KEY_MODIFIERS>(), It.IsAny<uint>()))
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

    #endregion
}
