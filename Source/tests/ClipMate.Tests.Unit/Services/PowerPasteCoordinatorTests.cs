using ClipMate.App;
using ClipMate.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace ClipMate.Tests.Unit.Services;

public class PowerPasteCoordinatorTests
{
    // Constructor Tests
    [Test]
    public async Task Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var hotkeyService = new Mock<IHotkeyService>();
        var logger = new Mock<ILogger<PowerPasteCoordinator>>();

        // Act & Assert
        await Assert.That(() => new PowerPasteCoordinator(null!, hotkeyService.Object, logger.Object))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullHotkeyService_ThrowsArgumentNullException()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var logger = new Mock<ILogger<PowerPasteCoordinator>>();

        // Act & Assert
        await Assert.That(() => new PowerPasteCoordinator(serviceProvider.Object, null!, logger.Object))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var hotkeyService = new Mock<IHotkeyService>();

        // Act & Assert
        await Assert.That(() => new PowerPasteCoordinator(serviceProvider.Object, hotkeyService.Object, null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var hotkeyService = new Mock<IHotkeyService>();
        var logger = new Mock<ILogger<PowerPasteCoordinator>>();

        // Act
        var coordinator = new PowerPasteCoordinator(serviceProvider.Object, hotkeyService.Object, logger.Object);

        // Assert
        await Assert.That(coordinator).IsNotNull();
    }

    // StartAsync Tests
    [Test]
    [Skip("Requires WPF Application.Current which is not available in unit tests")]
    public async Task StartAsync_RegistersHotkey()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var hotkeyService = new Mock<IHotkeyService>();
        var logger = new Mock<ILogger<PowerPasteCoordinator>>();

        hotkeyService.Setup(h => h.RegisterHotkey(
                It.IsAny<int>(),
                It.IsAny<ClipMate.Core.Models.ModifierKeys>(),
                It.IsAny<int>(),
                It.IsAny<Action>()))
            .Returns(true);

        var coordinator = new PowerPasteCoordinator(serviceProvider.Object, hotkeyService.Object, logger.Object);

        // Act
        await coordinator.StartAsync(CancellationToken.None);

        // Assert
        hotkeyService.Verify(h => h.RegisterHotkey(
            1001, // PowerPasteHotkeyId
            ClipMate.Core.Models.ModifierKeys.Control | ClipMate.Core.Models.ModifierKeys.Shift,
            It.IsAny<int>(),
            It.IsAny<Action>()), Times.Once);
    }

    [Test]
    [Skip("Requires WPF Application.Current which is not available in unit tests")]
    public async Task StartAsync_WhenHotkeyRegistrationFails_LogsWarning()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var hotkeyService = new Mock<IHotkeyService>();
        var logger = new Mock<ILogger<PowerPasteCoordinator>>();

        hotkeyService.Setup(h => h.RegisterHotkey(
                It.IsAny<int>(),
                It.IsAny<ClipMate.Core.Models.ModifierKeys>(),
                It.IsAny<int>(),
                It.IsAny<Action>()))
            .Returns(false);

        var coordinator = new PowerPasteCoordinator(serviceProvider.Object, hotkeyService.Object, logger.Object);

        // Act
        await coordinator.StartAsync(CancellationToken.None);

        // Assert - verify warning was logged
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to register")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // StopAsync Tests
    [Test]
    public async Task StopAsync_UnregistersHotkey()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var hotkeyService = new Mock<IHotkeyService>();
        var logger = new Mock<ILogger<PowerPasteCoordinator>>();

        hotkeyService.Setup(h => h.RegisterHotkey(
                It.IsAny<int>(),
                It.IsAny<ClipMate.Core.Models.ModifierKeys>(),
                It.IsAny<int>(),
                It.IsAny<Action>()))
            .Returns(true);

        var coordinator = new PowerPasteCoordinator(serviceProvider.Object, hotkeyService.Object, logger.Object);
        await coordinator.StartAsync(CancellationToken.None);

        // Act
        await coordinator.StopAsync(CancellationToken.None);

        // Assert
        hotkeyService.Verify(h => h.UnregisterHotkey(1001), Times.Once);
    }

    [Test]
    public async Task StopAsync_BeforeStart_DoesNotThrow()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var hotkeyService = new Mock<IHotkeyService>();
        var logger = new Mock<ILogger<PowerPasteCoordinator>>();

        var coordinator = new PowerPasteCoordinator(serviceProvider.Object, hotkeyService.Object, logger.Object);

        // Act & Assert - should not throw
        await coordinator.StopAsync(CancellationToken.None);
    }

    // Dispose Tests
    [Test]
    public async Task Dispose_CalledOnce_DisposesResources()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var hotkeyService = new Mock<IHotkeyService>();
        var logger = new Mock<ILogger<PowerPasteCoordinator>>();

        var coordinator = new PowerPasteCoordinator(serviceProvider.Object, hotkeyService.Object, logger.Object);

        // Act & Assert - should not throw
        coordinator.Dispose();
    }

    [Test]
    public async Task Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var hotkeyService = new Mock<IHotkeyService>();
        var logger = new Mock<ILogger<PowerPasteCoordinator>>();

        var coordinator = new PowerPasteCoordinator(serviceProvider.Object, hotkeyService.Object, logger.Object);

        // Act & Assert - should not throw
        coordinator.Dispose();
        coordinator.Dispose();
        coordinator.Dispose();
    }
}
