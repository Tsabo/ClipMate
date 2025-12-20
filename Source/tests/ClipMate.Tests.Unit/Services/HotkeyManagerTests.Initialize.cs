using System.Windows;
using ClipMate.Platform;
using TUnit.Core.Executors;

namespace ClipMate.Tests.Unit.Services;

public partial class HotkeyManagerTests
{
    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task Initialize_WithValidWindow_ShouldNotThrow()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        var manager = new HotkeyManager(mockInterop.Object);
        var window = new Window();

        // Act & Assert
        await Assert.That(() => manager.Initialize(window)).ThrowsNothing();
    }

    [Test]
    public async Task Initialize_WithNullWindow_ShouldThrowArgumentNullException()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        var manager = new HotkeyManager(mockInterop.Object);

        // Act & Assert
        await Assert.That(() => manager.Initialize(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task Initialize_WhenAlreadyInitialized_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        var manager = new HotkeyManager(mockInterop.Object);
        var window = new Window();
        manager.Initialize(window);

        // Act & Assert
        await Assert.That(() => manager.Initialize(window))
            .Throws<InvalidOperationException>();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task Initialize_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var mockInterop = CreateWin32HotkeyMock();
        var manager = new HotkeyManager(mockInterop.Object);
        manager.Dispose();
        var window = new Window();

        // Act & Assert
        await Assert.That(() => manager.Initialize(window))
            .Throws<ObjectDisposedException>();
    }
}
