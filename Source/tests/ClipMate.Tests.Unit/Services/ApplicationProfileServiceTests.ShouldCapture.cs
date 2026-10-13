using ClipMate.Core.Models;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for ShouldCaptureFormatAsync in <see cref="IApplicationProfileService" />.
/// </summary>
[Category("ApplicationProfileService")]
[Category("ShouldCapture")]
public class ApplicationProfileServiceShouldCaptureTests : ApplicationProfileServiceTestsBase
{
    [Test]
    public async Task ShouldCaptureFormatAsync_ReturnsFalse_WhenProfilesDisabled()
    {
        // Arrange
        Service.SetApplicationProfilesEnabled(false);

        // Act
        var result = await Service.ShouldCaptureFormatAsync("notepad.exe", "TEXT");

        // Assert
        await Assert.That(result).IsFalse();
        A.CallTo(() => MockStore.GetProfileAsync(A<string>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
    }

    [Test]
    public async Task ShouldCaptureFormatAsync_ReturnsFalse_WhenApplicationDisabled()
    {
        // Arrange
        Service.SetApplicationProfilesEnabled(true);
        var profile = ApplicationProfileTestFixtures.GetNotepadProfile();
        profile.Enabled = false;
        A.CallTo(() => MockStore.GetProfileAsync("NOTEPAD", A<CancellationToken>.Ignored))
            .Returns(profile);

        // Act
        var result = await Service.ShouldCaptureFormatAsync("notepad.exe", "TEXT");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ShouldCaptureFormatAsync_ReturnsTrue_WhenFormatEnabled()
    {
        // Arrange
        Service.SetApplicationProfilesEnabled(true);
        var profile = ApplicationProfileTestFixtures.GetNotepadProfile();
        A.CallTo(() => MockStore.GetProfileAsync("NOTEPAD", A<CancellationToken>.Ignored))
            .Returns(profile);

        // Act
        var result = await Service.ShouldCaptureFormatAsync("notepad.exe", "TEXT");

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ShouldCaptureFormatAsync_ReturnsFalse_WhenFormatDisabled()
    {
        // Arrange
        Service.SetApplicationProfilesEnabled(true);
        var profile = ApplicationProfileTestFixtures.GetNotepadProfile();
        A.CallTo(() => MockStore.GetProfileAsync("NOTEPAD", A<CancellationToken>.Ignored))
            .Returns(profile);

        // Act
        var result = await Service.ShouldCaptureFormatAsync("notepad.exe", "BITMAP");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ShouldCaptureFormatAsync_ReturnsFalse_WhenFormatNotInProfile()
    {
        // Arrange
        Service.SetApplicationProfilesEnabled(true);
        var profile = ApplicationProfileTestFixtures.GetNotepadProfile();
        A.CallTo(() => MockStore.GetProfileAsync("NOTEPAD", A<CancellationToken>.Ignored))
            .Returns(profile);

        // Act
        var result = await Service.ShouldCaptureFormatAsync("notepad.exe", "UnknownFormat");

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ShouldCaptureFormatAsync_CreatesProfile_WhenNotExists()
    {
        // Arrange
        Service.SetApplicationProfilesEnabled(true);
        A.CallTo(() => MockStore.GetProfileAsync("NOTEPAD", A<CancellationToken>.Ignored))
            .Returns((ApplicationProfile?)null);

        A.CallTo(() => MockStore.AddOrUpdateProfileAsync(A<ApplicationProfile>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Service.ShouldCaptureFormatAsync("notepad.exe", "CF_TEXT");

        // Assert
        await Assert.That(result).IsTrue(); // CF_TEXT is in smart defaults
        A.CallTo(() => MockStore.AddOrUpdateProfileAsync(A<ApplicationProfile>.That.Matches(p => p.ApplicationName == "NOTEPAD"), A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task ShouldCaptureFormatAsync_NormalizesApplicationName()
    {
        // Arrange
        Service.SetApplicationProfilesEnabled(true);
        A.CallTo(() => MockStore.GetProfileAsync("CHROME", A<CancellationToken>.Ignored))
            .Returns((ApplicationProfile?)null);

        A.CallTo(() => MockStore.AddOrUpdateProfileAsync(A<ApplicationProfile>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        // Act
        await Service.ShouldCaptureFormatAsync("chrome.exe", "TEXT");

        // Assert
        A.CallTo(() => MockStore.GetProfileAsync("CHROME", A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }
}
