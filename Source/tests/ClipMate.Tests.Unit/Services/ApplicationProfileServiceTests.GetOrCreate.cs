using ClipMate.Core.Models;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for GetOrCreateProfileAsync in <see cref="IApplicationProfileService" />.
/// </summary>
[Category("ApplicationProfileService")]
[Category("GetOrCreate")]
public class ApplicationProfileServiceGetOrCreateTests : ApplicationProfileServiceTestsBase
{
    [Test]
    public async Task GetOrCreateProfileAsync_ReturnsExistingProfile_WhenExists()
    {
        // Arrange
        var existingProfile = ApplicationProfileTestFixtures.GetNotepadProfile();
        A.CallTo(() => MockStore.GetProfileAsync("NOTEPAD", A<CancellationToken>.Ignored))
            .Returns(existingProfile);

        // Act
        var result = await Service.GetOrCreateProfileAsync("notepad.exe");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.ApplicationName).IsEqualTo("NOTEPAD");
        await Assert.That(result.Formats["TEXT"]).IsTrue();
        A.CallTo(() => MockStore.AddOrUpdateProfileAsync(A<ApplicationProfile>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
    }

    [Test]
    public async Task GetOrCreateProfileAsync_CreatesNewProfile_WhenNotExists()
    {
        // Arrange
        A.CallTo(() => MockStore.GetProfileAsync("NOTEPAD", A<CancellationToken>.Ignored))
            .Returns((ApplicationProfile?)null);

        A.CallTo(() => MockStore.AddOrUpdateProfileAsync(A<ApplicationProfile>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Service.GetOrCreateProfileAsync("notepad.exe");

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.ApplicationName).IsEqualTo("NOTEPAD");
        await Assert.That(result.Enabled).IsTrue();

        // Verify smart defaults
        await Assert.That(result.Formats["CF_TEXT"]).IsTrue();
        await Assert.That(result.Formats["CF_UNICODETEXT"]).IsTrue();
        await Assert.That(result.Formats["CF_BITMAP"]).IsTrue();
        await Assert.That(result.Formats["CF_HDROP"]).IsTrue();
        await Assert.That(result.Formats["HTML Format"]).IsTrue();
        await Assert.That(result.Formats["Rich Text Format"]).IsTrue();
        await Assert.That(result.Formats["DataObject"]).IsFalse();
        await Assert.That(result.Formats["CF_LOCALE"]).IsFalse();
        await Assert.That(result.Formats["OlePrivateData"]).IsFalse();

        A.CallTo(() => MockStore.AddOrUpdateProfileAsync(A<ApplicationProfile>.That.Matches(s => s.ApplicationName == "NOTEPAD"), A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task GetOrCreateProfileAsync_NormalizesApplicationName()
    {
        // Arrange
        A.CallTo(() => MockStore.GetProfileAsync("CHROME", A<CancellationToken>.Ignored))
            .Returns((ApplicationProfile?)null);

        A.CallTo(() => MockStore.AddOrUpdateProfileAsync(A<ApplicationProfile>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Service.GetOrCreateProfileAsync("chrome.exe");

        // Assert
        await Assert.That(result.ApplicationName).IsEqualTo("CHROME");
        A.CallTo(() => MockStore.GetProfileAsync("CHROME", A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }
}
