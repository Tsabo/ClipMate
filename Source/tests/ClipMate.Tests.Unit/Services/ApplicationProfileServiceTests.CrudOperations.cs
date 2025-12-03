using ClipMate.Core.Models;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for CRUD operations (Update, GetAll, Delete) in <see cref="IApplicationProfileService" />.
/// </summary>
[Category("ApplicationProfileService")]
[Category("CrudOperations")]
public class ApplicationProfileServiceCrudOperationsTests : ApplicationProfileServiceTestsBase
{
    [Test]
    public async Task UpdateProfileAsync_CallsStore()
    {
        // Arrange
        var profile = ApplicationProfileTestFixtures.GetNotepadProfile();
        MockStore.Setup(p => p.AddOrUpdateProfileAsync(profile, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await Service.UpdateProfileAsync(profile);

        // Assert
        MockStore.Verify(p => p.AddOrUpdateProfileAsync(profile, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetAllProfilesAsync_ReturnsAllProfiles()
    {
        // Arrange
        var profiles = new Dictionary<string, ApplicationProfile>
        {
            ["NOTEPAD"] = ApplicationProfileTestFixtures.GetNotepadProfile(),
            ["CHROME"] = ApplicationProfileTestFixtures.GetChromeProfile()
        };

        MockStore.Setup(p => p.LoadProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(profiles);

        // Act
        var result = await Service.GetAllProfilesAsync();

        // Assert
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result.ContainsKey("NOTEPAD")).IsTrue();
        await Assert.That(result.ContainsKey("CHROME")).IsTrue();
    }

    [Test]
    public async Task DeleteProfileAsync_CallsStore()
    {
        // Arrange
        MockStore.Setup(p => p.DeleteProfileAsync("NOTEPAD", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await Service.DeleteProfileAsync("notepad.exe");

        // Assert
        MockStore.Verify(p => p.DeleteProfileAsync("NOTEPAD", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task DeleteProfileAsync_NormalizesApplicationName()
    {
        // Arrange
        MockStore.Setup(p => p.DeleteProfileAsync("CHROME", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await Service.DeleteProfileAsync("chrome.exe");

        // Assert
        MockStore.Verify(p => p.DeleteProfileAsync("CHROME", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task DeleteAllProfilesAsync_CallsStore()
    {
        // Arrange
        MockStore.Setup(p => p.DeleteAllProfilesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await Service.DeleteAllProfilesAsync();

        // Assert
        MockStore.Verify(p => p.DeleteAllProfilesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
