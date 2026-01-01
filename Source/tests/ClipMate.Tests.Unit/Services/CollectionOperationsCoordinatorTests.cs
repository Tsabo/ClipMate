using ClipMate.App.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for CollectionOperationsCoordinator.
/// Note: Full integration tests require WPF context for ViewModels.
/// These tests focus on constructor parameter validation.
/// </summary>
public class CollectionOperationsCoordinatorTests
{
    private static Mock<IMessenger> CreateDefaultMocks() => new();

    // Constructor Tests
    [Test]
    public async Task CollectionCoordinator_NullActiveWindowService_ThrowsArgumentNullException()
    {
        // Arrange
        var messenger = CreateDefaultMocks();
        var logger = new Mock<ILogger<CollectionOperationsCoordinator>>();

        // Act & Assert
        await Assert.That(() => new CollectionOperationsCoordinator(
                null!,
                null!, // ClipListViewModel - required but can be null for this test
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                messenger.Object,
                null!,
                logger.Object))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task CollectionCoordinator_NullMessenger_ThrowsArgumentNullException()
    {
        // Arrange
        var activeWindow = new Mock<IActiveWindowService>();
        var logger = new Mock<ILogger<CollectionOperationsCoordinator>>();

        // Act & Assert
        await Assert.That(() => new CollectionOperationsCoordinator(
                activeWindow.Object,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!, // messenger is null
                null!,
                logger.Object))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task CollectionCoordinator_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var activeWindow = new Mock<IActiveWindowService>();
        var messenger = CreateDefaultMocks();

        // Act & Assert
        await Assert.That(() => new CollectionOperationsCoordinator(
                activeWindow.Object,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                messenger.Object,
                null!,
                null!)) // logger is null
            .Throws<ArgumentNullException>();
    }
}
