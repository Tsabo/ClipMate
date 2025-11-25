using ClipMate.Platform.Services;
using Moq;
using System.Windows;
using TUnit.Core.Executors;

namespace ClipMate.Tests.Unit.Services;

public partial class HotkeyServiceTests
{
    #region Initialize Tests

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task Initialize_WithValidWindow_ShouldCallManagerInitialize()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();
        var service = new HotkeyService(mockManager.Object);
        var window = new Window();

        // Act
        service.Initialize(window);

        // Assert
        mockManager.Verify(m => m.Initialize(window), Times.Once);
    }

    [Test]
    public async Task Initialize_WithNullWindow_ShouldThrowArgumentNullException()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();
        var service = new HotkeyService(mockManager.Object);

        // Act & Assert
        await Assert.That(() => service.Initialize(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task Initialize_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();
        var service = new HotkeyService(mockManager.Object);
        service.Dispose();
        var window = new Window();

        // Act & Assert
        await Assert.That(() => service.Initialize(window))
            .Throws<ObjectDisposedException>();
    }

    #endregion
}
