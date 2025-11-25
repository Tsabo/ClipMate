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
        _testConfigDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ClipMateTests", Guid.NewGuid().ToString());
    }

    [Test]
    public async Task LoadAsync_CreatesDefaultConfiguration_WhenFileDoesNotExist()
    {
        // Arrange
        var service = new ConfigurationService(_testConfigDirectory, _logger);

        // Act
        var config = await service.LoadAsync();

        // Assert
        await Assert.That(config).IsNotNull();
        await Assert.That(config.Version).IsEqualTo(1);
        await Assert.That(config.Databases.ContainsKey("MyClips")).IsTrue();
        await Assert.That(config.DefaultDatabase).IsEqualTo("MyClips");
        await Assert.That(config.Hotkeys).IsNotNull();
        await Assert.That(config.Preferences).IsNotNull();
    }

    [Test]
    public async Task SaveAsync_CreatesTomlFile()
    {
        // Arrange
        var service = new ConfigurationService(_testConfigDirectory, _logger);
        await service.LoadAsync();

        // Act
        await service.SaveAsync();

        // Assert
        await Assert.That(System.IO.File.Exists(service.ConfigurationFilePath)).IsTrue();
        var content = await System.IO.File.ReadAllTextAsync(service.ConfigurationFilePath);
        await Assert.That(content).Contains("[preferences]");
        await Assert.That(content).Contains("[hotkeys]");
        await Assert.That(content).Contains("[databases.MyClips]");
    }

    [Test]
    public async Task AddOrUpdateDatabaseAsync_AddsNewDatabase()
    {
        // Arrange
        var service = new ConfigurationService(_testConfigDirectory, _logger);
        await service.LoadAsync();

        var newDatabase = new DatabaseConfiguration
        {
            Name = "Work Clips",
            Directory = "C:\\Work",
            AutoLoad = false,
            PurgeDays = 30
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
        var service = new ConfigurationService(_testConfigDirectory, _logger);
        await service.LoadAsync();

        // Act
        await service.RemoveDatabaseAsync("MyClips");

        // Assert
        await Assert.That(service.Configuration.Databases.ContainsKey("MyClips")).IsFalse();
    }

    [Test]
    public async Task AddOrUpdateApplicationProfileAsync_AddsNewProfile()
    {
        // Arrange
        var service = new ConfigurationService(_testConfigDirectory, _logger);
        await service.LoadAsync();

        var profile = new ApplicationProfile
        {
            ApplicationName = "NOTEPAD",
            Enabled = true,
            Formats = new Dictionary<string, int>
            {
                { "TEXT", 1 },
                { "UNICODETEXT", 1 }
            }
        };

        // Act
        await service.AddOrUpdateApplicationProfileAsync("NOTEPAD", profile);

        // Assert
        await Assert.That(service.Configuration.ApplicationProfiles.ContainsKey("NOTEPAD")).IsTrue();
        await Assert.That(service.Configuration.ApplicationProfiles["NOTEPAD"].Formats.ContainsKey("TEXT")).IsTrue();
    }

    [Test]
    public async Task LoadAsync_LoadsExistingConfiguration()
    {
        // Arrange
        var service = new ConfigurationService(_testConfigDirectory, _logger);
        await service.LoadAsync();
        
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
    }

    [Test]
    public async Task ResetToDefaultsAsync_RestoresDefaultConfiguration()
    {
        // Arrange
        var service = new ConfigurationService(_testConfigDirectory, _logger);
        await service.LoadAsync();
        
        // Modify configuration
        service.Configuration.Preferences.LogLevel = 5;
        await service.SaveAsync();

        // Act
        await service.ResetToDefaultsAsync();

        // Assert
        await Assert.That(service.Configuration.Preferences.LogLevel).IsEqualTo(3); // Default value
    }
}
