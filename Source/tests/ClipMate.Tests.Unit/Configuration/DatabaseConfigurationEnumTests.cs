using ClipMate.Core.Models.Configuration;

namespace ClipMate.Tests.Unit.Configuration;

/// <summary>
/// Unit tests for database configuration enums.
/// </summary>
public class DatabaseConfigurationEnumTests
{
    [Test]
    public async Task TempFileLocation_ShouldHaveAllExpectedValues()
    {
        // Assert
        await Assert.That(Enum.IsDefined(typeof(TempFileLocation), TempFileLocation.DatabaseDirectory)).IsTrue();
        await Assert.That(Enum.IsDefined(typeof(TempFileLocation), TempFileLocation.SystemTmp)).IsTrue();
        await Assert.That(Enum.IsDefined(typeof(TempFileLocation), TempFileLocation.ProgramDirectory)).IsTrue();
        await Assert.That(Enum.IsDefined(typeof(TempFileLocation), TempFileLocation.ClipMateTemp)).IsTrue();
    }

    [Test]
    public async Task TempFileLocation_ShouldHaveCorrectValues()
    {
        // Assert - Verify enum values are defined
        await Assert.That(Enum.IsDefined(typeof(TempFileLocation), 0)).IsTrue();
        await Assert.That(Enum.IsDefined(typeof(TempFileLocation), 1)).IsTrue();
        await Assert.That(Enum.IsDefined(typeof(TempFileLocation), 2)).IsTrue();
        await Assert.That(Enum.IsDefined(typeof(TempFileLocation), 3)).IsTrue();
    }

    [Test]
    public async Task CleanupMethod_ShouldHaveAllExpectedValues()
    {
        // Assert
        await Assert.That(Enum.IsDefined(typeof(CleanupMethod), CleanupMethod.Never)).IsTrue();
        await Assert.That(Enum.IsDefined(typeof(CleanupMethod), CleanupMethod.Manual)).IsTrue();
        await Assert.That(Enum.IsDefined(typeof(CleanupMethod), CleanupMethod.AtStartup)).IsTrue();
        await Assert.That(Enum.IsDefined(typeof(CleanupMethod), CleanupMethod.AtShutdown)).IsTrue();
        await Assert.That(Enum.IsDefined(typeof(CleanupMethod), CleanupMethod.AfterHourIdle)).IsTrue();
    }

    [Test]
    public async Task CleanupMethod_ShouldHaveCorrectValues()
    {
        // Assert - Verify enum values are defined
        await Assert.That(Enum.IsDefined(typeof(CleanupMethod), 0)).IsTrue();
        await Assert.That(Enum.IsDefined(typeof(CleanupMethod), 1)).IsTrue();
        await Assert.That(Enum.IsDefined(typeof(CleanupMethod), 2)).IsTrue();
        await Assert.That(Enum.IsDefined(typeof(CleanupMethod), 3)).IsTrue();
        await Assert.That(Enum.IsDefined(typeof(CleanupMethod), 4)).IsTrue();
    }

    [Test]
    public async Task TempFileLocation_DefaultShouldBeDatabaseDirectory()
    {
        // Arrange
        var config = new DatabaseConfiguration();

        // Assert
        await Assert.That(config.TempFileLocation).IsEqualTo(TempFileLocation.DatabaseDirectory);
    }

    [Test]
    public async Task CleanupMethod_DefaultShouldBeAtStartup()
    {
        // Arrange
        var config = new DatabaseConfiguration();

        // Assert
        await Assert.That(config.CleanupMethod).IsEqualTo(CleanupMethod.AtStartup);
    }
}
