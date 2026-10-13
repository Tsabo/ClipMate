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
    public async Task RegisterHotkey_WithValidParameters_ShouldReturnHotkeyId()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        A.CallTo(() => mockInterop.RegisterHotKey(A<HWND>.Ignored, A<int>.Ignored, A<HOT_KEY_MODIFIERS>.Ignored, A<uint>.Ignored))
            .Returns(true);

        var manager = new HotkeyManager(mockInterop);
        var window = new Window();
        manager.Initialize(window);
        var callback = () => { };

        // Act
        var hotkeyId = manager.RegisterHotkey(ModifierKeys.Control, 0x56, callback); // Ctrl+V

        // Assert
        await Assert.That(hotkeyId).IsGreaterThan(0);
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task RegisterHotkey_WhenWin32Fails_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        A.CallTo(() => mockInterop.RegisterHotKey(A<HWND>.Ignored, A<int>.Ignored, A<HOT_KEY_MODIFIERS>.Ignored, A<uint>.Ignored))
            .Returns(false); // Win32 registration fails

        var manager = new HotkeyManager(mockInterop);
        var window = new Window();
        manager.Initialize(window);
        var callback = () => { };

        // Act & Assert
        await Assert.That(() => manager.RegisterHotkey(ModifierKeys.Control, 0x56, callback))
            .Throws<InvalidOperationException>();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task RegisterHotkey_WithNullCallback_ShouldThrowArgumentNullException()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        var manager = new HotkeyManager(mockInterop);

        // Create and initialize window on same thread to avoid cross-thread issues
        var window = new Window();
        manager.Initialize(window);

        // Act & Assert
        await Assert.That(() => manager.RegisterHotkey(ModifierKeys.Control, 0x56, null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task RegisterHotkey_WhenNotInitialized_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        var manager = new HotkeyManager(mockInterop);
        var callback = () => { };

        // Act & Assert
        await Assert.That(() => manager.RegisterHotkey(ModifierKeys.Control, 0x56, callback))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task RegisterHotkey_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        var manager = new HotkeyManager(mockInterop);
        manager.Dispose();
        var callback = () => { };

        // Act & Assert
        await Assert.That(() => manager.RegisterHotkey(ModifierKeys.Control, 0x56, callback))
            .Throws<ObjectDisposedException>();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task RegisterHotkey_MultipleHotkeys_ShouldReturnUniqueIds()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        A.CallTo(() => mockInterop.RegisterHotKey(A<HWND>.Ignored, A<int>.Ignored, A<HOT_KEY_MODIFIERS>.Ignored, A<uint>.Ignored))
            .Returns(true);

        var manager = new HotkeyManager(mockInterop);
        var window = new Window();
        manager.Initialize(window);
        var callback = () => { };

        // Act
        var id1 = manager.RegisterHotkey(ModifierKeys.Control, 0x56, callback);
        var id2 = manager.RegisterHotkey(ModifierKeys.Alt, 0x43, callback);
        var id3 = manager.RegisterHotkey(ModifierKeys.Shift, 0x46, callback);

        // Assert
        await Assert.That(id1).IsNotEqualTo(id2);
        await Assert.That(id2).IsNotEqualTo(id3);
        await Assert.That(id1).IsNotEqualTo(id3);
    }
}
