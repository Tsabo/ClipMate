using ClipMate.Core.Models;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

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

        await Task.CompletedTask;
    }

    [Test]
    public async Task LoadProfilesAsync_ReturnsEmptyDictionary_WhenFileDoesNotExist()
    {
        // Arrange
        var logger = new Mock<ILogger<ApplicationProfileStore>>();
        var store = new ApplicationProfileStore(_tempFilePath, logger.Object);

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
        var logger = new Mock<ILogger<ApplicationProfileStore>>();
        var store = new ApplicationProfileStore(_tempFilePath, logger.Object);

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
    public async Task LoadProfilesAsync_DeserializesProfiles_FromValidToml()
    {
        // Arrange
        var logger = new Mock<ILogger<ApplicationProfileStore>>();
        var store = new ApplicationProfileStore(_tempFilePath, logger.Object);

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
        var logger = new Mock<ILogger<ApplicationProfileStore>>();
        var store = new ApplicationProfileStore(_tempFilePath, logger.Object);

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
        var logger = new Mock<ILogger<ApplicationProfileStore>>();
        var store = new ApplicationProfileStore(_tempFilePath, logger.Object);

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
        var logger = new Mock<ILogger<ApplicationProfileStore>>();
        var store = new ApplicationProfileStore(_tempFilePath, logger.Object);

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
        var logger = new Mock<ILogger<ApplicationProfileStore>>();
        var store = new ApplicationProfileStore(_tempFilePath, logger.Object);

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
        var logger = new Mock<ILogger<ApplicationProfileStore>>();
        var store = new ApplicationProfileStore(_tempFilePath, logger.Object);

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
        var logger = new Mock<ILogger<ApplicationProfileStore>>();
        var store = new ApplicationProfileStore(_tempFilePath, logger.Object);

        // Act
        var profile = await store.GetProfileAsync("NONEXISTENT");

        // Assert
        await Assert.That(profile).IsNull();
    }
}
