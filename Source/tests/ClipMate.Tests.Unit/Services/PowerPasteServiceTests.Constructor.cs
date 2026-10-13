using ClipMate.Core.Services;
using ClipMate.Data.Services;

namespace ClipMate.Tests.Unit.Services;

public partial class PowerPasteServiceTests
{
    [Test]
    [Category("Constructor")]
    public async Task Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var service = CreateService();

        // Assert
        await Assert.That(service).IsNotNull();
        await Assert.That(service.State).IsEqualTo(PowerPasteState.Inactive);
        await Assert.That(service.CurrentPosition).IsEqualTo(-1);
        await Assert.That(service.TotalCount).IsEqualTo(0);
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_WithNullConfigService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new PowerPasteService(
            null!,
            _mockSoundService,
            _mockLogger));

        await Assert.That(exception.ParamName).IsEqualTo("configurationService");
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_WithNullSoundService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new PowerPasteService(
            _mockConfigService,
            null!,
            _mockLogger));

        await Assert.That(exception.ParamName).IsEqualTo("soundService");
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new PowerPasteService(
            _mockConfigService,
            _mockSoundService,
            null!));

        await Assert.That(exception.ParamName).IsEqualTo("logger");
    }
}
