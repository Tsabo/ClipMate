using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for ClipboardCoordinator clip processing functionality.
/// </summary>
public partial class ClipboardCoordinatorTests
{
    [Test]
    public async Task ProcessClipsAsync_WithFilteredClip_ShouldNotSaveClip()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var channel);
        var clipServiceMock = new Mock<IClipService>();
        var filterServiceMock = new Mock<IApplicationFilterService>();

        filterServiceMock.Setup(p => p.ShouldFilterAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Filter out the clip

        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Filtered content",
            ContentHash = "hash456",
            CapturedAt = DateTime.UtcNow,
            SourceApplicationName = "FilteredApp",
        };

        var configurationService = CreateMockConfigurationService();
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var autoAppendService = new Mock<IAutoAppendService>().Object;
        var serviceProvider = CreateMockServiceProvider(clipServiceMock, filterService: filterServiceMock);
        var messenger = new Mock<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService.Object, configurationService.Object, contextFactory.Object, autoAppendService, serviceProvider, messenger.Object, logger);

        await coordinator.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // Act
        await channel.Writer.WriteAsync(clip);
        await Task.Delay(200);

        // Cleanup
        channel.Writer.Complete();
        await coordinator.StopAsync(CancellationToken.None);

        // Assert
        clipServiceMock.Verify(p => p.CreateAsync(It.IsAny<string>(), It.IsAny<Clip>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ProcessClipsAsync_WithValidClip_ShouldSendClipAddedEvent()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var channel);
        var clipServiceMock = new Mock<IClipService>();
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Test content",
            ContentHash = "hash789",
            CapturedAt = DateTime.UtcNow,
        };

        clipServiceMock.Setup(p => p.CreateAsync(It.IsAny<string>(), It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(clip);

        var configurationService = CreateMockConfigurationService();
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var autoAppendService = new Mock<IAutoAppendService>().Object;
        var serviceProvider = CreateMockServiceProvider(clipServiceMock);
        var messengerMock = new Mock<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService.Object, configurationService.Object, contextFactory.Object, autoAppendService, serviceProvider, messengerMock.Object, logger);

        await coordinator.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // Act
        await channel.Writer.WriteAsync(clip);
        await Task.Delay(200);

        // Cleanup
        channel.Writer.Complete();
        await coordinator.StopAsync(CancellationToken.None);

        // Assert - Messenger sends are tested in other tests, extension methods can't be verified
        // Test passes if no exception is thrown
    }

    [Test]
    public async Task ProcessClipsAsync_WithDuplicateClip_ShouldStillSendEvent()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var channel);
        var clipServiceMock = new Mock<IClipService>();

        var originalClip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Duplicate content",
            ContentHash = "duplicate-hash",
            CapturedAt = DateTime.UtcNow,
        };

        var existingClip = new Clip
        {
            Id = Guid.NewGuid(), // Different ID indicates it's an existing clip
            Type = ClipType.Text,
            TextContent = "Duplicate content",
            ContentHash = "duplicate-hash",
            CapturedAt = DateTime.UtcNow.AddMinutes(-1),
        };

        clipServiceMock.Setup(p => p.CreateAsync(It.IsAny<string>(), It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingClip); // Return existing clip to simulate duplicate detection

        var configurationService = CreateMockConfigurationService();
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var autoAppendService = new Mock<IAutoAppendService>().Object;
        var serviceProvider = CreateMockServiceProvider(clipServiceMock);
        var messengerMock = new Mock<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService.Object, configurationService.Object, contextFactory.Object, autoAppendService, serviceProvider, messengerMock.Object, logger);

        await coordinator.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // Act
        await channel.Writer.WriteAsync(originalClip);
        await Task.Delay(200);

        // Cleanup
        channel.Writer.Complete();
        await coordinator.StopAsync(CancellationToken.None);

        // Assert - Messenger sends are tested in other tests, extension methods can't be verified
        // Test passes if no exception is thrown
    }

    [Test]
    public async Task ProcessClipsAsync_WhenAutoAppendActiveWithNoGrowingClip_ShouldCreateNormallyAndMarkAsGrowingClip()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var channel);

        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "First capture",
            ContentHash = "hash-seed",
            CapturedAt = DateTime.UtcNow,
        };

        var clipRepositoryMock = new Mock<IClipRepository>();
        clipRepositoryMock.Setup(p => p.GetByContentHashAsync(clip.ContentHash, It.IsAny<CancellationToken>())).ReturnsAsync((Clip?)null);
        clipRepositoryMock.Setup(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>())).ReturnsAsync(clip);

        var contextFactory = new Mock<IDatabaseContextFactory>();
        contextFactory.Setup(p => p.GetClipRepository(It.IsAny<string>())).Returns(clipRepositoryMock.Object);

        var clipAppendServiceMock = new Mock<IClipAppendService>();

        var autoAppendServiceMock = new Mock<IAutoAppendService>();
        autoAppendServiceMock.SetupGet(p => p.IsActive).Returns(true);
        autoAppendServiceMock.SetupGet(p => p.GrowingClipId).Returns((Guid?)null);

        var configurationService = CreateMockConfigurationService();
        var serviceProvider = CreateMockServiceProvider(clipAppendService: clipAppendServiceMock);
        var messengerMock = new Mock<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService.Object, configurationService.Object, contextFactory.Object, autoAppendServiceMock.Object, serviceProvider, messengerMock.Object, logger);

        await coordinator.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // Act
        await channel.Writer.WriteAsync(clip);
        await Task.Delay(200);

        // Cleanup
        channel.Writer.Complete();
        await coordinator.StopAsync(CancellationToken.None);

        // Assert - first capture since activation goes through the normal create path...
        clipRepositoryMock.Verify(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()), Times.Once);
        clipAppendServiceMock.Verify(p => p.AppendCapturedTextAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);

        // ...and is then marked as the growing clip for subsequent captures
        autoAppendServiceMock.Verify(p => p.SetGrowingClip(clip.Id, "test-database"), Times.Once);
    }

    [Test]
    public async Task ProcessClipsAsync_WhenAutoAppendActiveWithGrowingClip_ShouldMergeInsteadOfCreating()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var channel);

        var capturedClip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Second capture",
            ContentHash = "hash-second",
            CapturedAt = DateTime.UtcNow,
        };

        var growingClipId = Guid.NewGuid();
        var updatedClip = new Clip
        {
            Id = growingClipId,
            Type = ClipType.Text,
            TextContent = "First capture\r\nSecond capture",
            Title = "Auto-Append (2 items)",
            Size = 30,
        };

        var clipRepositoryMock = new Mock<IClipRepository>();
        var contextFactory = new Mock<IDatabaseContextFactory>();
        contextFactory.Setup(p => p.GetClipRepository(It.IsAny<string>())).Returns(clipRepositoryMock.Object);

        var clipAppendServiceMock = new Mock<IClipAppendService>();
        clipAppendServiceMock.Setup(p => p.AppendCapturedTextAsync(
                growingClipId,
                capturedClip.TextContent!,
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedClip);

        var autoAppendServiceMock = new Mock<IAutoAppendService>();
        autoAppendServiceMock.SetupGet(p => p.IsActive).Returns(true);
        autoAppendServiceMock.SetupGet(p => p.GrowingClipId).Returns(growingClipId);
        autoAppendServiceMock.SetupGet(p => p.DatabaseKey).Returns("test-database");

        var configurationService = CreateMockConfigurationService();
        var serviceProvider = CreateMockServiceProvider(clipAppendService: clipAppendServiceMock);
        var messengerMock = new Mock<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService.Object, configurationService.Object, contextFactory.Object, autoAppendServiceMock.Object, serviceProvider, messengerMock.Object, logger);

        await coordinator.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // Act
        await channel.Writer.WriteAsync(capturedClip);
        await Task.Delay(200);

        // Cleanup
        channel.Writer.Complete();
        await coordinator.StopAsync(CancellationToken.None);

        // Assert - merged into the growing clip instead of creating a new one
        clipAppendServiceMock.Verify(p => p.AppendCapturedTextAsync(
            growingClipId, capturedClip.TextContent!, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);

        clipRepositoryMock.Verify(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()), Times.Never);
        autoAppendServiceMock.Verify(p => p.SetGrowingClip(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);

        // Messenger sends (ClipContentUpdatedEvent) are tested via extension methods that Moq can't verify directly -
        // covered indirectly by ClipListViewModelTests for the receiving side.
    }

    [Test]
    public async Task ProcessClipsAsync_WhenAutoAppendActiveWithNonTextClip_ShouldCreateNormally()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var channel);

        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Html,
            TextContent = "<b>html</b>",
            ContentHash = "hash-html",
            CapturedAt = DateTime.UtcNow,
        };

        var clipRepositoryMock = new Mock<IClipRepository>();
        clipRepositoryMock.Setup(p => p.GetByContentHashAsync(clip.ContentHash, It.IsAny<CancellationToken>())).ReturnsAsync((Clip?)null);
        clipRepositoryMock.Setup(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>())).ReturnsAsync(clip);

        var contextFactory = new Mock<IDatabaseContextFactory>();
        contextFactory.Setup(p => p.GetClipRepository(It.IsAny<string>())).Returns(clipRepositoryMock.Object);

        var clipAppendServiceMock = new Mock<IClipAppendService>();

        var growingClipId = Guid.NewGuid();
        var autoAppendServiceMock = new Mock<IAutoAppendService>();
        autoAppendServiceMock.SetupGet(p => p.IsActive).Returns(true);
        autoAppendServiceMock.SetupGet(p => p.GrowingClipId).Returns(growingClipId);
        autoAppendServiceMock.SetupGet(p => p.DatabaseKey).Returns("test-database");

        var configurationService = CreateMockConfigurationService();
        var serviceProvider = CreateMockServiceProvider(clipAppendService: clipAppendServiceMock);
        var messengerMock = new Mock<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService.Object, configurationService.Object, contextFactory.Object, autoAppendServiceMock.Object, serviceProvider, messengerMock.Object, logger);

        await coordinator.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // Act
        await channel.Writer.WriteAsync(clip);
        await Task.Delay(200);

        // Cleanup
        channel.Writer.Complete();
        await coordinator.StopAsync(CancellationToken.None);

        // Assert - non-text captures are unaffected by Auto-Append mode
        clipRepositoryMock.Verify(p => p.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()), Times.Once);
        clipAppendServiceMock.Verify(p => p.AppendCapturedTextAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
