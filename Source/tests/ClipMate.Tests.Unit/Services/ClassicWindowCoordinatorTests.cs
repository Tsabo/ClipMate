using ClipMate.App;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public class ClassicWindowCoordinatorTests
{
    private static Mock<IMessenger> CreateDefaultMocks() => new();

    // Constructor Tests
    [Test]
    public async Task Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var messenger = CreateDefaultMocks();
        var logger = new Mock<ILogger<ClassicWindowCoordinator>>();

        // Act & Assert
        await Assert.That(() => new ClassicWindowCoordinator(null!, messenger.Object, logger.Object))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullMessenger_ThrowsArgumentNullException()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var logger = new Mock<ILogger<ClassicWindowCoordinator>>();

        // Act & Assert
        await Assert.That(() => new ClassicWindowCoordinator(serviceProvider.Object, null!, logger.Object))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var messenger = CreateDefaultMocks();

        // Act & Assert
        await Assert.That(() => new ClassicWindowCoordinator(serviceProvider.Object, messenger.Object, null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var messenger = CreateDefaultMocks();
        var logger = new Mock<ILogger<ClassicWindowCoordinator>>();

        // Act
        var coordinator = new ClassicWindowCoordinator(serviceProvider.Object, messenger.Object, logger.Object);

        // Assert
        await Assert.That(coordinator).IsNotNull();
    }

    // StartAsync Tests
    [Test]
    [Skip("Hotkey registration now handled by HotkeyCoordinator, not ClassicWindowCoordinator")]
    public async Task StartAsync_RegistersHotkey()
    {
        // This test is no longer valid as hotkey registration was moved to HotkeyCoordinator
        await Task.CompletedTask;
    }

    [Test]
    [Skip("Hotkey registration now handled by HotkeyCoordinator, not ClassicWindowCoordinator")]
    public async Task StartAsync_WhenHotkeyRegistrationFails_LogsWarning()
    {
        // This test is no longer valid as hotkey registration was moved to HotkeyCoordinator
        await Task.CompletedTask;
    }

    // StopAsync Tests
    [Test]
    [Skip("Hotkey unregistration now handled by HotkeyCoordinator, not ClassicWindowCoordinator")]
    public async Task StopAsync_UnregistersHotkey()
    {
        // This test is no longer valid as hotkey registration was moved to HotkeyCoordinator
        await Task.CompletedTask;
    }

    [Test]
    public async Task StopAsync_BeforeStart_DoesNotThrow()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var messenger = CreateDefaultMocks();
        var logger = new Mock<ILogger<ClassicWindowCoordinator>>();

        var coordinator = new ClassicWindowCoordinator(serviceProvider.Object, messenger.Object, logger.Object);

        // Act & Assert - should not throw
        await coordinator.StopAsync(CancellationToken.None);
    }

    // Dispose Tests
    [Test]
    public async Task Dispose_CalledOnce_DisposesResources()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var messenger = CreateDefaultMocks();
        var logger = new Mock<ILogger<ClassicWindowCoordinator>>();

        var coordinator = new ClassicWindowCoordinator(serviceProvider.Object, messenger.Object, logger.Object);

        // Act & Assert - should not throw
        await Task.CompletedTask;
        coordinator.Dispose();
    }

    [Test]
    public async Task Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var messenger = CreateDefaultMocks();
        var logger = new Mock<ILogger<ClassicWindowCoordinator>>();

        var coordinator = new ClassicWindowCoordinator(serviceProvider.Object, messenger.Object, logger.Object);

        // Act & Assert - should not throw
        await Task.CompletedTask;
        coordinator.Dispose();
        coordinator.Dispose();
        coordinator.Dispose();
    }
}
