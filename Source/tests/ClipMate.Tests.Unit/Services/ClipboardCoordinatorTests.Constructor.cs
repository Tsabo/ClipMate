using ClipMate.Data;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Constructor validation tests for ClipboardCoordinator.
/// </summary>
public partial class ClipboardCoordinatorTests
{
    [Test]
    public async Task Constructor_WithNullClipboardService_ShouldThrowArgumentNullException()
    {
        // Arrange
        var configurationService = CreateMockConfigurationService();
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var serviceProvider = CreateMockServiceProvider();
        var messenger = new Mock<IMessenger>().Object;
        var logger = CreateLogger<ClipboardCoordinator>();

        // Act & Assert
        await Assert.That(() => new ClipboardCoordinator(null!, configurationService.Object, contextFactory.Object, serviceProvider, messenger, logger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullServiceProvider_ShouldThrowArgumentNullException()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var _);
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var messenger = new Mock<IMessenger>().Object;
        var logger = CreateLogger<ClipboardCoordinator>();

        // Act & Assert
        await Assert.That(() => new ClipboardCoordinator(clipboardService.Object, null!, contextFactory.Object, null!, messenger, logger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullMessenger_ShouldThrowArgumentNullException()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var _);
        var configurationService = CreateMockConfigurationService();
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var serviceProvider = CreateMockServiceProvider();
        var logger = CreateLogger<ClipboardCoordinator>();

        // Act & Assert
        await Assert.That(() => new ClipboardCoordinator(clipboardService.Object, configurationService.Object, contextFactory.Object, serviceProvider, null!, logger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var _);
        var configurationService = CreateMockConfigurationService();
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var serviceProvider = CreateMockServiceProvider();
        var messenger = new Mock<IMessenger>().Object;

        // Act & Assert
        await Assert.That(() => new ClipboardCoordinator(clipboardService.Object, configurationService.Object, contextFactory.Object, serviceProvider, messenger, null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var _);
        var configurationService = CreateMockConfigurationService();
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var serviceProvider = CreateMockServiceProvider();
        var messenger = new Mock<IMessenger>().Object;
        var logger = CreateLogger<ClipboardCoordinator>();

        // Act
        var coordinator = new ClipboardCoordinator(clipboardService.Object, configurationService.Object, contextFactory.Object, serviceProvider, messenger, logger);

        // Assert
        await Assert.That(coordinator).IsNotNull();
    }
}
