using ClipMate.Core.Models;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Services;
using ClipMate.Platform;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

[Category("ClipAppendService")]
[Category("Unit")]
public partial class ClipAppendServiceTests
{
    private const string _testDatabaseKey = "test-db";
    private IClipService _mockClipService = null!;
    private ICollectionService _mockCollectionService = null!;
    private IDatabaseContextFactory _mockContextFactory = null!;
    private ILogger<ClipAppendService> _mockLogger = null!;
    private ISoundService _mockSoundService = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockLogger = A.Fake<ILogger<ClipAppendService>>();
        _mockContextFactory = A.Fake<IDatabaseContextFactory>();
        _mockCollectionService = A.Fake<ICollectionService>();
        _mockClipService = A.Fake<IClipService>();
        _mockSoundService = A.Fake<ISoundService>();

        // Default: return test database key
        A.CallTo(() => _mockCollectionService.GetActiveDatabaseKey()).Returns(_testDatabaseKey);

        // New clips go through the real clip store, onto the repository each test sets up
        var clipStore = new ClipStore(_mockContextFactory, A.Fake<ILogger<ClipStore>>());
        A.CallTo(() => _mockClipService.CreateAsync(A<string>._, A<Clip>._, A<CancellationToken>._))
            .ReturnsLazily(async (string databaseKey, Clip clip, CancellationToken cancellationToken) =>
                ((ClipAdded)await clipStore.AddAsync(databaseKey, clip, clip.CollectionId, clip.FolderId, false, cancellationToken)).Clip);
    }

    private ClipAppendService CreateService() => new(
        _mockContextFactory,
        _mockCollectionService,
        _mockClipService,
        _mockSoundService,
        _mockLogger);

    private static Clip CreateTestClip(string textContent, Guid? id = null, Guid? collectionId = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        TextContent = textContent,
        Type = ClipType.Text,
        CapturedAt = DateTimeOffset.Now,
        CollectionId = collectionId ?? Guid.NewGuid(),
        Title = "Test Clip",
        Creator = "TestUser",
    };
}
