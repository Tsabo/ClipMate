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
    public async Task UnregisterHotkey_WithRegisteredId_ShouldReturnTrue()
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
        var hotkeyId = manager.RegisterHotkey(ModifierKeys.Control, 0x56, callback);

        // Act
        var result = manager.UnregisterHotkey(hotkeyId);

        // Assert
        await Assert.That(result).IsTrue();
        A.CallTo(() => mockInterop.UnregisterHotKey(A<HWND>.Ignored, hotkeyId)).MustHaveHappenedOnceExactly();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task UnregisterHotkey_WithUnregisteredId_ShouldReturnFalse()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        A.CallTo(() => mockInterop.RegisterHotKey(A<HWND>.Ignored, A<int>.Ignored, A<HOT_KEY_MODIFIERS>.Ignored, A<uint>.Ignored))
            .Returns(true);

        var manager = new HotkeyManager(mockInterop);
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
        var manager = new HotkeyManager(mockInterop);
        manager.Dispose();

        // Act & Assert
        await Assert.That(() => manager.UnregisterHotkey(1))
            .Throws<ObjectDisposedException>();
    }
}
