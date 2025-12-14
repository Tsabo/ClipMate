using ClipMate.Core.Models.Configuration;

namespace ClipMate.Tests.Integration.Configuration;

/// <summary>
/// Integration tests for PreferencesConfiguration database settings.
/// </summary>
public class PreferencesConfigurationDatabaseTests
{
    [Test]
    public async Task BackupIntervalDays_ShouldDefaultTo7()
    {
        // Arrange
        var preferences = new PreferencesConfiguration();

        // Assert
        await Assert.That(preferences.BackupIntervalDays).IsEqualTo(7);
    }

    [Test]
    public async Task AutoConfirmBackupSeconds_ShouldDefaultTo0()
    {
        // Arrange
        var preferences = new PreferencesConfiguration();

        // Assert
        await Assert.That(preferences.AutoConfirmBackupSeconds).IsEqualTo(0);
    }

    [Test]
    public async Task BackupIntervalDays_ShouldBeSettable()
    {
        // Arrange
        var preferences = new PreferencesConfiguration();

        // Act
        preferences.BackupIntervalDays = 14;

        // Assert
        await Assert.That(preferences.BackupIntervalDays).IsEqualTo(14);
    }

    [Test]
    public async Task AutoConfirmBackupSeconds_ShouldBeSettable()
    {
        // Arrange
        var preferences = new PreferencesConfiguration();

        // Act
        preferences.AutoConfirmBackupSeconds = 30;

        // Assert
        await Assert.That(preferences.AutoConfirmBackupSeconds).IsEqualTo(30);
    }

    [Test]
    public async Task BackupIntervalDays_ShouldAcceptZero_ForDisabled()
    {
        // Arrange
        var preferences = new PreferencesConfiguration();

        // Act
        preferences.BackupIntervalDays = 0;

        // Assert
        await Assert.That(preferences.BackupIntervalDays).IsEqualTo(0);
    }

    [Test]
    public async Task BackupIntervalDays_ShouldAccept9999_ForNever()
    {
        // Arrange
        var preferences = new PreferencesConfiguration();

        // Act
        preferences.BackupIntervalDays = 9999;

        // Assert
        await Assert.That(preferences.BackupIntervalDays).IsEqualTo(9999);
    }

    [Test]
    public async Task AutoConfirmBackupSeconds_ShouldAcceptValidRange()
    {
        // Arrange
        var preferences = new PreferencesConfiguration();

        // Act & Assert - Valid values
        preferences.AutoConfirmBackupSeconds = 0;
        await Assert.That(preferences.AutoConfirmBackupSeconds).IsEqualTo(0);

        preferences.AutoConfirmBackupSeconds = 10;
        await Assert.That(preferences.AutoConfirmBackupSeconds).IsEqualTo(10);

        preferences.AutoConfirmBackupSeconds = 60;
        await Assert.That(preferences.AutoConfirmBackupSeconds).IsEqualTo(60);
    }
}
