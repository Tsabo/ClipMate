namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for application name normalization in <see cref="IApplicationProfileService" />.
/// </summary>
[Category("ApplicationProfileService")]
[Category("Normalization")]
public class ApplicationProfileServiceNormalizationTests : ApplicationProfileServiceTestsBase
{
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
