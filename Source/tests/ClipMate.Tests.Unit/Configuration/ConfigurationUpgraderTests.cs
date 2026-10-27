using ClipMate.Core.Models.Configuration;

namespace ClipMate.Tests.Unit.Configuration;

public class ConfigurationUpgraderTests
{
    [Test]
    public async Task Upgrade_Version1_AppendsCtfmonToTheBadTargets()
    {
        // Arrange
        var configuration = new ClipMateConfiguration
        {
            Version = 1,
            Preferences = new PreferencesConfiguration { QuickPasteBadTargets = ["CLIPMATE:", "EXPLORER:SHELL_TRAYWND"] },
        };

        // Act
        var changed = ConfigurationUpgrader.Upgrade(configuration);

        // Assert
        await Assert.That(changed).IsTrue();
        await Assert.That(configuration.Version).IsEqualTo(ConfigurationUpgrader.CurrentVersion);
        await Assert.That(configuration.Preferences.QuickPasteBadTargets)
            .IsEquivalentTo(new[] { "CLIPMATE:", "EXPLORER:SHELL_TRAYWND", "CTFMON:" });
    }

    [Test]
    public async Task Upgrade_Version1_WithCtfmonAlreadyListed_DoesNotAddItTwice()
    {
        // Arrange
        var configuration = new ClipMateConfiguration
        {
            Version = 1,
            Preferences = new PreferencesConfiguration { QuickPasteBadTargets = ["ctfmon:"] },
        };

        // Act
        ConfigurationUpgrader.Upgrade(configuration);

        // Assert
        await Assert.That(configuration.Preferences.QuickPasteBadTargets).IsEquivalentTo(new[] { "ctfmon:" });
    }

    [Test]
    public async Task Upgrade_Version2_MovesTheSavedCaptureDelayToTheDelayOnClipboardUpdate()
    {
        // Arrange
        var configuration = new ClipMateConfiguration
        {
            Version = 2,
            Preferences = new PreferencesConfiguration { LegacyCaptureDelayMs = 600 },
        };

        // Act
        var changed = ConfigurationUpgrader.Upgrade(configuration);

        // Assert
        await Assert.That(changed).IsTrue();
        await Assert.That(configuration.Preferences.DelayOnClipboardUpdateMs).IsEqualTo(600);
        await Assert.That(configuration.Preferences.LegacyCaptureDelayMs).IsNull();
    }

    [Test]
    public async Task Upgrade_Version2_WithoutASavedCaptureDelay_KeepsTheDefaultDelay()
    {
        // Arrange
        var configuration = new ClipMateConfiguration { Version = 2 };

        // Act
        ConfigurationUpgrader.Upgrade(configuration);

        // Assert
        await Assert.That(configuration.Preferences.DelayOnClipboardUpdateMs).IsEqualTo(250);
        await Assert.That(configuration.Preferences.SettleTimeMs).IsEqualTo(100);
    }

    [Test]
    public async Task Upgrade_Version3_ClearsTheOldToggleAutoCaptureDefault()
    {
        // Arrange: Ctrl+Alt+A was the default, and is Append's shortcut in ClipMate's windows
        var configuration = new ClipMateConfiguration
        {
            Version = 3,
            Hotkeys = new HotkeyConfiguration { AutoCapture = "Ctrl+Alt+A" },
        };

        // Act
        var changed = ConfigurationUpgrader.Upgrade(configuration);

        // Assert
        await Assert.That(changed).IsTrue();
        await Assert.That(configuration.Hotkeys.AutoCapture).IsEmpty();
    }

    [Test]
    public async Task Upgrade_Version3_KeepsAToggleAutoCaptureHotkeyTheUserChose()
    {
        // Arrange
        var configuration = new ClipMateConfiguration
        {
            Version = 3,
            Hotkeys = new HotkeyConfiguration { AutoCapture = "Ctrl+Alt+Shift+F8" },
        };

        // Act
        ConfigurationUpgrader.Upgrade(configuration);

        // Assert
        await Assert.That(configuration.Hotkeys.AutoCapture).IsEqualTo("Ctrl+Alt+Shift+F8");
    }

    [Test]
    public async Task NewSettings_HaveNoToggleAutoCaptureHotkey()
    {
        // Act
        var configuration = new ClipMateConfiguration();

        // Assert: ClipMate 7's Win+Shift+C is held by Windows, and Ctrl+Alt+A is Append's shortcut
        await Assert.That(configuration.Hotkeys.AutoCapture).IsEmpty();
    }

    [Test]
    public async Task Upgrade_CurrentVersion_LeavesADeletedCtfmonDeleted()
    {
        // Arrange: the user removed CTFMON: after the upgrade had run
        var configuration = new ClipMateConfiguration
        {
            Version = ConfigurationUpgrader.CurrentVersion,
            Preferences = new PreferencesConfiguration { QuickPasteBadTargets = ["CLIPMATE:"] },
        };

        // Act
        var changed = ConfigurationUpgrader.Upgrade(configuration);

        // Assert
        await Assert.That(changed).IsFalse();
        await Assert.That(configuration.Preferences.QuickPasteBadTargets).IsEquivalentTo(new[] { "CLIPMATE:" });
    }
}
