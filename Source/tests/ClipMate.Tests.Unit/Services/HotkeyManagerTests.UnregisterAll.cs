using System.Windows;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using ClipMate.Core.Models;
using ClipMate.Platform;
using FakeItEasy;
using TUnit.Core.Executors;

namespace ClipMate.Tests.Unit.Services;

public partial class HotkeyManagerTests
{
    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task UnregisterAll_ShouldUnregisterAllHotkeys()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        A.CallTo(() => mockInterop.RegisterHotKey(A<HWND>.Ignored, A<int>.Ignored, A<HOT_KEY_MODIFIERS>.Ignored, A<uint>.Ignored))
            .Returns(true);

        A.CallTo(() => mockInterop.UnregisterHotKey(A<HWND>.Ignored, A<int>.Ignored))
            .Returns(true);

        var manager = new HotkeyManager(mockInterop);
        var window = new Window();
        manager.Initialize(window);
        var callback = () => { };

        manager.RegisterHotkey(ModifierKeys.Control, 0x56, callback);
        manager.RegisterHotkey(ModifierKeys.Alt, 0x43, callback);

        // Act
        manager.UnregisterAll();

        // Assert
        A.CallTo(() => mockInterop.UnregisterHotKey(A<HWND>.Ignored, A<int>.Ignored)).MustHaveHappened(2, Times.OrMore);
    }

    [Test]
    public async Task UnregisterAll_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        var manager = new HotkeyManager(mockInterop);
        manager.Dispose();

        // Act & Assert
        await Assert.That(() => manager.UnregisterAll())
            .Throws<ObjectDisposedException>();
    }
}
