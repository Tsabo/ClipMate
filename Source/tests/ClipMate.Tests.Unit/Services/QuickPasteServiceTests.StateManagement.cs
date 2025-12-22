using ClipMate.Core.Models.Configuration;

namespace ClipMate.Tests.Unit.Services;

public partial class QuickPasteServiceTests
{
    [Test]
    public async Task GetCurrentTarget_ReturnsNull_WhenNoTargetSet()
    {
        // Arrange
        var service = CreateService();

        // Act
        var target = service.GetCurrentTarget();

        // Assert
        await Assert.That(target).IsNull();
    }

    [Test]
    public async Task SetTargetLock_SetsLockState()
    {
        // Arrange
        var service = CreateService();

        // Act
        service.SetTargetLock(true);

        // Assert
        await Assert.That(service.IsTargetLocked()).IsTrue();
    }

    [Test]
    public async Task IsTargetLocked_ReturnsFalse_Initially()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Assert.That(service.IsTargetLocked()).IsFalse();
    }

    [Test]
    public async Task SetTargetLock_CanBeToggledOff()
    {
        // Arrange
        var service = CreateService();
        service.SetTargetLock(true);

        // Act
        service.SetTargetLock(false);

        // Assert
        await Assert.That(service.IsTargetLocked()).IsFalse();
    }

    [Test]
    public async Task GetGoBackState_ReturnsTrue_Initially()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert - Default value is false
        await Assert.That(service.GetGoBackState()).IsFalse();
    }

    [Test]
    public async Task SetGoBackState_UpdatesState()
    {
        // Arrange
        var service = CreateService();

        // Act
        service.SetGoBackState(false);

        // Assert
        await Assert.That(service.GetGoBackState()).IsFalse();
    }

    [Test]
    public async Task SetGoBackState_CanBeToggledOn()
    {
        // Arrange
        var service = CreateService();
        service.SetGoBackState(false);

        // Act
        service.SetGoBackState(true);

        // Assert
        await Assert.That(service.GetGoBackState()).IsTrue();
    }

    [Test]
    public async Task GetSelectedFormattingString_ReturnsDefaultFormattingString()
    {
        // Arrange
        var service = CreateService();

        // Act
        var format = service.GetSelectedFormattingString();

        // Assert
        await Assert.That(format).IsNotNull();
        await Assert.That(format!.TitleTrigger).IsEqualTo("*");
        await Assert.That(format.Title).IsEqualTo("Default - Ctrl+V");
    }

    [Test]
    public async Task SelectFormattingString_UpdatesSelectedFormat()
    {
        // Arrange
        var service = CreateService();
        var newFormat = new QuickPasteFormattingString
        {
            Title = "Custom Format",
            Preamble = "Test",
            PasteKeystrokes = "^v",
            Postamble = "",
            TitleTrigger = "",
        };

        // Act
        service.SelectFormattingString(newFormat);

        // Assert
        var selected = service.GetSelectedFormattingString();
        await Assert.That(selected).IsEqualTo(newFormat);
        await Assert.That(selected!.Title).IsEqualTo("Custom Format");
    }

    [Test]
    public async Task SelectFormattingString_CanSetToNull()
    {
        // Arrange
        var service = CreateService();

        // Act
        service.SelectFormattingString(null);

        // Assert
        await Assert.That(service.GetSelectedFormattingString()).IsNull();
    }

    [Test]
    public async Task GetCurrentTargetString_ReturnsEmpty_WhenNoTargetSet()
    {
        // Arrange
        var service = CreateService();

        // Act
        var targetString = service.GetCurrentTargetString();

        // Assert
        await Assert.That(targetString).IsEqualTo(string.Empty);
    }
}
