using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Data.Services;
using ClipMate.Platform;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Base test class for MaintenanceSchedulerService tests.
/// Contains shared setup, cleanup, and helper methods.
/// </summary>
[Category("MaintenanceSchedulerService")]
[Category("Unit")]
public partial class MaintenanceSchedulerServiceTests
{
    private IConfigurationService _mockConfigService = null!;
    private IWin32IdleDetector _mockIdleDetector = null!;
    private ILogger<MaintenanceSchedulerService> _mockLogger = null!;
    private IDatabaseMaintenanceService _mockMaintenanceService = null!;
    private IRetentionEnforcementService _mockRetentionService = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockConfigService = A.Fake<IConfigurationService>();
        _mockMaintenanceService = A.Fake<IDatabaseMaintenanceService>();
        _mockRetentionService = A.Fake<IRetentionEnforcementService>();
        _mockIdleDetector = A.Fake<IWin32IdleDetector>();
        _mockLogger = A.Fake<ILogger<MaintenanceSchedulerService>>();
    }

    private MaintenanceSchedulerService CreateService(ClipMateConfiguration? config = null)
    {
        // Setup default configuration with no databases if not provided
        config ??= new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration(),
            Databases = new Dictionary<string, DatabaseConfiguration>(),
        };

        A.CallTo(() => _mockConfigService.Configuration).Returns(config);

        return new MaintenanceSchedulerService(
            _mockRetentionService,
            _mockMaintenanceService,
            _mockConfigService,
            _mockIdleDetector,
            _mockLogger);
    }

    private static ClipMateConfiguration CreateConfigWithDatabase(string databaseKey = "test-db",
        CleanupMethod cleanupMethod = CleanupMethod.Manual,
        string backupDirectory = "C:\\Backups") =>
        new()
        {
            Preferences = new PreferencesConfiguration(),
            Databases = new Dictionary<string, DatabaseConfiguration>
            {
                {
                    databaseKey, new DatabaseConfiguration
                    {
                        Name = "Test Database",
                        FilePath = "C:\\test.db",
                        CleanupMethod = cleanupMethod,
                        BackupDirectory = backupDirectory,
                        AllowBackup = true,
                        PurgeDays = 7,
                    }
                },
            },
        };
}
