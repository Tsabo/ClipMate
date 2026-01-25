using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using ClipMate.Platform;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

[Category("ApplicationFilterService")]
[Category("Unit")]
public partial class ApplicationFilterServiceTests
{
    private const string _testDatabaseKey = "test-db";
    private Mock<ICollectionService> _mockCollectionService = null!;
    private Mock<IDatabaseContextFactory> _mockContextFactory = null!;
    private Mock<IApplicationFilterRepository> _mockFilterRepository = null!;
    private Mock<ILogger<ApplicationFilterService>> _mockLogger = null!;
    private Mock<ISoundService> _mockSoundService = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<ApplicationFilterService>>();
        _mockContextFactory = new Mock<IDatabaseContextFactory>();
        _mockCollectionService = new Mock<ICollectionService>();
        _mockSoundService = new Mock<ISoundService>();
        _mockFilterRepository = new Mock<IApplicationFilterRepository>();

        // Default: return test database key
        _mockCollectionService.Setup(p => p.GetActiveDatabaseKey()).Returns(_testDatabaseKey);
        _mockContextFactory.Setup(p => p.GetApplicationFilterRepository(_testDatabaseKey))
            .Returns(_mockFilterRepository.Object);
    }

    private ApplicationFilterService CreateService() => new(
        _mockContextFactory.Object,
        _mockCollectionService.Object,
        _mockSoundService.Object,
        _mockLogger.Object);

    private static ApplicationFilter CreateTestFilter(string name,
        string? processName = null,
        string? windowTitlePattern = null,
        bool isEnabled = true,
        Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = name,
        ProcessName = processName,
        WindowTitlePattern = windowTitlePattern,
        IsEnabled = isEnabled,
        CreatedAt = DateTime.UtcNow,
    };
}
