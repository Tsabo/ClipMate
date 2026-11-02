using ClipMate.Core.Models;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for <see cref="ApplicationProfileStore" />.
/// </summary>
public class ApplicationProfileStoreTests : IAsyncDisposable
{
    private string _tempFilePath = null!;

    public async ValueTask DisposeAsync()
    {
        await Cleanup();
        GC.SuppressFinalize(this);
    }

    [Before(Test)]
    public Task SetupAsync()
    {
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"test-profiles-{Guid.NewGuid()}.toml");
        return Task.CompletedTask;
    }

    [After(Test)]
    public async Task Cleanup()
    {
        if (File.Exists(_tempFilePath))
            File.Delete(_tempFilePath);

        foreach (var kept in Directory.GetFiles(Path.GetDirectoryName(_tempFilePath)!, Path.GetFileName(_tempFilePath) + ".unreadable-*"))
            File.Delete(kept);

        await Task.CompletedTask;
    }

    [Test]
    public async Task LoadProfilesAsync_ReturnsEmptyDictionary_WhenFileDoesNotExist()
    {
        // Arrange
        var logger = A.Fake<ILogger<ApplicationProfileStore>>();
        var store = new ApplicationProfileStore(_tempFilePath, logger);

        // Act
        var profiles = await store.LoadProfilesAsync();

        // Assert
        await Assert.That(profiles).IsNotNull();
        await Assert.That(profiles.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SaveProfilesAsync_CreatesFile_WithValidTomlContent()
    {
        // Arrange
        var logger = A.Fake<ILogger<ApplicationProfileStore>>();
        var store = new ApplicationProfileStore(_tempFilePath, logger);

        var profiles = new Dictionary<string, ApplicationProfile>
        {
            ["NOTEPAD"] = ApplicationProfileTestFixtures.GetNotepadProfile(),
        };

        // Act
        await store.SaveProfilesAsync(profiles);

        // Assert
        await Assert.That(File.Exists(_tempFilePath)).IsTrue();

        var fileContent = await File.ReadAllTextAsync(_tempFilePath);
        await Assert.That(fileContent).Contains("[profiles.NOTEPAD]");
        await Assert.That(fileContent).Contains("enabled = true");
        await Assert.That(fileContent).Contains("TEXT = true");
    }

    [Test]
    public async Task LoadProfilesAsync_FileThatCannotBeParsed_IsKeptAndNotSavedOver()
    {
        // Arrange: a damaged profiles file
        var store = new ApplicationProfileStore(_tempFilePath, A.Fake<ILogger<ApplicationProfileStore>>());
        await File.WriteAllTextAsync(_tempFilePath, "[profiles.NOTEPAD\nenabled = ");

        // Act: loading, then learning a new profile, which saves
        var loaded = await store.LoadProfilesAsync();
        await store.AddOrUpdateProfileAsync(ApplicationProfileTestFixtures.GetChromeProfile());

        // Assert: the damaged file is kept beside the new one
        await Assert.That(loaded).IsEmpty();
        var kept = Directory.GetFiles(Path.GetDirectoryName(_tempFilePath)!, Path.GetFileName(_tempFilePath) + ".unreadable-*");
        await Assert.That(kept.Length).IsEqualTo(1);
        await Assert.That(await File.ReadAllTextAsync(kept[0])).IsEqualTo("[profiles.NOTEPAD\nenabled = ");
    }

    [Test]
    public async Task LoadProfilesAsync_ProfileWithoutEnabledKey_IsOn()
    {
        // Arrange: a profile written without the key, as a new profile defaults to on
        var store = new ApplicationProfileStore(_tempFilePath, A.Fake<ILogger<ApplicationProfileStore>>());
        await File.WriteAllTextAsync(_tempFilePath, "[profiles.NOTEPAD]\nTEXT = true\n");

        // Act
        var loaded = await store.LoadProfilesAsync();

        // Assert
        await Assert.That(loaded["NOTEPAD"].Enabled).IsTrue();
    }

    [Test]
    public async Task LoadProfilesAsync_DeserializesProfiles_FromValidToml()
    {
        // Arrange
        var logger = A.Fake<ILogger<ApplicationProfileStore>>();
        var store = new ApplicationProfileStore(_tempFilePath, logger);

        var originalProfiles = new Dictionary<string, ApplicationProfile>
        {
            ["NOTEPAD"] = ApplicationProfileTestFixtures.GetNotepadProfile(),
            ["CHROME"] = ApplicationProfileTestFixtures.GetChromeProfile(),
        };

        await store.SaveProfilesAsync(originalProfiles);

        // Act
        var loadedProfiles = await store.LoadProfilesAsync();

        // Assert
        await Assert.That(loadedProfiles.Count).IsEqualTo(2);
        await Assert.That(loadedProfiles.ContainsKey("NOTEPAD")).IsTrue();
        await Assert.That(loadedProfiles["NOTEPAD"].Enabled).IsTrue();
        await Assert.That(loadedProfiles["NOTEPAD"].Formats["TEXT"]).IsTrue();
    }

    [Test]
    public async Task AddOrUpdateProfileAsync_AddsNewProfile_WhenNotExists()
    {
        // Arrange
        var logger = A.Fake<ILogger<ApplicationProfileStore>>();
        var store = new ApplicationProfileStore(_tempFilePath, logger);

        var profile = ApplicationProfileTestFixtures.GetNotepadProfile();

        // Act
        await store.AddOrUpdateProfileAsync(profile);
        var profiles = await store.LoadProfilesAsync();

        // Assert
        await Assert.That(profiles.ContainsKey("NOTEPAD")).IsTrue();
    }

    [Test]
    public async Task AddOrUpdateProfileAsync_UpdatesExistingProfile_WhenExists()
    {
        // Arrange
        var logger = A.Fake<ILogger<ApplicationProfileStore>>();
        var store = new ApplicationProfileStore(_tempFilePath, logger);

        var profile = ApplicationProfileTestFixtures.GetNotepadProfile();
        await store.AddOrUpdateProfileAsync(profile);

        // Modify profile
        profile.Enabled = false;
        profile.Formats["TEXT"] = false;

        // Act
        await store.AddOrUpdateProfileAsync(profile);
        var profiles = await store.LoadProfilesAsync();

        // Assert
        await Assert.That(profiles["NOTEPAD"].Enabled).IsFalse();
        await Assert.That(profiles["NOTEPAD"].Formats["TEXT"]).IsFalse();
    }

    [Test]
    public async Task DeleteProfileAsync_RemovesProfile_WhenExists()
    {
        // Arrange
        var logger = A.Fake<ILogger<ApplicationProfileStore>>();
        var store = new ApplicationProfileStore(_tempFilePath, logger);

        await store.AddOrUpdateProfileAsync(ApplicationProfileTestFixtures.GetNotepadProfile());
        await store.AddOrUpdateProfileAsync(ApplicationProfileTestFixtures.GetChromeProfile());

        // Act
        await store.DeleteProfileAsync("NOTEPAD");
        var profiles = await store.LoadProfilesAsync();

        // Assert
        await Assert.That(profiles.ContainsKey("NOTEPAD")).IsFalse();
        await Assert.That(profiles.ContainsKey("CHROME")).IsTrue();
    }

    [Test]
    public async Task DeleteAllProfilesAsync_RemovesAllProfiles()
    {
        // Arrange
        var logger = A.Fake<ILogger<ApplicationProfileStore>>();
        var store = new ApplicationProfileStore(_tempFilePath, logger);

        await store.AddOrUpdateProfileAsync(ApplicationProfileTestFixtures.GetNotepadProfile());
        await store.AddOrUpdateProfileAsync(ApplicationProfileTestFixtures.GetChromeProfile());

        // Act
        await store.DeleteAllProfilesAsync();
        var profiles = await store.LoadProfilesAsync();

        // Assert
        await Assert.That(profiles.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetProfileAsync_ReturnsProfile_WhenExists()
    {
        // Arrange
        var logger = A.Fake<ILogger<ApplicationProfileStore>>();
        var store = new ApplicationProfileStore(_tempFilePath, logger);

        await store.AddOrUpdateProfileAsync(ApplicationProfileTestFixtures.GetNotepadProfile());

        // Act
        var profile = await store.GetProfileAsync("NOTEPAD");

        // Assert
        await Assert.That(profile).IsNotNull();
        await Assert.That(profile!.ApplicationName).IsEqualTo("NOTEPAD");
    }

    [Test]
    public async Task GetProfileAsync_ReturnsNull_WhenNotExists()
    {
        // Arrange
        var logger = A.Fake<ILogger<ApplicationProfileStore>>();
        var store = new ApplicationProfileStore(_tempFilePath, logger);

        // Act
        var profile = await store.GetProfileAsync("NONEXISTENT");

        // Assert
        await Assert.That(profile).IsNull();
    }
}
