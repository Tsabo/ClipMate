using ClipMate.Core.Models;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for application name normalization in <see cref="IApplicationProfileService" />.
/// </summary>
[Category("ApplicationProfileService")]
[Category("Normalization")]
public class ApplicationProfileServiceNormalizationTests : ApplicationProfileServiceTestsBase
{
    [Test]
    public async Task UpdateProfileAsync_SavesUnderTheNormalizedName()
    {
        // Arrange: the name as a caller might give it; every lookup uses the normalized one
        var profile = new ApplicationProfile { ApplicationName = " notepad.exe " };
        A.CallTo(() => MockStore.AddOrUpdateProfileAsync(A<ApplicationProfile>._, A<CancellationToken>._)).Returns(Task.CompletedTask);

        // Act
        await Service.UpdateProfileAsync(profile);

        // Assert
        A.CallTo(() => MockStore.AddOrUpdateProfileAsync(A<ApplicationProfile>.That.Matches(p => p.ApplicationName == "NOTEPAD"),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task NormalizeApplicationName_RemovesExeExtension()
    {
        // Act
        var result = Service.NormalizeApplicationName("notepad.exe");

        // Assert
        await Assert.That(result).IsEqualTo("NOTEPAD");
    }

    [Test]
    public async Task NormalizeApplicationName_ConvertsToUppercase()
    {
        // Act
        var result = Service.NormalizeApplicationName("Chrome");

        // Assert
        await Assert.That(result).IsEqualTo("CHROME");
    }

    [Test]
    public async Task NormalizeApplicationName_HandlesExeInDifferentCase()
    {
        // Act
        var result = Service.NormalizeApplicationName("NOTEPAD.EXE");

        // Assert
        await Assert.That(result).IsEqualTo("NOTEPAD");
    }

    [Test]
    public async Task NormalizeApplicationName_HandlesNoExtension()
    {
        // Act
        var result = Service.NormalizeApplicationName("notepad");

        // Assert
        await Assert.That(result).IsEqualTo("NOTEPAD");
    }
}
