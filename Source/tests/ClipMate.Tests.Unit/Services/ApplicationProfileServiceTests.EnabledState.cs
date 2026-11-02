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
    public async Task SetApplicationProfilesEnabled_IsKeptInTheSavedSettings()
    {
        // Act: the Options checkbox, saved with the rest of the configuration
        Service.SetApplicationProfilesEnabled(false);

        // Assert
        await Assert.That(Configuration.Preferences.ApplicationProfilesEnabled).IsFalse();
    }

    [Test]
    public async Task IsApplicationProfilesEnabled_ReadsTheSavedSetting()
    {
        // Arrange: switched off in an earlier session
        Configuration.Preferences.ApplicationProfilesEnabled = false;

        // Act & Assert
        await Assert.That(Service.IsApplicationProfilesEnabled()).IsFalse();
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
