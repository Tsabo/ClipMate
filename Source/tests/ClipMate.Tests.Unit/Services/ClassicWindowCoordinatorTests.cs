using ClipMate.App;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public class ClassicWindowCoordinatorTests
{
    private static (Mock<IConfigurationService>, Mock<IMessenger>) CreateDefaultMocks()
    {
        var mockConfig = new Mock<IConfigurationService>();
        var mockMessenger = new Mock<IMessenger>();

        var config = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration(),
        };

        mockConfig.Setup(p => p.Configuration).Returns(config);

        return (mockConfig, mockMessenger);
    }

    // Constructor Tests
    [Test]
    public async Task Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var hotkeyService = new Mock<IHotkeyService>();
        var (configService, messenger) = CreateDefaultMocks();
        var logger = new Mock<ILogger<ClassicWindowCoordinator>>();

        // Act & Assert
        await Assert.That(() => new ClassicWindowCoordinator(null!, hotkeyService.Object, configService.Object, messenger.Object, logger.Object))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullHotkeyService_ThrowsArgumentNullException()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var (configService, messenger) = CreateDefaultMocks();
        var logger = new Mock<ILogger<ClassicWindowCoordinator>>();

        // Act & Assert
        await Assert.That(() => new ClassicWindowCoordinator(serviceProvider.Object, null!, configService.Object, messenger.Object, logger.Object))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var hotkeyService = new Mock<IHotkeyService>();
        var (configService, messenger) = CreateDefaultMocks();

        // Act & Assert
        await Assert.That(() => new ClassicWindowCoordinator(serviceProvider.Object, hotkeyService.Object, configService.Object, messenger.Object, null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var hotkeyService = new Mock<IHotkeyService>();
        var (configService, messenger) = CreateDefaultMocks();
        var logger = new Mock<ILogger<ClassicWindowCoordinator>>();

        // Act
        var coordinator = new ClassicWindowCoordinator(serviceProvider.Object, hotkeyService.Object, configService.Object, messenger.Object, logger.Object);

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
        var (configService, messenger) = CreateDefaultMocks();
        var logger = new Mock<ILogger<ClassicWindowCoordinator>>();

        hotkeyService.Setup(p => p.RegisterHotkey(
                It.IsAny<int>(),
                It.IsAny<ModifierKeys>(),
                It.IsAny<int>(),
                It.IsAny<Action>()))
            .Returns(true);

        var coordinator = new ClassicWindowCoordinator(serviceProvider.Object, hotkeyService.Object, configService.Object, messenger.Object, logger.Object);

        // Act
        await coordinator.StartAsync(CancellationToken.None);

        // Assert
        hotkeyService.Verify(p => p.RegisterHotkey(
            1001, // PowerPasteHotkeyId
            ModifierKeys.Control | ModifierKeys.Shift,
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
        var (configService, messenger) = CreateDefaultMocks();
        var logger = new Mock<ILogger<ClassicWindowCoordinator>>();

        hotkeyService.Setup(p => p.RegisterHotkey(
                It.IsAny<int>(),
                It.IsAny<ModifierKeys>(),
                It.IsAny<int>(),
                It.IsAny<Action>()))
            .Returns(false);

        var coordinator = new ClassicWindowCoordinator(serviceProvider.Object, hotkeyService.Object, configService.Object, messenger.Object, logger.Object);

        // Act
        await coordinator.StartAsync(CancellationToken.None);

        // Assert - verify warning was logged
        logger.Verify(
            p => p.Log(
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
        var (configService, messenger) = CreateDefaultMocks();
        var logger = new Mock<ILogger<ClassicWindowCoordinator>>();

        hotkeyService.Setup(p => p.RegisterHotkey(
                It.IsAny<int>(),
                It.IsAny<ModifierKeys>(),
                It.IsAny<int>(),
                It.IsAny<Action>()))
            .Returns(true);

        var coordinator = new ClassicWindowCoordinator(serviceProvider.Object, hotkeyService.Object, configService.Object, messenger.Object, logger.Object);
        await coordinator.StartAsync(CancellationToken.None);

        // Act
        await coordinator.StopAsync(CancellationToken.None);

        // Assert
        hotkeyService.Verify(p => p.UnregisterHotkey(1001), Times.Once);
    }

    [Test]
    public async Task StopAsync_BeforeStart_DoesNotThrow()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var hotkeyService = new Mock<IHotkeyService>();
        var (configService, messenger) = CreateDefaultMocks();
        var logger = new Mock<ILogger<ClassicWindowCoordinator>>();

        var coordinator = new ClassicWindowCoordinator(serviceProvider.Object, hotkeyService.Object, configService.Object, messenger.Object, logger.Object);

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
        var (configService, messenger) = CreateDefaultMocks();
        var logger = new Mock<ILogger<ClassicWindowCoordinator>>();

        var coordinator = new ClassicWindowCoordinator(serviceProvider.Object, hotkeyService.Object, configService.Object, messenger.Object, logger.Object);

        // Act & Assert - should not throw
        await Task.CompletedTask;
        coordinator.Dispose();
    }

    [Test]
    public async Task Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var hotkeyService = new Mock<IHotkeyService>();
        var (configService, messenger) = CreateDefaultMocks();
        var logger = new Mock<ILogger<ClassicWindowCoordinator>>();

        var coordinator = new ClassicWindowCoordinator(serviceProvider.Object, hotkeyService.Object, configService.Object, messenger.Object, logger.Object);

        // Act & Assert - should not throw
        await Task.CompletedTask;
        coordinator.Dispose();
        coordinator.Dispose();
        coordinator.Dispose();
    }
}
