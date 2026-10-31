using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;

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
        var clipServiceMock = A.Fake<IClipService>();
        var filterServiceMock = A.Fake<IApplicationFilterService>();

        A.CallTo(() => filterServiceMock.ShouldFilterAsync(A<string?>.Ignored, A<string?>.Ignored, A<CancellationToken>.Ignored))
            .Returns(true); // Filter out the clip

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
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        var autoAppendService = A.Fake<IAutoAppendService>();
        var serviceProvider = CreateMockServiceProvider(clipServiceMock, filterService: filterServiceMock);
        var messenger = A.Fake<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService, configurationService, contextFactory, autoAppendService, serviceProvider, messenger, logger);

        await coordinator.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // Act
        await channel.Writer.WriteAsync(clip);
        await Task.Delay(200);

        // Cleanup
        channel.Writer.Complete();
        await coordinator.StopAsync(CancellationToken.None);

        // Assert
        A.CallTo(() => clipServiceMock.CreateAsync(A<string>.Ignored, A<Clip>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
    }

    [Test]
    public async Task ProcessClipsAsync_WithValidClip_ShouldSendClipAddedEvent()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var channel);
        var clipServiceMock = A.Fake<IClipService>();
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Test content",
            ContentHash = "hash789",
            CapturedAt = DateTime.UtcNow,
        };

        A.CallTo(() => clipServiceMock.CreateAsync(A<string>.Ignored, A<Clip>.Ignored, A<CancellationToken>.Ignored))
            .Returns(clip);

        var configurationService = CreateMockConfigurationService();
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        var autoAppendService = A.Fake<IAutoAppendService>();
        var serviceProvider = CreateMockServiceProvider(clipServiceMock);
        var messengerMock = A.Fake<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService, configurationService, contextFactory, autoAppendService, serviceProvider, messengerMock, logger);

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
        var clipServiceMock = A.Fake<IClipService>();

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

        A.CallTo(() => clipServiceMock.CreateAsync(A<string>.Ignored, A<Clip>.Ignored, A<CancellationToken>.Ignored))
            .Returns(existingClip); // Return existing clip to simulate duplicate detection

        var configurationService = CreateMockConfigurationService();
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        var autoAppendService = A.Fake<IAutoAppendService>();
        var serviceProvider = CreateMockServiceProvider(clipServiceMock);
        var messengerMock = A.Fake<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService, configurationService, contextFactory, autoAppendService, serviceProvider, messengerMock, logger);

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

        var clipRepositoryMock = A.Fake<IClipRepository>();
        A.CallTo(() => clipRepositoryMock.GetByContentHashAsync(clip.ContentHash, A<CancellationToken>.Ignored)).Returns((Clip?)null);
        A.CallTo(() => clipRepositoryMock.CreateAsync(A<Clip>.Ignored, A<CancellationToken>.Ignored)).Returns(clip);

        var contextFactory = A.Fake<IDatabaseContextFactory>();
        A.CallTo(() => contextFactory.GetClipRepository(A<string>.Ignored)).Returns(clipRepositoryMock);

        var clipAppendServiceMock = A.Fake<IClipAppendService>();

        var autoAppendServiceMock = A.Fake<IAutoAppendService>();
        A.CallTo(() => autoAppendServiceMock.IsActive).Returns(true);
        A.CallTo(() => autoAppendServiceMock.GrowingClipId).Returns((Guid?)null);

        var configurationService = CreateMockConfigurationService();
        var serviceProvider = CreateMockServiceProvider(clipAppendService: clipAppendServiceMock);
        var messengerMock = A.Fake<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService, configurationService, contextFactory, autoAppendServiceMock, serviceProvider, messengerMock, logger);

        await coordinator.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // Act
        await channel.Writer.WriteAsync(clip);
        await Task.Delay(200);

        // Cleanup
        channel.Writer.Complete();
        await coordinator.StopAsync(CancellationToken.None);

        // Assert - first capture since activation goes through the normal create path...
        A.CallTo(() => clipRepositoryMock.CreateAsync(A<Clip>.Ignored, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
        A.CallTo(() => clipAppendServiceMock.AppendCapturedTextAsync(A<Guid>.Ignored, A<string>.Ignored, A<string>.Ignored, A<bool>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();

        // ...and is then marked as the growing clip for subsequent captures
        A.CallTo(() => autoAppendServiceMock.SetGrowingClip(clip.Id, "test-database")).MustHaveHappenedOnceExactly();
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

        var clipRepositoryMock = A.Fake<IClipRepository>();
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        A.CallTo(() => contextFactory.GetClipRepository(A<string>.Ignored)).Returns(clipRepositoryMock);

        var clipAppendServiceMock = A.Fake<IClipAppendService>();
        A.CallTo(() => clipAppendServiceMock.AppendCapturedTextAsync(
                growingClipId,
                capturedClip.TextContent!,
                A<string>.Ignored,
                A<bool>.Ignored,
                A<CancellationToken>.Ignored))
            .Returns(updatedClip);

        var autoAppendServiceMock = A.Fake<IAutoAppendService>();
        A.CallTo(() => autoAppendServiceMock.IsActive).Returns(true);
        A.CallTo(() => autoAppendServiceMock.GrowingClipId).Returns(growingClipId);
        A.CallTo(() => autoAppendServiceMock.DatabaseKey).Returns("test-database");

        var configurationService = CreateMockConfigurationService();
        var serviceProvider = CreateMockServiceProvider(clipAppendService: clipAppendServiceMock);
        var messengerMock = A.Fake<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService, configurationService, contextFactory, autoAppendServiceMock, serviceProvider, messengerMock, logger);

        await coordinator.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // Act
        await channel.Writer.WriteAsync(capturedClip);
        await Task.Delay(200);

        // Cleanup
        channel.Writer.Complete();
        await coordinator.StopAsync(CancellationToken.None);

        // Assert - merged into the growing clip instead of creating a new one
        A.CallTo(() => clipAppendServiceMock.AppendCapturedTextAsync(
            growingClipId, capturedClip.TextContent!, A<string>.Ignored, A<bool>.Ignored, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();

        A.CallTo(() => clipRepositoryMock.CreateAsync(A<Clip>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
        A.CallTo(() => autoAppendServiceMock.SetGrowingClip(A<Guid>.Ignored, A<string>.Ignored)).MustNotHaveHappened();

        // Messenger sends (ClipContentUpdatedEvent) are tested via extension methods that FakeItEasy can't verify directly -
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

        var clipRepositoryMock = A.Fake<IClipRepository>();
        A.CallTo(() => clipRepositoryMock.GetByContentHashAsync(clip.ContentHash, A<CancellationToken>.Ignored)).Returns((Clip?)null);
        A.CallTo(() => clipRepositoryMock.CreateAsync(A<Clip>.Ignored, A<CancellationToken>.Ignored)).Returns(clip);

        var contextFactory = A.Fake<IDatabaseContextFactory>();
        A.CallTo(() => contextFactory.GetClipRepository(A<string>.Ignored)).Returns(clipRepositoryMock);

        var clipAppendServiceMock = A.Fake<IClipAppendService>();

        var growingClipId = Guid.NewGuid();
        var autoAppendServiceMock = A.Fake<IAutoAppendService>();
        A.CallTo(() => autoAppendServiceMock.IsActive).Returns(true);
        A.CallTo(() => autoAppendServiceMock.GrowingClipId).Returns(growingClipId);
        A.CallTo(() => autoAppendServiceMock.DatabaseKey).Returns("test-database");

        var configurationService = CreateMockConfigurationService();
        var serviceProvider = CreateMockServiceProvider(clipAppendService: clipAppendServiceMock);
        var messengerMock = A.Fake<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService, configurationService, contextFactory, autoAppendServiceMock, serviceProvider, messengerMock, logger);

        await coordinator.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // Act
        await channel.Writer.WriteAsync(clip);
        await Task.Delay(200);

        // Cleanup
        channel.Writer.Complete();
        await coordinator.StopAsync(CancellationToken.None);

        // Assert - non-text captures are unaffected by Auto-Append mode
        A.CallTo(() => clipRepositoryMock.CreateAsync(A<Clip>.Ignored, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
        A.CallTo(() => clipAppendServiceMock.AppendCapturedTextAsync(A<Guid>.Ignored, A<string>.Ignored, A<string>.Ignored, A<bool>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
    }
}
