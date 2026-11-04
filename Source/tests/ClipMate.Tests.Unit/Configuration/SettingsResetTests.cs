using ClipMate.Core.Helpers;
using ClipMate.Core.Models.Configuration;
using Tomlyn;

namespace ClipMate.Tests.Unit.Configuration;

public class SettingsResetTests
{
    /// <summary>
    /// Settings with something other than the default in every section.
    /// </summary>
    private static ClipMateConfiguration CreateCustomizedConfiguration() =>
        new()
        {
            Version = ConfigurationUpgrader.CurrentVersion,
            DefaultDatabase = "work",
            Databases = new Dictionary<string, DatabaseConfiguration>
            {
                ["work"] = new() { Name = "Work", FilePath = @"D:\Clips\work.db", PurgeDays = 30 },
            },
            SavedSearchQueries = [new SavedSearchQuery { Name = "Invoices", Query = "invoice" }],
            Peers = [new PeerConfiguration { DeviceId = "peer-1", DeviceName = "Laptop" }],
            Preferences = new PreferencesConfiguration
            {
                ClassicWindowHotkey = new WindowStateConfiguration { Left = 10, Top = 20, Width = 300, Height = 400 },
                ClassicWindowTaskbar = new WindowStateConfiguration { Left = 30, Top = 40 },
                ExplorerWindow = new WindowStateConfiguration { Width = 900 },
                ClassicExpandedHeight = 777,
                ClassicIsDroppedDown = true,
                ClassicIsTacked = true,
                ClipBarLastPosition = "5,6",
                ExplorerLayout = ExplorerLayoutMode.FullHeightCollectionTree,
                PowerPasteDelay = 555,
            },
            MonacoEditor = new MonacoEditorConfiguration { FontFamily = "Fira Code", FontSize = 22, TabSize = 8 },
            Appearance = new AppearanceConfiguration { AppTheme = AppTheme.Dark },
            Networking = new NetworkingConfiguration { Enabled = true, Port = 50000 },
            Print = new PrintConfiguration { QuickPrintEnabled = true },
            RecentEmojis = [new RecentEmoji { Emoji = "x" }],
        };

    private static string ToToml(ClipMateConfiguration configuration) => TomlSerializer.Serialize(configuration, TomlSerialization.Options);

    [Test]
    public async Task Request_DoesNotNarrowAWiderResetAlreadyWaiting()
    {
        // Arrange
        var configuration = new ClipMateConfiguration();
        SettingsReset.Request(configuration, PendingSettingsReset.Everything);

        // Act
        SettingsReset.Request(configuration, PendingSettingsReset.LayoutAndFonts);

        // Assert
        await Assert.That(configuration.PendingSettingsReset).IsEqualTo(PendingSettingsReset.Everything);
    }

    [Test]
    public async Task Request_LeavesEverySettingAsIsUntilTheNextStart()
    {
        // Arrange
        var configuration = CreateCustomizedConfiguration();

        // Act
        SettingsReset.Request(configuration, PendingSettingsReset.Everything);

        // Assert
        await Assert.That(configuration.MonacoEditor.FontSize).IsEqualTo(22);
        await Assert.That(configuration.Appearance.AppTheme).IsEqualTo(AppTheme.Dark);
    }

    [Test]
    public async Task ApplyPending_WithNothingWaiting_ChangesNothing()
    {
        // Arrange
        var configuration = CreateCustomizedConfiguration();
        var before = ToToml(configuration);

        // Act
        var result = SettingsReset.ApplyPending(configuration);

        // Assert
        await Assert.That(ToToml(result)).IsEqualTo(before);
    }

    [Test]
    public async Task ApplyPending_LayoutAndFonts_ResetsWindowStateAndEditorFont()
    {
        // Arrange
        var configuration = CreateCustomizedConfiguration();
        SettingsReset.Request(configuration, PendingSettingsReset.LayoutAndFonts);

        // Act
        var result = SettingsReset.ApplyPending(configuration);

        // Assert
        var defaults = new ClipMateConfiguration();
        await Assert.That(result.Preferences.ClassicWindowHotkey.Left).IsNull();
        await Assert.That(result.Preferences.ClassicWindowHotkey.Width).IsNull();
        await Assert.That(result.Preferences.ClassicWindowTaskbar.Left).IsNull();
        await Assert.That(result.Preferences.ExplorerWindow.Width).IsNull();
        await Assert.That(result.Preferences.ClassicExpandedHeight).IsEqualTo(defaults.Preferences.ClassicExpandedHeight);
        await Assert.That(result.Preferences.ClassicIsDroppedDown).IsFalse();
        await Assert.That(result.Preferences.ClassicIsTacked).IsFalse();
        await Assert.That(result.Preferences.ClipBarLastPosition).IsNull();
        await Assert.That(result.MonacoEditor.FontFamily).IsEqualTo(defaults.MonacoEditor.FontFamily);
        await Assert.That(result.MonacoEditor.FontSize).IsEqualTo(defaults.MonacoEditor.FontSize);
        await Assert.That(result.PendingSettingsReset).IsEqualTo(PendingSettingsReset.None);
    }

    [Test]
    public async Task ApplyPending_LayoutAndFonts_LeavesOtherSettingsAlone()
    {
        // Arrange
        var configuration = CreateCustomizedConfiguration();
        SettingsReset.Request(configuration, PendingSettingsReset.LayoutAndFonts);

        // Act
        var result = SettingsReset.ApplyPending(configuration);

        // Assert: ClipMate 7 keeps its ExplorerLayout under Preferences, which this reset doesn't erase
        await Assert.That(result.Preferences.ExplorerLayout).IsEqualTo(ExplorerLayoutMode.FullHeightCollectionTree);
        await Assert.That(result.Preferences.PowerPasteDelay).IsEqualTo(555);
        await Assert.That(result.MonacoEditor.TabSize).IsEqualTo(8);
        await Assert.That(result.Appearance.AppTheme).IsEqualTo(AppTheme.Dark);
        await Assert.That(result.Networking.Enabled).IsTrue();
        await Assert.That(result.Databases.ContainsKey("work")).IsTrue();
    }

    [Test]
    public async Task ApplyPending_Everything_LeavesOnlyWhatIsKeptOnTopOfFactoryDefaults()
    {
        // Arrange
        var configuration = CreateCustomizedConfiguration();
        var expected = new ClipMateConfiguration
        {
            Version = ConfigurationUpgrader.CurrentVersion,
            DefaultDatabase = configuration.DefaultDatabase,
            Databases = configuration.Databases,
            SavedSearchQueries = configuration.SavedSearchQueries,
            Peers = configuration.Peers,
        };

        SettingsReset.Request(configuration, PendingSettingsReset.Everything);

        // Act
        var result = SettingsReset.ApplyPending(configuration);

        // Assert: the whole file, so a setting added later can't escape the reset unnoticed
        await Assert.That(ToToml(result)).IsEqualTo(ToToml(expected));
        await Assert.That(result.Databases["work"].PurgeDays).IsEqualTo(30);
    }
}
