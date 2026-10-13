using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using ClipMate.Core.Services;
using ClipMate.Data;
using ClipMate.Data.Services;
using ClipMate.Platform;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

[Category("ApplicationFilterService")]
[Category("Unit")]
public partial class ApplicationFilterServiceTests
{
    private const string _testDatabaseKey = "test-db";
    private ICollectionService _mockCollectionService = null!;
    private IDatabaseContextFactory _mockContextFactory = null!;
    private IApplicationFilterRepository _mockFilterRepository = null!;
    private ILogger<ApplicationFilterService> _mockLogger = null!;
    private ISoundService _mockSoundService = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockLogger = A.Fake<ILogger<ApplicationFilterService>>();
        _mockContextFactory = A.Fake<IDatabaseContextFactory>();
        _mockCollectionService = A.Fake<ICollectionService>();
        _mockSoundService = A.Fake<ISoundService>();
        _mockFilterRepository = A.Fake<IApplicationFilterRepository>();

        // Default: return test database key
        A.CallTo(() => _mockCollectionService.GetActiveDatabaseKey()).Returns(_testDatabaseKey);
        A.CallTo(() => _mockContextFactory.GetApplicationFilterRepository(_testDatabaseKey))
            .Returns(_mockFilterRepository);
    }

    private ApplicationFilterService CreateService() => new(
        _mockContextFactory,
        _mockCollectionService,
        _mockSoundService,
        _mockLogger);

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
