using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Base test class for DatabaseMaintenanceService tests.
/// Contains shared setup, mocks, and helper methods.
/// </summary>
[Category("DatabaseMaintenanceService")]
[Category("Unit")]
public partial class DatabaseMaintenanceServiceTests
{
    protected const string TestDatabasePath = "C:\\TestData\\clipmate.db";
    protected const string TestBackupDirectory = "C:\\TestBackups";
    protected Mock<ILogger<DatabaseMaintenanceService>> MockLogger { get; private set; } = null!;

    [Before(Test)]
    public void Setup()
    {
        MockLogger = new Mock<ILogger<DatabaseMaintenanceService>>();
    }

    protected IDatabaseMaintenanceService CreateService() =>
        new DatabaseMaintenanceService(MockLogger.Object);

    protected static DatabaseConfiguration CreateTestDatabaseConfig(string name = "TestDB",
        string? filePath = null,
        int purgeDays = 30,
        bool allowBackup = true,
        DateTime? lastBackupDate = null) =>
        new()
        {
            Name = name,
            FilePath = filePath ?? TestDatabasePath,
            PurgeDays = purgeDays,
            AllowBackup = allowBackup,
            LastBackupDate = lastBackupDate,
        };

    protected static IProgress<string> CreateMockProgress() => Mock.Of<IProgress<string>>();
}
