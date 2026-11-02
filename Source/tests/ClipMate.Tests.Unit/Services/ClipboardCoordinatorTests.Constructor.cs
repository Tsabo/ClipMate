using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;

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
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        var autoAppendService = A.Fake<IAutoAppendService>();
        var serviceProvider = CreateMockServiceProvider();
        var messenger = A.Fake<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();

        // Act & Assert
        await Assert.That(() => new ClipboardCoordinator(null!, configurationService, A.Fake<ICurrentClipWriter>(),
            contextFactory, new ClipStore(contextFactory, NullLogger<ClipStore>.Instance), autoAppendService,
            serviceProvider, messenger, logger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullServiceProvider_ShouldThrowArgumentNullException()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var _);
        var configurationService = CreateMockConfigurationService();
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        var autoAppendService = A.Fake<IAutoAppendService>();
        var messenger = A.Fake<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();

        // Act & Assert
        await Assert.That(() => new ClipboardCoordinator(clipboardService, configurationService,
            A.Fake<ICurrentClipWriter>(), contextFactory, new ClipStore(contextFactory,
            NullLogger<ClipStore>.Instance), autoAppendService, null!, messenger, logger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullAutoAppendService_ShouldThrowArgumentNullException()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var _);
        var configurationService = CreateMockConfigurationService();
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        var serviceProvider = CreateMockServiceProvider();
        var messenger = A.Fake<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();

        // Act & Assert
        await Assert.That(() => new ClipboardCoordinator(clipboardService, configurationService,
            A.Fake<ICurrentClipWriter>(), contextFactory, new ClipStore(contextFactory,
            NullLogger<ClipStore>.Instance), null!, serviceProvider, messenger, logger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullMessenger_ShouldThrowArgumentNullException()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var _);
        var configurationService = CreateMockConfigurationService();
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        var autoAppendService = A.Fake<IAutoAppendService>();
        var serviceProvider = CreateMockServiceProvider();
        var logger = CreateLogger<ClipboardCoordinator>();

        // Act & Assert
        await Assert.That(() => new ClipboardCoordinator(clipboardService, configurationService,
            A.Fake<ICurrentClipWriter>(), contextFactory, new ClipStore(contextFactory,
            NullLogger<ClipStore>.Instance), autoAppendService, serviceProvider, null!, logger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var _);
        var configurationService = CreateMockConfigurationService();
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        var autoAppendService = A.Fake<IAutoAppendService>();
        var serviceProvider = CreateMockServiceProvider();
        var messenger = A.Fake<IMessenger>();

        // Act & Assert
        await Assert.That(() => new ClipboardCoordinator(clipboardService, configurationService,
            A.Fake<ICurrentClipWriter>(), contextFactory, new ClipStore(contextFactory,
            NullLogger<ClipStore>.Instance), autoAppendService, serviceProvider, messenger, null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var _);
        var configurationService = CreateMockConfigurationService();
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        var autoAppendService = A.Fake<IAutoAppendService>();
        var serviceProvider = CreateMockServiceProvider();
        var messenger = A.Fake<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();

        // Act
        var coordinator = new ClipboardCoordinator(clipboardService, configurationService,
            A.Fake<ICurrentClipWriter>(), contextFactory, new ClipStore(contextFactory,
            NullLogger<ClipStore>.Instance), autoAppendService, serviceProvider, messenger, logger);

        // Assert
        await Assert.That(coordinator).IsNotNull();
    }
}
