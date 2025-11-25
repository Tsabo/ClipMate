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
    #region UnregisterAll Tests

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task UnregisterAll_ShouldUnregisterAllHotkeys()
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
        
        manager.RegisterHotkey(Core.Models.ModifierKeys.Control, 0x56, callback);
        manager.RegisterHotkey(Core.Models.ModifierKeys.Alt, 0x43, callback);

        // Act
        manager.UnregisterAll();

        // Assert
        mockInterop.Verify(w => w.UnregisterHotKey(It.IsAny<HWND>(), It.IsAny<int>()), Times.AtLeast(2));
    }

    [Test]
    public async Task UnregisterAll_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        var manager = new HotkeyManager(mockInterop.Object);
        manager.Dispose();

        // Act & Assert
        await Assert.That(() => manager.UnregisterAll())
            .Throws<ObjectDisposedException>();
    }

    #endregion
}
