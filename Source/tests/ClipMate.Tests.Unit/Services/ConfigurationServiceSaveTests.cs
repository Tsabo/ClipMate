using ClipMate.Core.Models.Configuration;
using ClipMate.Data.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Saving clipmate.toml under contention: a transient lock on the file (antivirus, indexer, backup tool)
/// or another <see cref="ConfigurationService" /> on the same file must not lose the save.
/// </summary>
public class ConfigurationServiceSaveTests
{
    private string _directory = null!;

    [Before(Test)]
    public void Setup() => _directory = Path.Combine(Path.GetTempPath(), $"clipmate_configsave_{Guid.NewGuid():N}");

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }

    [Test]
    public async Task SaveAsync_DestinationBrieflyHeldOpenByAnotherProcess_SavesOnceItIsReleased()
    {
        // Arrange: the file exists and something (like an antivirus scan) holds it open without delete sharing
        var service = CreateService();
        await service.SaveAsync();
        service.Configuration.Databases["held"] = new DatabaseConfiguration { Name = "Held", FilePath = "held.db" };
        var holder = new FileStream(service.ConfigurationFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var release = Task.Run(async () =>
        {
            await Task.Delay(100);
            await holder.DisposeAsync();
        });

        // Act
        try
        {
            await service.SaveAsync();
        }
        finally
        {
            await release;
        }

        // Assert
        var reloaded = new ConfigurationService(_directory, NullLogger<ConfigurationService>.Instance);
        await reloaded.LoadAsync();
        await Assert.That(reloaded.Configuration.Databases.ContainsKey("held")).IsTrue();
    }

    private ConfigurationService CreateService()
    {
        var service = new ConfigurationService(_directory, NullLogger<ConfigurationService>.Instance);
        service.Configuration.Databases["db_test"] = new DatabaseConfiguration { Name = "Test", FilePath = "test.db" };
        service.Configuration.DefaultDatabase = "db_test";
        return service;
    }
}
