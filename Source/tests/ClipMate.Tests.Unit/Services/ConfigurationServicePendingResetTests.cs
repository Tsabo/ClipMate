using ClipMate.Core.Models.Configuration;
using ClipMate.Data.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Unit.Services;

public class ConfigurationServicePendingResetTests
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ClipMateTests", Guid.NewGuid().ToString());

    [After(Test)]
    public void DeleteDirectory()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    private ConfigurationService NewService() => new(_directory, NullLogger<ConfigurationService>.Instance);

    private async Task SaveCustomizedAsync(PendingSettingsReset reset)
    {
        await NewService().SaveAsync(new ClipMateConfiguration
        {
            Version = ConfigurationUpgrader.CurrentVersion,
            DefaultDatabase = "work",
            Databases = new Dictionary<string, DatabaseConfiguration>
            {
                ["work"] = new() { Name = "Work", FilePath = "work.db" },
            },
            MonacoEditor = new MonacoEditorConfiguration { FontSize = 22 },
            Appearance = new AppearanceConfiguration { AppTheme = AppTheme.Dark },
            PendingSettingsReset = reset,
        });
    }

    [Test]
    public async Task LoadAsync_WithEverythingWaiting_ResetsSettingsAndKeepsTheDatabases()
    {
        // Arrange
        await SaveCustomizedAsync(PendingSettingsReset.Everything);

        // Act
        var configuration = await NewService().LoadAsync();

        // Assert
        await Assert.That(configuration.Appearance.AppTheme).IsEqualTo(AppTheme.Light);
        await Assert.That(configuration.MonacoEditor.FontSize).IsEqualTo(new MonacoEditorConfiguration().FontSize);
        await Assert.That(configuration.DefaultDatabase).IsEqualTo("work");
        await Assert.That(configuration.Databases["work"].FilePath).IsEqualTo("work.db");
    }

    [Test]
    public async Task LoadAsync_WithAResetWaiting_SavesTheResultSoItRunsOnce()
    {
        // Arrange
        await SaveCustomizedAsync(PendingSettingsReset.LayoutAndFonts);
        await NewService().LoadAsync();

        // Change a font after the reset, as the user might
        var service = NewService();
        await service.LoadAsync();
        service.Configuration.MonacoEditor.FontSize = 30;
        await service.SaveAsync();

        // Act
        var configuration = await NewService().LoadAsync();

        // Assert
        await Assert.That(configuration.PendingSettingsReset).IsEqualTo(PendingSettingsReset.None);
        await Assert.That(configuration.MonacoEditor.FontSize).IsEqualTo(30);
        await Assert.That(configuration.Appearance.AppTheme).IsEqualTo(AppTheme.Dark);
    }

    [Test]
    public async Task SaveAsync_AfterARequest_KeepsItForTheNextStartWhateverIsSavedInBetween()
    {
        // Arrange
        await SaveCustomizedAsync(PendingSettingsReset.None);
        var service = NewService();
        await service.LoadAsync();
        SettingsReset.Request(service.Configuration, PendingSettingsReset.LayoutAndFonts);
        await service.SaveAsync();

        // Act: the window or the Options dialog saves what it holds before ClipMate exits
        service.Configuration.MonacoEditor.FontSize = 40;
        await service.SaveAsync();
        var configuration = await NewService().LoadAsync();

        // Assert
        await Assert.That(configuration.MonacoEditor.FontSize).IsEqualTo(new MonacoEditorConfiguration().FontSize);
    }
}
