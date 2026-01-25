using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using ClipMate.Platform;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

[Category("ClipAppendService")]
[Category("Unit")]
public partial class ClipAppendServiceTests
{
    private const string _testDatabaseKey = "test-db";
    private Mock<ICollectionService> _mockCollectionService = null!;
    private Mock<IDatabaseContextFactory> _mockContextFactory = null!;
    private Mock<ILogger<ClipAppendService>> _mockLogger = null!;
    private Mock<ISoundService> _mockSoundService = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<ClipAppendService>>();
        _mockContextFactory = new Mock<IDatabaseContextFactory>();
        _mockCollectionService = new Mock<ICollectionService>();
        _mockSoundService = new Mock<ISoundService>();

        // Default: return test database key
        _mockCollectionService.Setup(p => p.GetActiveDatabaseKey()).Returns(_testDatabaseKey);
    }

    private ClipAppendService CreateService() => new(
        _mockContextFactory.Object,
        _mockCollectionService.Object,
        _mockSoundService.Object,
        _mockLogger.Object);

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
