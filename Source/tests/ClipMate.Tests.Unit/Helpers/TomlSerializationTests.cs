using ClipMate.Core.Helpers;
using ClipMate.Core.Models.Configuration;
using Microsoft.Extensions.Logging;
using Tomlyn;

namespace ClipMate.Tests.Unit.Helpers;

/// <summary>
/// Tests that the shared Tomlyn options keep the on-disk format compatible with existing config files.
/// </summary>
public class TomlSerializationTests
{
    [Test]
    public async Task Serialize_WritesSnakeCaseKeysAndEnumNames()
    {
        // Act
        var toml = TomlSerializer.Serialize(new ClipMateConfiguration(), TomlSerialization.Options);

        // Assert
        await Assert.That(toml).Contains("[preferences]");
        await Assert.That(toml).Contains("log_level = \"Information\"");
        await Assert.That(toml).Contains("tray_icon_left_click_action = \"ShowExplorerWindow\"");
    }

    [Test]
    public async Task Deserialize_ReadsEnumFromIntegerOrName()
    {
        // Act
        var fromInteger = TomlSerializer.Deserialize<ClipMateConfiguration>("[preferences]\nlog_level = 3\n", TomlSerialization.Options)!;
        var fromName = TomlSerializer.Deserialize<ClipMateConfiguration>("[preferences]\nlog_level = \"Debug\"\n", TomlSerialization.Options)!;

        // Assert
        await Assert.That(fromInteger.Preferences.LogLevel).IsEqualTo(LogLevel.Warning);
        await Assert.That(fromName.Preferences.LogLevel).IsEqualTo(LogLevel.Debug);
    }

    [Test]
    public async Task Deserialize_InvalidEnumName_ThrowsTomlException()
    {
        // Act & Assert
        await Assert.That(() => TomlSerializer.Deserialize<ClipMateConfiguration>("[preferences]\nlog_level = \"Bogus\"\n", TomlSerialization.Options))
            .Throws<TomlException>();
    }

    [Test]
    public async Task Deserialize_IgnoresUnknownKeys()
    {
        // Act
        var config = TomlSerializer.Deserialize<ClipMateConfiguration>("version = 1\nremoved_setting = true\n", TomlSerialization.Options)!;

        // Assert
        await Assert.That(config.Version).IsEqualTo(1);
    }

    [Test]
    [Arguments(DateTimeKind.Unspecified)]
    [Arguments(DateTimeKind.Local)]
    [Arguments(DateTimeKind.Utc)]
    public async Task Serialize_DateTime_WritesLocalDateTimeWithMilliseconds(DateTimeKind kind)
    {
        // Arrange
        var config = new ClipMateConfiguration();
        config.Export.DateLastExport = new DateTime(2026, 10, 1, 0, 15, 0, 316, kind);

        // Act
        var toml = TomlSerializer.Serialize(config, TomlSerialization.Options);
        var roundTripped = TomlSerializer.Deserialize<ClipMateConfiguration>(toml, TomlSerialization.Options)!;

        // Assert
        await Assert.That(toml).Contains("date_last_export = 2026-10-01T00:15:00.316\n");
        await Assert.That(roundTripped.Export.DateLastExport).IsEqualTo(new DateTime(2026, 10, 1, 0, 15, 0, 316));
    }

    [Test]
    public async Task Deserialize_ReplacesDefaultCollectionsInsteadOfAppending()
    {
        // Arrange
        var defaults = new ClipMateConfiguration();
        var toml = TomlSerializer.Serialize(defaults, TomlSerialization.Options);

        // Act
        var roundTripped = TomlSerializer.Deserialize<ClipMateConfiguration>(toml, TomlSerialization.Options)!;

        // Assert
        await Assert.That(roundTripped.Preferences.QuickPasteFormattingStrings.Count)
            .IsEqualTo(defaults.Preferences.QuickPasteFormattingStrings.Count);
    }

    [Test]
    public async Task Networking_DefaultsToOffWithComputerNameAndRoundTrips()
    {
        // Arrange
        var defaults = new ClipMateConfiguration();
        var changed = new ClipMateConfiguration { Networking = { Enabled = true, Port = 50123, DeviceName = "Office PC" } };

        // Act
        var toml = TomlSerializer.Serialize(changed, TomlSerialization.Options);
        var roundTripped = TomlSerializer.Deserialize<ClipMateConfiguration>(toml, TomlSerialization.Options)!;

        // Assert
        await Assert.That(defaults.Networking.Enabled).IsFalse();
        await Assert.That(defaults.Networking.DeviceName).IsEqualTo(Environment.MachineName);
        await Assert.That(defaults.Networking.Port).IsEqualTo(NetworkingConfiguration.DefaultPort);
        await Assert.That(toml).Contains("[networking]");
        await Assert.That(roundTripped.Networking.Enabled).IsTrue();
        await Assert.That(roundTripped.Networking.Port).IsEqualTo(50123);
        await Assert.That(roundTripped.Networking.DeviceName).IsEqualTo("Office PC");
    }
}
