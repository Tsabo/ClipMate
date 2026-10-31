using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data.Services;
using Microsoft.Extensions.Logging;
using FakeItEasy;

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
    protected ILogger<DatabaseMaintenanceService> MockLogger { get; private set; } = null!;

    [Before(Test)]
    public void Setup()
    {
        MockLogger = A.Fake<ILogger<DatabaseMaintenanceService>>();
    }

    protected IDatabaseMaintenanceService CreateService() =>
        new DatabaseMaintenanceService(MockLogger);

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

    protected static IProgress<string> CreateMockProgress() => A.Fake<IProgress<string>>();
}
