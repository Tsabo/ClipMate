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

    [Test]
    public async Task SaveAsync_DestinationHeldOpenThroughEveryRetry_ThrowsAndKeepsThePreviousFile()
    {
        // Arrange
        var service = CreateService([TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)]);
        await service.SaveAsync();
        var before = await File.ReadAllTextAsync(service.ConfigurationFilePath);
        service.Configuration.Databases["held"] = new DatabaseConfiguration { Name = "Held", FilePath = "held.db" };

        // Act / Assert
        await using (new FileStream(service.ConfigurationFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            await Assert.That(() => service.SaveAsync()).Throws<UnauthorizedAccessException>();

        await Assert.That(await File.ReadAllTextAsync(service.ConfigurationFilePath)).IsEqualTo(before);
    }

    [Test]
    public async Task SaveAsync_TwoServicesOnTheSameFileSavingConcurrently_AllSavesSucceed()
    {
        // Arrange: e.g. the startup ConfigurationService in App.xaml.cs and the DI singleton share clipmate.toml
        var first = CreateService();
        var second = CreateService();

        // Act
        var saves = Enumerable.Range(0, 25)
            .SelectMany(_ => new[] { Task.Run(() => first.SaveAsync()), Task.Run(() => second.SaveAsync()) })
            .ToList();
        await Task.WhenAll(saves);

        // Assert
        var reloaded = new ConfigurationService(_directory, NullLogger<ConfigurationService>.Instance);
        await reloaded.LoadAsync();
        await Assert.That(reloaded.Configuration.Databases.ContainsKey("db_test")).IsTrue();
        await Assert.That(File.Exists(reloaded.ConfigurationFilePath + ".tmp")).IsFalse();
    }

    private ConfigurationService CreateService(IReadOnlyList<TimeSpan>? replaceRetryDelays = null)
    {
        var service = replaceRetryDelays == null
            ? new ConfigurationService(_directory, NullLogger<ConfigurationService>.Instance)
            : new ConfigurationService(_directory, NullLogger<ConfigurationService>.Instance, replaceRetryDelays);
        service.Configuration.Databases["db_test"] = new DatabaseConfiguration { Name = "Test", FilePath = "test.db" };
        service.Configuration.DefaultDatabase = "db_test";
        return service;
    }
}
