using ClipMate.Core.Models;
using FakeItEasy;

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
        A.CallTo(() => MockStore.AddOrUpdateProfileAsync(profile, A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        // Act
        await Service.UpdateProfileAsync(profile);

        // Assert
        A.CallTo(() => MockStore.AddOrUpdateProfileAsync(profile, A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task GetAllProfilesAsync_ReturnsAllProfiles()
    {
        // Arrange
        var profiles = new Dictionary<string, ApplicationProfile>
        {
            ["NOTEPAD"] = ApplicationProfileTestFixtures.GetNotepadProfile(),
            ["CHROME"] = ApplicationProfileTestFixtures.GetChromeProfile(),
        };

        A.CallTo(() => MockStore.LoadProfilesAsync(A<CancellationToken>.Ignored))
            .Returns(profiles);

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
        A.CallTo(() => MockStore.DeleteProfileAsync("NOTEPAD", A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        // Act
        await Service.DeleteProfileAsync("notepad.exe");

        // Assert
        A.CallTo(() => MockStore.DeleteProfileAsync("NOTEPAD", A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task DeleteProfileAsync_NormalizesApplicationName()
    {
        // Arrange
        A.CallTo(() => MockStore.DeleteProfileAsync("CHROME", A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        // Act
        await Service.DeleteProfileAsync("chrome.exe");

        // Assert
        A.CallTo(() => MockStore.DeleteProfileAsync("CHROME", A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task DeleteAllProfilesAsync_CallsStore()
    {
        // Arrange
        A.CallTo(() => MockStore.DeleteAllProfilesAsync(A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        // Act
        await Service.DeleteAllProfilesAsync();

        // Assert
        A.CallTo(() => MockStore.DeleteAllProfilesAsync(A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }
}
