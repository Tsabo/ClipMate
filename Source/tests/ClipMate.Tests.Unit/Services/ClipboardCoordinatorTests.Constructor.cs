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
        var serviceProvider = CreateMockServiceProvider();
        var messenger = new Mock<IMessenger>().Object;
        var soundService = CreateMockSoundService();
        var logger = CreateLogger<ClipboardCoordinator>();

        // Act & Assert
        await Assert.That(() => new ClipboardCoordinator(null!, configurationService.Object, serviceProvider, messenger, soundService.Object, logger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullServiceProvider_ShouldThrowArgumentNullException()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var _);
        var messenger = new Mock<IMessenger>().Object;
        var soundService = CreateMockSoundService();
        var logger = CreateLogger<ClipboardCoordinator>();

        // Act & Assert
        await Assert.That(() => new ClipboardCoordinator(clipboardService.Object, null!, null!, messenger, soundService.Object, logger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullMessenger_ShouldThrowArgumentNullException()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var _);
        var configurationService = CreateMockConfigurationService();
        var serviceProvider = CreateMockServiceProvider();
        var soundService = CreateMockSoundService();
        var logger = CreateLogger<ClipboardCoordinator>();

        // Act & Assert
        await Assert.That(() => new ClipboardCoordinator(clipboardService.Object, configurationService.Object, serviceProvider, null!, soundService.Object, logger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var _);
        var configurationService = CreateMockConfigurationService();
        var serviceProvider = CreateMockServiceProvider();
        var messenger = new Mock<IMessenger>().Object;
        var soundService = CreateMockSoundService();

        // Act & Assert
        await Assert.That(() => new ClipboardCoordinator(clipboardService.Object, configurationService.Object, serviceProvider, messenger, soundService.Object, null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var _);
        var configurationService = CreateMockConfigurationService();
        var serviceProvider = CreateMockServiceProvider();
        var messenger = new Mock<IMessenger>().Object;
        var soundService = CreateMockSoundService();
        var logger = CreateLogger<ClipboardCoordinator>();

        // Act
        var coordinator = new ClipboardCoordinator(clipboardService.Object, configurationService.Object, serviceProvider, messenger, soundService.Object, logger);

        // Assert
        await Assert.That(coordinator).IsNotNull();
    }
}
