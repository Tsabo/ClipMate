using ClipMate.Core.Models.Configuration;
using ClipMate.Data.Services;
using Microsoft.Extensions.Logging;
using Xunit;
using Shouldly;

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

    [Fact]
    public async Task LoadAsync_CreatesDefaultConfiguration_WhenFileDoesNotExist()
    {
        // Arrange
        var service = new ConfigurationService(_testConfigDirectory, _logger);

        // Act
        var config = await service.LoadAsync();

        // Assert
        config.ShouldNotBeNull();
        config.Version.ShouldBe(1);
        config.Databases.ShouldContainKey("MyClips");
        config.DefaultDatabase.ShouldBe("MyClips");
        config.Hotkeys.ShouldNotBeNull();
        config.Preferences.ShouldNotBeNull();
    }

    [Fact]
    public async Task SaveAsync_CreatesTomlFile()
    {
        // Arrange
        var service = new ConfigurationService(_testConfigDirectory, _logger);
        await service.LoadAsync();

        // Act
        await service.SaveAsync();

        // Assert
        System.IO.File.Exists(service.ConfigurationFilePath).ShouldBeTrue();
        var content = await System.IO.File.ReadAllTextAsync(service.ConfigurationFilePath);
        content.ShouldContain("[Preferences]");
        content.ShouldContain("[Hotkeys]");
        content.ShouldContain("[Databases.MyClips]");
    }

    [Fact]
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
        service.Configuration.Databases.ShouldContainKey("WorkClips");
        service.Configuration.Databases["WorkClips"].Name.ShouldBe("Work Clips");
        service.Configuration.Databases["WorkClips"].PurgeDays.ShouldBe(30);
    }

    [Fact]
    public async Task RemoveDatabaseAsync_RemovesDatabase()
    {
        // Arrange
        var service = new ConfigurationService(_testConfigDirectory, _logger);
        await service.LoadAsync();

        // Act
        await service.RemoveDatabaseAsync("MyClips");

        // Assert
        service.Configuration.Databases.ShouldNotContainKey("MyClips");
    }

    [Fact]
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
        service.Configuration.ApplicationProfiles.ShouldContainKey("NOTEPAD");
        service.Configuration.ApplicationProfiles["NOTEPAD"].Formats.ShouldContainKey("TEXT");
    }

    [Fact]
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
        loadedConfig.Preferences.LogLevel.ShouldBe(5);
        loadedConfig.Hotkeys.QuickPaste.ShouldBe("Ctrl+Shift+Q");
    }

    [Fact]
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
        service.Configuration.Preferences.LogLevel.ShouldBe(3); // Default value
    }
}
