using ClipMate.Core.Models.Configuration;

namespace ClipMate.Tests.Unit.Models;

/// <summary>
/// Unit tests for DatabaseConfiguration model.
/// </summary>
public class DatabaseConfigurationTests
{
    [Test]
    public async Task Constructor_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var config = new DatabaseConfiguration();

        // Assert
        await Assert.That(config.Name).IsEqualTo("My Clips");
        await Assert.That(config.FilePath).IsEqualTo(string.Empty);
        await Assert.That(config.AutoLoad).IsTrue();
        await Assert.That(config.AllowBackup).IsTrue();
        await Assert.That(config.ReadOnly).IsFalse();
        await Assert.That(config.CleanupMethod).IsEqualTo(CleanupMethod.AfterHourIdle);
        await Assert.That(config.PurgeDays).IsEqualTo(7);
        await Assert.That(config.UserName).IsEqualTo(Environment.UserName);
        await Assert.That(config.IsRemote).IsFalse();
        await Assert.That(config.MultiUser).IsFalse();
        await Assert.That(config.TempFileLocation).IsEqualTo(TempFileLocation.DatabaseDirectory);
        await Assert.That(config.UseModificationTimeStamp).IsTrue();
        await Assert.That(config.BackupDirectory).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Properties_ShouldBeSettable()
    {
        // Arrange
        var config = new DatabaseConfiguration();
        var testDate = DateTime.Now;

        // Act
        config.Name = "Test Database";
        config.FilePath = @"C:\TestPath\testdb.db";
        config.AutoLoad = false;
        config.AllowBackup = false;
        config.ReadOnly = true;
        config.CleanupMethod = CleanupMethod.AfterHourIdle;
        config.PurgeDays = 30;
        config.UserName = "TestUser";
        config.IsRemote = true;
        config.MultiUser = true;
        config.RemoteHost = "server.example.com";
        config.RemoteDatabase = "RemoteDB";
        config.IsCommandLineDatabase = true;
        config.UseModificationTimeStamp = false;
        config.LastBackupDate = testDate;
        config.BackupDirectory = @"C:\Backups";
        config.TempFileLocation = TempFileLocation.SystemTmp;
        config.SetOfflineDailyAt = TimeSpan.FromHours(2);
        config.RemoteUserId = "admin";
        config.RemotePassword = "secret";

        // Assert
        await Assert.That(config.Name).IsEqualTo("Test Database");
        await Assert.That(config.FilePath).IsEqualTo(@"C:\TestPath\testdb.db");
        await Assert.That(config.AutoLoad).IsFalse();
        await Assert.That(config.AllowBackup).IsFalse();
        await Assert.That(config.ReadOnly).IsTrue();
        await Assert.That(config.CleanupMethod).IsEqualTo(CleanupMethod.AfterHourIdle);
        await Assert.That(config.PurgeDays).IsEqualTo(30);
        await Assert.That(config.UserName).IsEqualTo("TestUser");
        await Assert.That(config.IsRemote).IsTrue();
        await Assert.That(config.MultiUser).IsTrue();
        await Assert.That(config.RemoteHost).IsEqualTo("server.example.com");
        await Assert.That(config.RemoteDatabase).IsEqualTo("RemoteDB");
        await Assert.That(config.IsCommandLineDatabase).IsTrue();
        await Assert.That(config.UseModificationTimeStamp).IsFalse();
        await Assert.That(config.LastBackupDate).IsEqualTo(testDate);
        await Assert.That(config.BackupDirectory).IsEqualTo(@"C:\Backups");
        await Assert.That(config.TempFileLocation).IsEqualTo(TempFileLocation.SystemTmp);
        await Assert.That(config.SetOfflineDailyAt).IsEqualTo(TimeSpan.FromHours(2));
        await Assert.That(config.RemoteUserId).IsEqualTo("admin");
        await Assert.That(config.RemotePassword).IsEqualTo("secret");
    }

    [Test]
    public async Task LastBackupDate_ShouldAcceptNull()
    {
        // Arrange
        var config = new DatabaseConfiguration();

        // Act
        config.LastBackupDate = null;

        // Assert
        await Assert.That(config.LastBackupDate).IsNull();
    }

    [Test]
    public async Task SetOfflineDailyAt_ShouldAcceptNull()
    {
        // Arrange
        var config = new DatabaseConfiguration();

        // Act
        config.SetOfflineDailyAt = null;

        // Assert
        await Assert.That(config.SetOfflineDailyAt).IsNull();
    }
}
