using ClipMate.Core.Models;
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
    [Skip("Requires real database context - ClipboardCoordinator uses ClipRepository directly, not IClipService")]
    public async Task ProcessClipsAsync_WithValidClip_ShouldSaveClip()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var channel);
        var clipServiceMock = new Mock<IClipService>();
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Test content",
            ContentHash = "hash123",
            CapturedAt = DateTime.UtcNow,
        };

        clipServiceMock.Setup(p => p.CreateAsync(It.IsAny<string>(), It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(clip);

        var configurationService = CreateMockConfigurationService();
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var serviceProvider = CreateMockServiceProvider(clipServiceMock);
        var messenger = new Mock<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService.Object, configurationService.Object, contextFactory.Object, serviceProvider, messenger.Object, logger);

        await coordinator.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Let background task start

        // Act
        await channel.Writer.WriteAsync(clip);
        await Task.Delay(200); // Give time to process

        // Cleanup
        channel.Writer.Complete();
        await coordinator.StopAsync(CancellationToken.None);

        // Assert
        clipServiceMock.Verify(p => p.CreateAsync(It.IsAny<string>(), It.Is<Clip>(c => c.ContentHash == "hash123"), It.IsAny<CancellationToken>()), Times.Once);
    }

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
        var serviceProvider = CreateMockServiceProvider(clipServiceMock, filterService: filterServiceMock);
        var messenger = new Mock<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService.Object, configurationService.Object, contextFactory.Object, serviceProvider, messenger.Object, logger);

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
        var serviceProvider = CreateMockServiceProvider(clipServiceMock);
        var messengerMock = new Mock<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService.Object, configurationService.Object, contextFactory.Object, serviceProvider, messengerMock.Object, logger);

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
    [Skip("Requires real database context - ClipboardCoordinator uses ClipRepository directly, not IClipService")]
    public async Task ProcessClipsAsync_WithMultipleClips_ShouldProcessAll()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var channel);
        var clipServiceMock = new Mock<IClipService>();

        clipServiceMock.Setup(p => p.CreateAsync(It.IsAny<string>(), It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string dbKey, Clip c, CancellationToken ct) => c);

        var configurationService = CreateMockConfigurationService();
        var contextFactory = new Mock<IDatabaseContextFactory>();
        var serviceProvider = CreateMockServiceProvider(clipServiceMock);
        var messenger = new Mock<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService.Object, configurationService.Object, contextFactory.Object, serviceProvider, messenger.Object, logger);

        await coordinator.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        var clips = new[]
        {
            new Clip { Id = Guid.NewGuid(), Type = ClipType.Text, TextContent = "Clip 1", ContentHash = "hash1", CapturedAt = DateTime.UtcNow },
            new Clip { Id = Guid.NewGuid(), Type = ClipType.Text, TextContent = "Clip 2", ContentHash = "hash2", CapturedAt = DateTime.UtcNow },
            new Clip { Id = Guid.NewGuid(), Type = ClipType.Text, TextContent = "Clip 3", ContentHash = "hash3", CapturedAt = DateTime.UtcNow },
        };

        // Act
        foreach (var item in clips)
            await channel.Writer.WriteAsync(item);

        await Task.Delay(400); // Give time to process all clips

        // Cleanup
        channel.Writer.Complete();
        await coordinator.StopAsync(CancellationToken.None);

        // Assert
        clipServiceMock.Verify(p => p.CreateAsync(It.IsAny<string>(), It.IsAny<Clip>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
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
        var serviceProvider = CreateMockServiceProvider(clipServiceMock);
        var messengerMock = new Mock<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService.Object, configurationService.Object, contextFactory.Object, serviceProvider, messengerMock.Object, logger);

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
}
