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
    #region RegisterHotkey Tests

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task RegisterHotkey_WithValidParameters_ShouldReturnHotkeyId()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        mockInterop.Setup(w => w.RegisterHotKey(It.IsAny<HWND>(), It.IsAny<int>(), It.IsAny<HOT_KEY_MODIFIERS>(), It.IsAny<uint>()))
            .Returns(true);
        
        var manager = new HotkeyManager(mockInterop.Object);
        var window = new Window();
        manager.Initialize(window);
        var callback = () => { };

        // Act
        var hotkeyId = manager.RegisterHotkey(Core.Models.ModifierKeys.Control, 0x56, callback); // Ctrl+V

        // Assert
        await Assert.That(hotkeyId).IsGreaterThan(0);
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task RegisterHotkey_WhenWin32Fails_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        mockInterop.Setup(w => w.RegisterHotKey(It.IsAny<HWND>(), It.IsAny<int>(), It.IsAny<HOT_KEY_MODIFIERS>(), It.IsAny<uint>()))
            .Returns(false); // Win32 registration fails
        
        var manager = new HotkeyManager(mockInterop.Object);
        var window = new Window();
        manager.Initialize(window);
        var callback = () => { };

        // Act & Assert
        await Assert.That(() => manager.RegisterHotkey(Core.Models.ModifierKeys.Control, 0x56, callback))
            .Throws<InvalidOperationException>();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task RegisterHotkey_WithNullCallback_ShouldThrowArgumentNullException()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        var manager = new HotkeyManager(mockInterop.Object);
        
        // Create and initialize window on same thread to avoid cross-thread issues
        var window = new Window();
        manager.Initialize(window);

        // Act & Assert
        await Assert.That(() => manager.RegisterHotkey(Core.Models.ModifierKeys.Control, 0x56, null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task RegisterHotkey_WhenNotInitialized_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        var manager = new HotkeyManager(mockInterop.Object);
        var callback = () => { };

        // Act & Assert
        await Assert.That(() => manager.RegisterHotkey(Core.Models.ModifierKeys.Control, 0x56, callback))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task RegisterHotkey_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        var manager = new HotkeyManager(mockInterop.Object);
        manager.Dispose();
        var callback = () => { };

        // Act & Assert
        await Assert.That(() => manager.RegisterHotkey(Core.Models.ModifierKeys.Control, 0x56, callback))
            .Throws<ObjectDisposedException>();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task RegisterHotkey_MultipleHotkeys_ShouldReturnUniqueIds()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        mockInterop.Setup(w => w.RegisterHotKey(It.IsAny<HWND>(), It.IsAny<int>(), It.IsAny<HOT_KEY_MODIFIERS>(), It.IsAny<uint>()))
            .Returns(true);
        
        var manager = new HotkeyManager(mockInterop.Object);
        var window = new Window();
        manager.Initialize(window);
        var callback = () => { };

        // Act
        var id1 = manager.RegisterHotkey(Core.Models.ModifierKeys.Control, 0x56, callback);
        var id2 = manager.RegisterHotkey(Core.Models.ModifierKeys.Alt, 0x43, callback);
        var id3 = manager.RegisterHotkey(Core.Models.ModifierKeys.Shift, 0x46, callback);

        // Assert
        await Assert.That(id1).IsNotEqualTo(id2);
        await Assert.That(id2).IsNotEqualTo(id3);
        await Assert.That(id1).IsNotEqualTo(id3);
    }

    #endregion
}
