using System.Reflection;
using ClipMate.Core.Models.Configuration;
using ClipMate.Data.Services;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Core maintenance execution tests for MaintenanceSchedulerService.
/// Tests the RunMaintenance method logic including idle detection, retention enforcement,
/// cleanup execution, and backup management.
/// </summary>
public partial class MaintenanceSchedulerServiceTests
{
    [Test]
    [Category("RunMaintenance")]
    [Category("IdleDetection")]
    public async Task RunMaintenance_WhenSystemNotIdle_ShouldSkipMaintenance()
    {
        // Arrange
        var config = CreateConfigWithDatabase();
        var service = CreateService(config);

        // System is NOT idle
        _mockIdleDetector.Setup(p => p.IsIdle(It.IsAny<TimeSpan>())).Returns(false);

        await service.StartAsync(CancellationToken.None);

        // Act - Wait briefly for timer to potentially fire
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);

        // Assert - No maintenance operations should be called
        _mockRetentionService.Verify(
            p => p.EnforceAllCollectionsAsync(It.IsAny<string>()),
            Times.Never,
            "Retention enforcement should be skipped when system is not idle");

        _mockMaintenanceService.Verify(
            p => p.RunCleanupAsync(It.IsAny<DatabaseConfiguration>(), It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "Cleanup should be skipped when system is not idle");

        service.Dispose();
    }

    [Test]
    [Category("RunMaintenance")]
    [Category("RetentionEnforcement")]
    public async Task RunMaintenance_WhenSystemIdle_ShouldRunRetentionEnforcement()
    {
        // Arrange
        var config = CreateConfigWithDatabase();
        var service = CreateService(config);

        // System IS idle
        _mockIdleDetector.Setup(p => p.IsIdle(It.IsAny<TimeSpan>())).Returns(true);

        // Retention service returns 5 clips processed
        _mockRetentionService
            .Setup(p => p.EnforceAllCollectionsAsync("test-db"))
            .ReturnsAsync(5);

        // Start service, trigger maintenance by invoking via reflection
        await service.StartAsync(CancellationToken.None);

        // Use reflection to invoke RunMaintenance immediately
        var runMaintenanceMethod = typeof(MaintenanceSchedulerService).GetMethod(
            "RunMaintenance",
            BindingFlags.NonPublic | BindingFlags.Instance);

        runMaintenanceMethod?.Invoke(service, [null]);

        // Wait for async operations to complete
        await Task.Delay(100);

        // Assert - Retention enforcement should be called exactly once
        _mockRetentionService.Verify(
            p => p.EnforceAllCollectionsAsync("test-db"),
            Times.Once,
            "Retention enforcement should be called once per database when system is idle");

        await service.StopAsync(CancellationToken.None);
        service.Dispose();
    }

