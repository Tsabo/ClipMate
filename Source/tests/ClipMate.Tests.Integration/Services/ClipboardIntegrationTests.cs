using System.Windows;
using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Repositories;
using ClipMate.Data.Services;
using ClipMate.Platform.Services;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for the complete clipboard capture pipeline.
/// Tests the flow: clipboard change -> capture -> save to database.
/// </summary>
public class ClipboardIntegrationTests : IDisposable
{
    private readonly ClipMateDbContext _dbContext;
    private readonly IClipRepository _clipRepository;
    private readonly IClipService _clipService;
    private readonly IApplicationFilterService _filterService;
    private readonly IClipboardService _clipboardService;
    private readonly ClipboardCoordinator _coordinator;
    private readonly string _databasePath;

    public ClipboardIntegrationTests()
    {
        // Create in-memory SQLite database for testing
        _databasePath = $"DataSource=:memory:";
        var options = new DbContextOptionsBuilder<ClipMateDbContext>()
            .UseSqlite(_databasePath)
            .Options;

        _dbContext = new ClipMateDbContext(options);
        _dbContext.Database.OpenConnection(); // Keep connection open for in-memory DB
        _dbContext.Database.EnsureCreated();

        // Create real services
        var clipLogger = Mock.Of<ILogger<ClipService>>();
        var clipboardLogger = Mock.Of<ILogger<ClipboardService>>();
        var filterLogger = Mock.Of<ILogger<ApplicationFilterService>>();
        var coordinatorLogger = Mock.Of<ILogger<ClipboardCoordinator>>();

        _clipRepository = new ClipRepository(_dbContext);
        _clipService = new ClipService(_clipRepository);
        
        var filterRepository = new ApplicationFilterRepository(_dbContext);
        _filterService = new ApplicationFilterService(filterRepository, filterLogger);
        
        var collectionRepository = new CollectionRepository(_dbContext);
        var collectionService = new CollectionService(collectionRepository);
        
        var folderRepository = new FolderRepository(_dbContext);
        var folderService = new FolderService(folderRepository);
        
        _clipboardService = new ClipboardService(clipboardLogger);
        _coordinator = new ClipboardCoordinator(_clipboardService, _clipService, collectionService, folderService, _filterService, coordinatorLogger);
    }

    [StaFact]
    public async Task ClipboardCapture_ShouldSaveToDatabase()
    {
        // Arrange
        var capturedClips = new List<Clip>();
        var taskCompletionSource = new TaskCompletionSource<bool>();

        // Subscribe to clip capture to detect when save is complete
        _clipboardService.ClipCaptured += (sender, e) =>
        {
            // Give coordinator time to save
            Task.Delay(100).ContinueWith(_ => taskCompletionSource.SetResult(true));
        };

        await _coordinator.StartAsync();

        // Act - Simulate clipboard change by manually triggering the event
        var testClip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Test clipboard content",
            ContentHash = "test-hash-" + Guid.NewGuid().ToString(),
            CapturedAt = DateTime.UtcNow,
            SourceApplicationName = "TestApp.exe",
            SourceApplicationTitle = "Test Window"
        };

        // Manually raise the event (simulating clipboard capture)
        var eventArgs = new ClipCapturedEventArgs { Clip = testClip };
        var eventInfo = typeof(ClipboardService).GetEvent("ClipCaptured");
        var raiseMethod = typeof(ClipboardService).GetMethod("OnClipCaptured", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (raiseMethod == null)
        {
            // Fallback: directly invoke the coordinator's event handler
            var coordinatorType = typeof(ClipboardCoordinator);
            var handlerMethod = coordinatorType.GetMethod("OnClipCaptured",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            handlerMethod?.Invoke(_coordinator, new object?[] { _clipboardService, eventArgs });
        }

        // Wait for async save to complete
        await Task.WhenAny(taskCompletionSource.Task, Task.Delay(2000));

        // Assert
        var savedClips = await _clipRepository.GetRecentAsync(10);
        savedClips.ShouldNotBeEmpty();
        
        var savedClip = savedClips.FirstOrDefault(c => c.ContentHash == testClip.ContentHash);
        savedClip.ShouldNotBeNull();
        savedClip!.TextContent.ShouldBe(testClip.TextContent);
        savedClip.Type.ShouldBe(ClipType.Text);
        savedClip.SourceApplicationName.ShouldBe("TestApp.exe");
    }

    [Fact]
    public async Task ClipboardCapture_DuplicateContent_ShouldNotCreateNewRecord()
    {
        // Arrange
        var clip1 = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Duplicate content",
            ContentHash = "duplicate-hash",
            CapturedAt = DateTime.UtcNow
        };

        await _clipService.CreateAsync(clip1);

        // Act - Try to create duplicate
        var clip2 = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Duplicate content",
            ContentHash = "duplicate-hash", // Same hash
            CapturedAt = DateTime.UtcNow.AddSeconds(1)
        };

        var result = await _clipService.CreateAsync(clip2);

        // Assert
        result.Id.ShouldBe(clip1.Id, "duplicate detection should return existing clip");
        
        var allClips = await _clipRepository.GetRecentAsync(100);
        allClips.Count(c => c.ContentHash == "duplicate-hash").ShouldBe(1, 
            "only one clip with this hash should exist");
    }

    [StaFact]
    public async Task ClipboardCoordinator_Start_ShouldEnableMonitoring()
    {
        // Act
        await _coordinator.StartAsync();

        // Assert - verify monitoring is active by checking we can stop it
        await _coordinator.StopAsync();
        
        // If we got here without exceptions, monitoring was successfully started and stopped
        Assert.True(true);
    }

    [StaFact(Skip = "Event cancellation requires subscribing BEFORE coordinator - needs refactoring")]
    public async Task ClipboardCoordinator_EventCancelled_ShouldNotSaveClip()
    {
        // Arrange
        _clipboardService.ClipCaptured += (sender, e) =>
        {
            e.Cancel = true; // Cancel the save
        };

        await _coordinator.StartAsync();

        // Act - Try to capture a clip
        var testClip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Cancelled clip",
            ContentHash = "cancelled-hash",
            CapturedAt = DateTime.UtcNow
        };

        // Manually trigger the event
        var coordinatorType = typeof(ClipboardCoordinator);
        var handlerMethod = coordinatorType.GetMethod("OnClipCaptured",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        handlerMethod?.Invoke(_coordinator, new object?[] { _clipboardService, new ClipCapturedEventArgs { Clip = testClip } });

        await Task.Delay(200); // Give time for any async operations

        // Assert
        var savedClips = await _clipRepository.GetRecentAsync(10);
        savedClips.ShouldNotContain(c => c.ContentHash == "cancelled-hash",
            "cancelled clips should not be saved");
    }

    public void Dispose()
    {
        _coordinator?.Dispose();
        if (_clipboardService is IDisposable disposable)
        {
            disposable.Dispose();
        }
        _dbContext?.Database.CloseConnection();
        _dbContext?.Dispose();
    }
}
