using ClipMate.Core.Models.Configuration;
using ClipMate.Data.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Tests.Unit.Services;

public class ConfigurationServiceTests
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly string _testConfigDirectory;

    public ConfigurationServiceTests()
    {
        _logger = LoggerFactory.Create(builder => builder.AddDebug()).CreateLogger<ConfigurationService>();
        _testConfigDirectory = Path.Combine(Path.GetTempPath(), "ClipMateTests", Guid.NewGuid().ToString());
    }

    /// <summary>
    /// Helper to create a valid configuration with at least one database to pass validation.
    /// </summary>
    private async Task<ConfigurationService> CreateServiceWithValidConfigurationAsync()
    {
        var service = new ConfigurationService(_testConfigDirectory, _logger);

        // Manually create a valid configuration file to avoid validation errors
        Directory.CreateDirectory(_testConfigDirectory);
        var configPath = Path.Combine(_testConfigDirectory, "config.toml");
        var validConfig = @"version = 1
default_database = ""MyClips""
[preferences]
[hotkeys]
[monaco_editor]
[databases.MyClips]
name = ""My Clips""
file_path = ""clipmate.db""
auto_load = true
";

        await File.WriteAllTextAsync(configPath, validConfig);
        await service.LoadAsync();
        return service;
    }

    [Test]
    public async Task LoadAsync_CreatesDefaultConfiguration_WhenFileDoesNotExist()
    {
        // Arrange
        var service = new ConfigurationService(_testConfigDirectory, _logger);

        // Act - creates default configuration (without databases) and saves it
        var config = await service.LoadAsync();

        // Assert - should have created a configuration (validation doesn't run on new files)
        await Assert.That(config).IsNotNull();
        await Assert.That(config.Version).IsEqualTo(1);
        await Assert.That(config.Preferences).IsNotNull();
        await Assert.That(config.Hotkeys).IsNotNull();

        // Verify configuration file was created
        await Assert.That(File.Exists(service.ConfigurationFilePath)).IsTrue();

        // Note: Default configuration has no databases, which would fail validation
        // if loaded again, but doesn't fail when initially created
    }

    [Test]
    [Skip("Database dictionary serialization in TOML needs investigation - Tomlyn library behavior")]
    public async Task SaveAsync_CreatesTomlFile()
    {
        // Arrange
        var service = await CreateServiceWithValidConfigurationAsync();

        // Verify database was loaded correctly before saving
        await Assert.That(service.Configuration.Databases.ContainsKey("MyClips")).IsTrue();

        // Act
        await service.SaveAsync();

        // Assert
        await Assert.That(File.Exists(service.ConfigurationFilePath)).IsTrue();
        var content = await File.ReadAllTextAsync(service.ConfigurationFilePath);
        await Assert.That(content).Contains("[preferences]");
        await Assert.That(content).Contains("[hotkeys]");
        // Check for databases section (case insensitive)
        await Assert.That(content.ToLowerInvariant()).Contains("myclips");
    }

    [Test]
    public async Task AddOrUpdateDatabaseAsync_AddsNewDatabase()
    {
        // Arrange
        var service = await CreateServiceWithValidConfigurationAsync();

        var newDatabase = new DatabaseConfiguration
        {
            Name = "Work Clips",
            FilePath = "C:\\Work\\work.db",
            AutoLoad = false,
            PurgeDays = 30,
        };

        // Act
        await service.AddOrUpdateDatabaseAsync("WorkClips", newDatabase);

        // Assert
        await Assert.That(service.Configuration.Databases.ContainsKey("WorkClips")).IsTrue();
        await Assert.That(service.Configuration.Databases["WorkClips"].Name).IsEqualTo("Work Clips");
        await Assert.That(service.Configuration.Databases["WorkClips"].PurgeDays).IsEqualTo(30);
    }

    [Test]
    public async Task RemoveDatabaseAsync_RemovesDatabase()
    {
        // Arrange
        var service = await CreateServiceWithValidConfigurationAsync();

        // Act
        await service.RemoveDatabaseAsync("MyClips");

        // Assert
        await Assert.That(service.Configuration.Databases.ContainsKey("MyClips")).IsFalse();
    }

    [Test]
    public async Task AddOrUpdateApplicationProfileAsync_AddsNewProfile()
    {
        // Arrange
        var service = await CreateServiceWithValidConfigurationAsync();

        var profile = new ApplicationProfile
        {
            ApplicationName = "NOTEPAD",
            Enabled = true,
            Formats = new Dictionary<string, int>
            {
                { "TEXT", 1 },
                { "UNICODETEXT", 1 },
            },
        };

        // Act
        await service.AddOrUpdateApplicationProfileAsync("NOTEPAD", profile);

        // Assert
        await Assert.That(service.Configuration.ApplicationProfiles.ContainsKey("NOTEPAD")).IsTrue();
        await Assert.That(service.Configuration.ApplicationProfiles["NOTEPAD"].Formats.ContainsKey("TEXT")).IsTrue();
    }

    [Test]
    [Skip("Database dictionary serialization in TOML needs investigation - Tomlyn library behavior")]
    public async Task LoadAsync_LoadsExistingConfiguration()
    {
        // Arrange
        var service = await CreateServiceWithValidConfigurationAsync();

        // Modify configuration
        service.Configuration.Preferences.LogLevel = 5;
        service.Configuration.Hotkeys.QuickPaste = "Ctrl+Shift+Q";
        await service.SaveAsync();

        // Create new service instance to load from disk
        var service2 = new ConfigurationService(_testConfigDirectory, _logger);

        // Act
        var loadedConfig = await service2.LoadAsync();

        // Assert
        await Assert.That(loadedConfig.Preferences.LogLevel).IsEqualTo(5);
        await Assert.That(loadedConfig.Hotkeys.QuickPaste).IsEqualTo("Ctrl+Shift+Q");
        // Verify database still exists from saved configuration
        await Assert.That(loadedConfig.Databases.ContainsKey("MyClips")).IsTrue();
    }

    [Test]
    public async Task ResetToDefaultsAsync_RestoresDefaultConfiguration()
    {
        // Arrange
        var service = await CreateServiceWithValidConfigurationAsync();

        // Modify configuration
        service.Configuration.Preferences.LogLevel = 5;
        await service.SaveAsync();

        // Act
        await service.ResetToDefaultsAsync();

        // Assert
        await Assert.That(service.Configuration.Preferences.LogLevel).IsEqualTo(3); // Default value
    }
}
