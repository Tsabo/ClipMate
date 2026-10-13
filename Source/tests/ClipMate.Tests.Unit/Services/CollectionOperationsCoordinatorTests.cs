using ClipMate.App.Services;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for CollectionOperationsCoordinator.
/// Note: Full integration tests require WPF context for ViewModels.
/// These tests focus on constructor parameter validation.
/// </summary>
public class CollectionOperationsCoordinatorTests
{
    private static IMessenger CreateDefaultMocks() => A.Fake<IMessenger>();

    // Constructor Tests
    [Test]
    public async Task CollectionCoordinator_NullActiveWindowService_ThrowsArgumentNullException()
    {
        // Arrange
        var messenger = CreateDefaultMocks();
        var logger = A.Fake<ILogger<CollectionOperationsCoordinator>>();

        // Act & Assert
        await Assert.That(() => new CollectionOperationsCoordinator(
                null!,
                null!, // IAutoAppendService
                null!, // ClipListViewModel - required but can be null for this test
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                messenger,
                null!,
                logger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task CollectionCoordinator_NullMessenger_ThrowsArgumentNullException()
    {
        // Arrange
        var activeWindow = A.Fake<IActiveWindowService>();
        var logger = A.Fake<ILogger<CollectionOperationsCoordinator>>();

        // Act & Assert
        await Assert.That(() => new CollectionOperationsCoordinator(
                activeWindow,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!, // messenger is null
                null!,
                logger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task CollectionCoordinator_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var activeWindow = A.Fake<IActiveWindowService>();
        var messenger = CreateDefaultMocks();

        // Act & Assert
        await Assert.That(() => new CollectionOperationsCoordinator(
                activeWindow,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                messenger,
                null!,
                null!)) // logger is null
            .Throws<ArgumentNullException>();
    }
}
