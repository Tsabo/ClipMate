using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using ClipMate.Platform;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Base test class for MaintenanceSchedulerService tests.
/// Contains shared setup, cleanup, and helper methods.
/// </summary>
[Category("MaintenanceSchedulerService")]
[Category("Unit")]
public partial class MaintenanceSchedulerServiceTests
{
    private Mock<IConfigurationService> _mockConfigService = null!;
    private Mock<IWin32IdleDetector> _mockIdleDetector = null!;
    private Mock<ILogger<MaintenanceSchedulerService>> _mockLogger = null!;
    private Mock<IDatabaseMaintenanceService> _mockMaintenanceService = null!;
    private Mock<IRetentionEnforcementService> _mockRetentionService = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockConfigService = new Mock<IConfigurationService>();
        _mockMaintenanceService = new Mock<IDatabaseMaintenanceService>();
        _mockRetentionService = new Mock<IRetentionEnforcementService>();
        _mockIdleDetector = new Mock<IWin32IdleDetector>();
        _mockLogger = new Mock<ILogger<MaintenanceSchedulerService>>();
    }

    private MaintenanceSchedulerService CreateService(ClipMateConfiguration? config = null)
    {
        // Setup default configuration with no databases if not provided
        config ??= new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration(),
            Databases = new Dictionary<string, DatabaseConfiguration>(),
        };

        _mockConfigService.Setup(p => p.Configuration).Returns(config);

        return new MaintenanceSchedulerService(
            _mockRetentionService.Object,
            _mockMaintenanceService.Object,
            _mockConfigService.Object,
            _mockIdleDetector.Object,
            _mockLogger.Object);
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
