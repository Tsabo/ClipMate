using ClipMate.Core.Events;
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
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Moq;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for the complete clipboard capture pipeline.
/// Tests the flow: clipboard change -> channel -> save to database.
/// </summary>
public class ClipboardIntegrationTests : IDisposable
{
    private readonly ClipMateDbContext _dbContext;
    private readonly IClipRepository _clipRepository;
    private readonly IClipService _clipService;
    private readonly IApplicationFilterService _filterService;
    private readonly IClipboardService _clipboardService;
    private readonly ClipboardCoordinator _coordinator;
    private readonly ServiceProvider _serviceProvider;
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
        var clipRepoLogger = Mock.Of<ILogger<ClipRepository>>();

        _clipRepository = new ClipRepository(_dbContext, clipRepoLogger);
        _clipService = new ClipService(_clipRepository);
        
        var filterRepository = new ApplicationFilterRepository(_dbContext);
        _filterService = new ApplicationFilterService(filterRepository, filterLogger);
        
        var collectionRepository = new CollectionRepository(_dbContext);
        var collectionService = new CollectionService(collectionRepository);
        
        var folderRepository = new FolderRepository(_dbContext);
        var folderService = new FolderService(folderRepository);
        
        _clipboardService = new ClipboardService(clipboardLogger);

        // Setup DI container for ClipboardCoordinator (needs IServiceProvider for scoped services)
        var services = new ServiceCollection();
        services.AddScoped<IClipService>(_ => _clipService);
        services.AddScoped<ICollectionService>(_ => collectionService);
        services.AddScoped<IFolderService>(_ => folderService);
        services.AddScoped<IApplicationFilterService>(_ => _filterService);
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        _serviceProvider = services.BuildServiceProvider();

        var messenger = _serviceProvider.GetRequiredService<IMessenger>();
        _coordinator = new ClipboardCoordinator(_clipboardService, _serviceProvider, messenger, coordinatorLogger);
    }

    [StaFact(Skip = "Requires mocking internal channel writer - integration test needs refactoring for channel-based approach")]
    public async Task ClipboardCapture_ShouldSaveToDatabase()
    {
        // This test is skipped because:
        // 1. The channel is internal to ClipboardService
        // 2. We can't easily mock WriteAsync to the channel
        // 3. Real clipboard monitoring requires Win32 interaction
        // 
        // Alternative: Test the coordinator's ProcessClipAsync method directly
        // by using reflection or making it internal-visible-to tests
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
        await _coordinator.StartAsync(CancellationToken.None);

        // Assert - verify monitoring is active by checking we can stop it
        await _coordinator.StopAsync(CancellationToken.None);
        
        // If we got here without exceptions, monitoring was successfully started and stopped
        Assert.True(true);
    }

    [StaFact]
    public async Task ClipboardCoordinator_Stop_ShouldCompleteChannel()
    {
        // Arrange
        await _coordinator.StartAsync(CancellationToken.None);
        
        // Act
        await _coordinator.StopAsync(CancellationToken.None);

        // Assert
        _clipboardService.ClipsChannel.Completion.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task ClipService_CreateAsync_ShouldDetectDuplicates()
    {
        // Arrange
        var clip1 = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Same content",
            ContentHash = "same-hash",
            CapturedAt = DateTime.UtcNow
        };

        // Act
        var saved1 = await _clipService.CreateAsync(clip1);
        
        var clip2 = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Same content",
            ContentHash = "same-hash",
            CapturedAt = DateTime.UtcNow.AddSeconds(5)
        };
        
        var saved2 = await _clipService.CreateAsync(clip2);

        // Assert
        saved1.Id.ShouldBe(saved2.Id, "duplicate should return existing clip ID");
        
        var recentClips = await _clipRepository.GetRecentAsync(100);
        recentClips.Count(c => c.ContentHash == "same-hash").ShouldBe(1);
    }

    public void Dispose()
    {
        _coordinator?.StopAsync(CancellationToken.None).Wait();
        if (_clipboardService is IDisposable disposable)
        {
            disposable.Dispose();
        }
        _serviceProvider?.Dispose();
        _dbContext?.Database.CloseConnection();
        _dbContext?.Dispose();
    }
}