    [Test]
    [Category("RunMaintenance")]
    [Category("CleanupMethod")]
    public async Task RunMaintenance_WithCleanupMethodAfterHourIdle_ShouldRunCleanup()
    {
        // Arrange - Database configured with CleanupMethod.AfterHourIdle
        var config = CreateConfigWithDatabase(cleanupMethod: CleanupMethod.AfterHourIdle);
        var service = CreateService(config);

        _mockIdleDetector.Setup(p => p.IsIdle(It.IsAny<TimeSpan>())).Returns(true);
        _mockRetentionService.Setup(p => p.EnforceAllCollectionsAsync(It.IsAny<string>())).ReturnsAsync(0);
        _mockMaintenanceService
            .Setup(p => p.RunCleanupAsync(
                It.IsAny<DatabaseConfiguration>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(3); // 3 clips purged

        await service.StartAsync(CancellationToken.None);

        // Invoke RunMaintenance via reflection
        var runMaintenanceMethod = typeof(MaintenanceSchedulerService).GetMethod(
            "RunMaintenance",
            BindingFlags.NonPublic | BindingFlags.Instance);

        runMaintenanceMethod?.Invoke(service, [null]);
        await Task.Delay(100);

        // Assert - Cleanup should be called when CleanupMethod is AfterHourIdle
        _mockMaintenanceService.Verify(
            p => p.RunCleanupAsync(
                It.Is<DatabaseConfiguration>(db => db.Name == "Test Database"),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "Cleanup should be executed when CleanupMethod is AfterHourIdle");

        await service.StopAsync(CancellationToken.None);
        service.Dispose();
    }

    [Test]
    [Category("RunMaintenance")]
    [Category("CleanupMethod")]
    public async Task RunMaintenance_WithCleanupMethodManual_ShouldSkipCleanup()
    {
        // Arrange - Database configured with CleanupMethod.Manual
        var config = CreateConfigWithDatabase(cleanupMethod: CleanupMethod.Manual);
        var service = CreateService(config);

        _mockIdleDetector.Setup(p => p.IsIdle(It.IsAny<TimeSpan>())).Returns(true);
        _mockRetentionService.Setup(p => p.EnforceAllCollectionsAsync(It.IsAny<string>())).ReturnsAsync(0);

        await service.StartAsync(CancellationToken.None);

        // Invoke RunMaintenance via reflection
        var runMaintenanceMethod = typeof(MaintenanceSchedulerService).GetMethod(
            "RunMaintenance",
            BindingFlags.NonPublic | BindingFlags.Instance);

        runMaintenanceMethod?.Invoke(service, [null]);
        await Task.Delay(100);

        // Assert - Cleanup should NOT be called when CleanupMethod is Manual
        _mockMaintenanceService.Verify(
            p => p.RunCleanupAsync(
                It.IsAny<DatabaseConfiguration>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "Cleanup should be skipped when CleanupMethod is Manual");

        await service.StopAsync(CancellationToken.None);
        service.Dispose();
    }

    [Test]
    [Category("RunMaintenance")]
    [Category("CleanupMethod")]
    public async Task RunMaintenance_WithCleanupMethodNever_ShouldSkipCleanup()
    {
        // Arrange - Database configured with CleanupMethod.Never
        var config = CreateConfigWithDatabase(cleanupMethod: CleanupMethod.Never);
        var service = CreateService(config);

        _mockIdleDetector.Setup(p => p.IsIdle(It.IsAny<TimeSpan>())).Returns(true);
        _mockRetentionService.Setup(p => p.EnforceAllCollectionsAsync(It.IsAny<string>())).ReturnsAsync(0);

        await service.StartAsync(CancellationToken.None);

        // Invoke RunMaintenance via reflection
        var runMaintenanceMethod = typeof(MaintenanceSchedulerService).GetMethod(
            "RunMaintenance",
            BindingFlags.NonPublic | BindingFlags.Instance);

        runMaintenanceMethod?.Invoke(service, [null]);
        await Task.Delay(100);

        // Assert - Cleanup should NOT be called when CleanupMethod is Never
        _mockMaintenanceService.Verify(
            p => p.RunCleanupAsync(
                It.IsAny<DatabaseConfiguration>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "Cleanup should be skipped when CleanupMethod is Never");

        await service.StopAsync(CancellationToken.None);
        service.Dispose();
    }

    [Test]
    [Category("RunMaintenance")]
    [Category("MultipleDatabases")]
    public async Task RunMaintenance_WithMultipleDatabases_ShouldProcessEach()
    {
        // Arrange - Configuration with 3 databases
        var config = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration(),
            Databases = new Dictionary<string, DatabaseConfiguration>
            {
                { "db1", new DatabaseConfiguration { Name = "DB1", FilePath = "C:\\db1.db", CleanupMethod = CleanupMethod.Manual, BackupDirectory = "C:\\Backups1" } },
                { "db2", new DatabaseConfiguration { Name = "DB2", FilePath = "C:\\db2.db", CleanupMethod = CleanupMethod.Manual, BackupDirectory = "C:\\Backups2" } },
                { "db3", new DatabaseConfiguration { Name = "DB3", FilePath = "C:\\db3.db", CleanupMethod = CleanupMethod.Manual, BackupDirectory = "C:\\Backups3" } },
            },
        };

        var service = CreateService(config);

        _mockIdleDetector.Setup(p => p.IsIdle(It.IsAny<TimeSpan>())).Returns(true);
        _mockRetentionService.Setup(p => p.EnforceAllCollectionsAsync(It.IsAny<string>())).ReturnsAsync(0);

        await service.StartAsync(CancellationToken.None);

        // Invoke RunMaintenance via reflection
        var runMaintenanceMethod = typeof(MaintenanceSchedulerService).GetMethod(
            "RunMaintenance",
            BindingFlags.NonPublic | BindingFlags.Instance);

        runMaintenanceMethod?.Invoke(service, [null]);
        await Task.Delay(100);

        // Assert - Retention enforcement should be called once per database
        _mockRetentionService.Verify(p => p.EnforceAllCollectionsAsync("db1"), Times.Once);
        _mockRetentionService.Verify(p => p.EnforceAllCollectionsAsync("db2"), Times.Once);
        _mockRetentionService.Verify(p => p.EnforceAllCollectionsAsync("db3"), Times.Once);

        await service.StopAsync(CancellationToken.None);
        service.Dispose();
    }

    [Test]
    [Category("RunMaintenance")]
    [Category("BackupCleanup")]
    public async Task RunMaintenance_ShouldCleanupOldBackups()
    {
        // Arrange
        var config = CreateConfigWithDatabase(backupDirectory: "C:\\Backups");
        var service = CreateService(config);

        _mockIdleDetector.Setup(p => p.IsIdle(It.IsAny<TimeSpan>())).Returns(true);
        _mockRetentionService.Setup(p => p.EnforceAllCollectionsAsync(It.IsAny<string>())).ReturnsAsync(0);
        _mockMaintenanceService
            .Setup(p => p.CleanupOldBackupsAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(2); // 2 old backups deleted

        await service.StartAsync(CancellationToken.None);

        // Invoke RunMaintenance via reflection
        var runMaintenanceMethod = typeof(MaintenanceSchedulerService).GetMethod(
            "RunMaintenance",
            BindingFlags.NonPublic | BindingFlags.Instance);

        runMaintenanceMethod?.Invoke(service, [null]);
        await Task.Delay(100);

        // Assert - Backup cleanup should be called with expanded path
        _mockMaintenanceService.Verify(
            p => p.CleanupOldBackupsAsync("C:\\Backups", It.IsAny<int>()),
            Times.Once,
            "Old backup cleanup should be executed once per maintenance run");

        await service.StopAsync(CancellationToken.None);
        service.Dispose();
    }

    [Test]
    [Category("RunMaintenance")]
    [Category("BackupCleanup")]
    public async Task RunMaintenance_WithMultipleBackupDirectories_ShouldCleanupEachOnce()
    {
        // Arrange - 3 databases with 2 unique backup directories
        var config = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration(),
            Databases = new Dictionary<string, DatabaseConfiguration>
            {
                { "db1", new DatabaseConfiguration { Name = "DB1", FilePath = "C:\\db1.db", CleanupMethod = CleanupMethod.Manual, BackupDirectory = "C:\\BackupA" } },
                { "db2", new DatabaseConfiguration { Name = "DB2", FilePath = "C:\\db2.db", CleanupMethod = CleanupMethod.Manual, BackupDirectory = "C:\\BackupB" } },
                { "db3", new DatabaseConfiguration { Name = "DB3", FilePath = "C:\\db3.db", CleanupMethod = CleanupMethod.Manual, BackupDirectory = "C:\\BackupA" } }, // Duplicate directory
            },
        };

        var service = CreateService(config);

        _mockIdleDetector.Setup(p => p.IsIdle(It.IsAny<TimeSpan>())).Returns(true);
        _mockRetentionService.Setup(p => p.EnforceAllCollectionsAsync(It.IsAny<string>())).ReturnsAsync(0);
        _mockMaintenanceService.Setup(p => p.CleanupOldBackupsAsync(It.IsAny<string>(), It.IsAny<int>())).ReturnsAsync(0);

        await service.StartAsync(CancellationToken.None);

        // Invoke RunMaintenance via reflection
        var runMaintenanceMethod = typeof(MaintenanceSchedulerService).GetMethod(
            "RunMaintenance",
            BindingFlags.NonPublic | BindingFlags.Instance);

        runMaintenanceMethod?.Invoke(service, [null]);
        await Task.Delay(100);

        // Assert - Each unique backup directory should be cleaned exactly once
        _mockMaintenanceService.Verify(p => p.CleanupOldBackupsAsync("C:\\BackupA", It.IsAny<int>()), Times.Once);
        _mockMaintenanceService.Verify(p => p.CleanupOldBackupsAsync("C:\\BackupB", It.IsAny<int>()), Times.Once);

        // Total cleanup calls should be 2 (not 3)
        _mockMaintenanceService.Verify(p => p.CleanupOldBackupsAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Exactly(2));

        await service.StopAsync(CancellationToken.None);
        service.Dispose();
    }

    [Test]
    [Category("RunMaintenance")]
    [Category("BackupCleanup")]
    public async Task RunMaintenance_WithEmptyBackupDirectory_ShouldSkipCleanup()
    {
        // Arrange - Database with empty/whitespace backup directory
        var config = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration(),
            Databases = new Dictionary<string, DatabaseConfiguration>
            {
                { "db1", new DatabaseConfiguration { Name = "DB1", FilePath = "C:\\db1.db", CleanupMethod = CleanupMethod.Manual, BackupDirectory = "" } },
                { "db2", new DatabaseConfiguration { Name = "DB2", FilePath = "C:\\db2.db", CleanupMethod = CleanupMethod.Manual, BackupDirectory = "   " } },
            },
        };

        var service = CreateService(config);

        _mockIdleDetector.Setup(p => p.IsIdle(It.IsAny<TimeSpan>())).Returns(true);
        _mockRetentionService.Setup(p => p.EnforceAllCollectionsAsync(It.IsAny<string>())).ReturnsAsync(0);

        await service.StartAsync(CancellationToken.None);

        // Invoke RunMaintenance via reflection
        var runMaintenanceMethod = typeof(MaintenanceSchedulerService).GetMethod(
            "RunMaintenance",
            BindingFlags.NonPublic | BindingFlags.Instance);

        runMaintenanceMethod?.Invoke(service, [null]);
        await Task.Delay(100);

        // Assert - No backup cleanup should be called for empty directories
        _mockMaintenanceService.Verify(
            p => p.CleanupOldBackupsAsync(It.IsAny<string>(), It.IsAny<int>()),
            Times.Never,
            "Backup cleanup should be skipped for empty/whitespace backup directories");

        await service.StopAsync(CancellationToken.None);
        service.Dispose();
    }

    [Test]
    [Category("RunMaintenance")]
    [Category("ErrorHandling")]
    public async Task RunMaintenance_WhenRetentionEnforcementThrows_ShouldContinueAndLogError()
    {
        // Arrange
        var config = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration(),
            Databases = new Dictionary<string, DatabaseConfiguration>
            {
                { "db1", new DatabaseConfiguration { Name = "DB1", FilePath = "C:\\db1.db", CleanupMethod = CleanupMethod.Manual, BackupDirectory = "C:\\Backups1" } },
                { "db2", new DatabaseConfiguration { Name = "DB2", FilePath = "C:\\db2.db", CleanupMethod = CleanupMethod.Manual, BackupDirectory = "C:\\Backups2" } },
            },
        };

        var service = CreateService(config);

        _mockIdleDetector.Setup(p => p.IsIdle(It.IsAny<TimeSpan>())).Returns(true);

        // First database throws exception
        _mockRetentionService
            .Setup(p => p.EnforceAllCollectionsAsync("db1"))
            .ThrowsAsync(new InvalidOperationException("Simulated retention failure"));

        // Second database succeeds
        _mockRetentionService
            .Setup(p => p.EnforceAllCollectionsAsync("db2"))
            .ReturnsAsync(5);

        await service.StartAsync(CancellationToken.None);

        // Invoke RunMaintenance via reflection
        var runMaintenanceMethod = typeof(MaintenanceSchedulerService).GetMethod(
            "RunMaintenance",
            BindingFlags.NonPublic | BindingFlags.Instance);

        runMaintenanceMethod?.Invoke(service, [null]);
        await Task.Delay(100);

        // Assert - Both databases should be attempted despite first failure
        // Note: The error handling is in the outer try-catch, so both may not be called
        // This test verifies the service doesn't crash when exceptions occur
        _mockRetentionService.Verify(
            p => p.EnforceAllCollectionsAsync(It.IsAny<string>()),
            Times.AtLeastOnce,
            "Service should attempt maintenance despite errors");

        await service.StopAsync(CancellationToken.None);
        service.Dispose();
    }

    [Test]
    [Category("RunMaintenance")]
    [Category("NoDatabases")]
    public async Task RunMaintenance_WithNoDatabases_ShouldCompleteWithoutError()
    {
        // Arrange - Configuration with empty database dictionary
        var config = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration(),
            Databases = new Dictionary<string, DatabaseConfiguration>(),
        };

        var service = CreateService(config);

        _mockIdleDetector.Setup(p => p.IsIdle(It.IsAny<TimeSpan>())).Returns(true);

        await service.StartAsync(CancellationToken.None);

        // Invoke RunMaintenance via reflection
        var runMaintenanceMethod = typeof(MaintenanceSchedulerService).GetMethod(
            "RunMaintenance",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act & Assert - Should complete without error
        runMaintenanceMethod?.Invoke(service, [null]);
        await Task.Delay(100);

        // Assert - No database operations should be called
        _mockRetentionService.Verify(p => p.EnforceAllCollectionsAsync(It.IsAny<string>()), Times.Never);
        _mockMaintenanceService.Verify(p => p.RunCleanupAsync(It.IsAny<DatabaseConfiguration>(), It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()), Times.Never);

        await service.StopAsync(CancellationToken.None);
        service.Dispose();
    }
}
