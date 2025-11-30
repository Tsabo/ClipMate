using ClipMate.Core.Models;
using Moq;

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
        MockStore.Verify(p => p.GetProfileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ShouldCaptureFormatAsync_ReturnsFalse_WhenApplicationDisabled()
    {
        // Arrange
        Service.SetApplicationProfilesEnabled(true);
        var profile = ApplicationProfileTestFixtures.GetNotepadProfile();
        profile.Enabled = false;
        MockStore.Setup(p => p.GetProfileAsync("NOTEPAD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

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
        MockStore.Setup(p => p.GetProfileAsync("NOTEPAD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

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
        MockStore.Setup(p => p.GetProfileAsync("NOTEPAD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

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
        MockStore.Setup(p => p.GetProfileAsync("NOTEPAD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

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
        MockStore.Setup(p => p.GetProfileAsync("NOTEPAD", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationProfile?)null);

        MockStore.Setup(s => s.AddOrUpdateProfileAsync(It.IsAny<ApplicationProfile>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await Service.ShouldCaptureFormatAsync("notepad.exe", "TEXT");

        // Assert
        await Assert.That(result).IsTrue(); // TEXT is in smart defaults
        MockStore.Verify(s => s.AddOrUpdateProfileAsync(It.Is<ApplicationProfile>(p => p.ApplicationName == "NOTEPAD"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ShouldCaptureFormatAsync_NormalizesApplicationName()
    {
        // Arrange
        Service.SetApplicationProfilesEnabled(true);
        MockStore.Setup(p => p.GetProfileAsync("CHROME", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApplicationProfile?)null);

        MockStore.Setup(s => s.AddOrUpdateProfileAsync(It.IsAny<ApplicationProfile>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await Service.ShouldCaptureFormatAsync("chrome.exe", "TEXT");

        // Assert
        MockStore.Verify(p => p.GetProfileAsync("CHROME", It.IsAny<CancellationToken>()), Times.Once);
    }
}
