namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for enabled/disabled state management in <see cref="IApplicationProfileService" />.
/// </summary>
[Category("ApplicationProfileService")]
[Category("EnabledState")]
public class ApplicationProfileServiceEnabledStateTests : ApplicationProfileServiceTestsBase
{
    [Test]
    public async Task IsApplicationProfilesEnabled_ReturnsTrue_ByDefault()
    {
        // Act
        var result = Service.IsApplicationProfilesEnabled();

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task SetApplicationProfilesEnabled_UpdatesEnabledState()
    {
        // Act
        Service.SetApplicationProfilesEnabled(false);
        var result = Service.IsApplicationProfilesEnabled();

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task SetApplicationProfilesEnabled_CanToggle()
    {
        // Act & Assert
        Service.SetApplicationProfilesEnabled(true);
        await Assert.That(Service.IsApplicationProfilesEnabled()).IsTrue();

        Service.SetApplicationProfilesEnabled(false);
        await Assert.That(Service.IsApplicationProfilesEnabled()).IsFalse();
    }
}
