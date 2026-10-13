using ClipMate.App.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

public class ClassicWindowCoordinatorTests
{
    private static IMessenger CreateDefaultMocks() => A.Fake<IMessenger>();

    // Constructor Tests
    [Test]
    public async Task Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var messenger = CreateDefaultMocks();
        var logger = A.Fake<ILogger<ClassicWindowCoordinator>>();

        // Act & Assert
        await Assert.That(() => new ClassicWindowCoordinator(null!, messenger, logger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullMessenger_ThrowsArgumentNullException()
    {
        // Arrange
        var serviceProvider = A.Fake<IServiceProvider>();
        var logger = A.Fake<ILogger<ClassicWindowCoordinator>>();

        // Act & Assert
        await Assert.That(() => new ClassicWindowCoordinator(serviceProvider, null!, logger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var serviceProvider = A.Fake<IServiceProvider>();
        var messenger = CreateDefaultMocks();

        // Act & Assert
        await Assert.That(() => new ClassicWindowCoordinator(serviceProvider, messenger, null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var serviceProvider = A.Fake<IServiceProvider>();
        var messenger = CreateDefaultMocks();
        var logger = A.Fake<ILogger<ClassicWindowCoordinator>>();

        // Act
        var coordinator = new ClassicWindowCoordinator(serviceProvider, messenger, logger);

        // Assert
        await Assert.That(coordinator).IsNotNull();
    }

    [Test]
    public async Task StopAsync_BeforeStart_DoesNotThrow()
    {
        // Arrange
        var serviceProvider = A.Fake<IServiceProvider>();
        var messenger = CreateDefaultMocks();
        var logger = A.Fake<ILogger<ClassicWindowCoordinator>>();

        var coordinator = new ClassicWindowCoordinator(serviceProvider, messenger, logger);

        // Act & Assert - should not throw
        await coordinator.StopAsync(CancellationToken.None);
    }

    // Dispose Tests
    [Test]
    public async Task Dispose_CalledOnce_DisposesResources()
    {
        // Arrange
        var serviceProvider = A.Fake<IServiceProvider>();
        var messenger = CreateDefaultMocks();
        var logger = A.Fake<ILogger<ClassicWindowCoordinator>>();

        var coordinator = new ClassicWindowCoordinator(serviceProvider, messenger, logger);

        // Act & Assert - should not throw
        await Task.CompletedTask;
        coordinator.Dispose();
    }

    [Test]
    public async Task Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var serviceProvider = A.Fake<IServiceProvider>();
        var messenger = CreateDefaultMocks();
        var logger = A.Fake<ILogger<ClassicWindowCoordinator>>();

        using var coordinator = new ClassicWindowCoordinator(serviceProvider, messenger, logger);

        // Act & Assert - should not throw
        await Task.CompletedTask;
        coordinator.Dispose();
        coordinator.Dispose();
        coordinator.Dispose();
    }
}
