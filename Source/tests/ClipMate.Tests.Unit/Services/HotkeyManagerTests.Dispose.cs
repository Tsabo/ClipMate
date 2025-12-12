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
    #region Dispose Tests

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task Dispose_ShouldUnregisterAllHotkeys()
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
        using var manager2 = manager;
        manager2.RegisterHotkey(ModifierKeys.Control, 0x56, callback);
        manager2.RegisterHotkey(ModifierKeys.Alt, 0x43, callback);

        // Act
        manager2.Dispose();

        // Assert
        mockInterop.Verify(w => w.UnregisterHotKey(It.IsAny<HWND>(), It.IsAny<int>()), Times.AtLeast(2));
    }

    [Test]
    public async Task Dispose_CalledMultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        var manager = new HotkeyManager(mockInterop.Object);

        // Act & Assert
        await Assert.That(() =>
            {
                manager.Dispose();
                manager.Dispose();
                manager.Dispose();
            })
            .ThrowsNothing();
    }

    #endregion
}
