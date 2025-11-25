using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using Moq;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for ClipboardCoordinator clip processing functionality.
/// </summary>
public partial class ClipboardCoordinatorTests
{
    [Test]
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
            CapturedAt = DateTime.UtcNow
        };

        clipServiceMock.Setup(s => s.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(clip);

        var serviceProvider = CreateMockServiceProvider(clipService: clipServiceMock);
        var messenger = new Mock<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService.Object, serviceProvider, messenger.Object, logger);

        await coordinator.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Let background task start

        // Act
        await channel.Writer.WriteAsync(clip);
        await Task.Delay(200); // Give time to process

        // Cleanup
        channel.Writer.Complete();
        await coordinator.StopAsync(CancellationToken.None);

        // Assert
        clipServiceMock.Verify(s => s.CreateAsync(It.Is<Clip>(c => c.ContentHash == "hash123"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ProcessClipsAsync_WithFilteredClip_ShouldNotSaveClip()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var channel);
        var clipServiceMock = new Mock<IClipService>();
        var filterServiceMock = new Mock<IApplicationFilterService>();
        
        filterServiceMock.Setup(s => s.ShouldFilterAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Filter out the clip

        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Filtered content",
            ContentHash = "hash456",
            CapturedAt = DateTime.UtcNow,
            SourceApplicationName = "FilteredApp"
        };

        var serviceProvider = CreateMockServiceProvider(clipService: clipServiceMock, filterService: filterServiceMock);
        var messenger = new Mock<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService.Object, serviceProvider, messenger.Object, logger);

        await coordinator.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // Act
        await channel.Writer.WriteAsync(clip);
        await Task.Delay(200);

        // Cleanup
        channel.Writer.Complete();
        await coordinator.StopAsync(CancellationToken.None);

        // Assert
        clipServiceMock.Verify(s => s.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()), Times.Never);
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
            CapturedAt = DateTime.UtcNow
        };

        clipServiceMock.Setup(s => s.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(clip);

        var serviceProvider = CreateMockServiceProvider(clipService: clipServiceMock);
        var messengerMock = new Mock<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService.Object, serviceProvider, messengerMock.Object, logger);

        await coordinator.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // Act
        await channel.Writer.WriteAsync(clip);
        await Task.Delay(200);

        // Cleanup
        channel.Writer.Complete();
        await coordinator.StopAsync(CancellationToken.None);

        // Assert
        messengerMock.Verify(m => m.Send(It.IsAny<ClipAddedEvent>(), It.IsAny<string>()), Times.Once);
    }

    [Test]
    public async Task ProcessClipsAsync_WithMultipleClips_ShouldProcessAll()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var channel);
        var clipServiceMock = new Mock<IClipService>();
        
        clipServiceMock.Setup(s => s.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Clip c, CancellationToken ct) => c);

        var serviceProvider = CreateMockServiceProvider(clipService: clipServiceMock);
        var messenger = new Mock<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService.Object, serviceProvider, messenger.Object, logger);

        await coordinator.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        var clips = new[]
        {
            new Clip { Id = Guid.NewGuid(), Type = ClipType.Text, TextContent = "Clip 1", ContentHash = "hash1", CapturedAt = DateTime.UtcNow },
            new Clip { Id = Guid.NewGuid(), Type = ClipType.Text, TextContent = "Clip 2", ContentHash = "hash2", CapturedAt = DateTime.UtcNow },
            new Clip { Id = Guid.NewGuid(), Type = ClipType.Text, TextContent = "Clip 3", ContentHash = "hash3", CapturedAt = DateTime.UtcNow }
        };

        // Act
        foreach (var clip in clips)
        {
            await channel.Writer.WriteAsync(clip);
        }
        
        await Task.Delay(400); // Give time to process all clips

        // Cleanup
        channel.Writer.Complete();
        await coordinator.StopAsync(CancellationToken.None);

        // Assert
        clipServiceMock.Verify(s => s.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
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
            CapturedAt = DateTime.UtcNow
        };

        var existingClip = new Clip
        {
            Id = Guid.NewGuid(), // Different ID indicates it's an existing clip
            Type = ClipType.Text,
            TextContent = "Duplicate content",
            ContentHash = "duplicate-hash",
            CapturedAt = DateTime.UtcNow.AddMinutes(-1)
        };

        clipServiceMock.Setup(s => s.CreateAsync(It.IsAny<Clip>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingClip); // Return existing clip to simulate duplicate detection

        var serviceProvider = CreateMockServiceProvider(clipService: clipServiceMock);
        var messengerMock = new Mock<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService.Object, serviceProvider, messengerMock.Object, logger);

        await coordinator.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // Act
        await channel.Writer.WriteAsync(originalClip);
        await Task.Delay(200);

        // Cleanup
        channel.Writer.Complete();
        await coordinator.StopAsync(CancellationToken.None);

        // Assert - Event should still be sent for duplicates
        messengerMock.Verify(m => m.Send(It.Is<ClipAddedEvent>(e => e.WasDuplicate == true), It.IsAny<string>()), Times.Once);
    }
}
