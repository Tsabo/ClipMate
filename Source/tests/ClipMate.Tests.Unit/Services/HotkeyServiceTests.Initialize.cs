using ClipMate.Platform.Services;
using FakeItEasy;
using System.Windows;
using TUnit.Core.Executors;

namespace ClipMate.Tests.Unit.Services;

[NotInParallel(StaTestIsolationTests.StaKey)]
public partial class HotkeyServiceTests
{
    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task Initialize_WithValidWindow_ShouldCallManagerInitialize()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();
        var service = new HotkeyService(mockManager);
        var window = new Window();

        // Act
        service.Initialize(window);

        // Assert
        A.CallTo(() => mockManager.Initialize(window)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task Initialize_WithNullWindow_ShouldThrowArgumentNullException()
    {
        // Arrange
        var mockManager = CreateMockHotkeyManager();
        var service = new HotkeyService(mockManager);

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
        var service = new HotkeyService(mockManager);
        service.Dispose();
        var window = new Window();

        // Act & Assert
        await Assert.That(() => service.Initialize(window))
            .Throws<ObjectDisposedException>();
    }
}
