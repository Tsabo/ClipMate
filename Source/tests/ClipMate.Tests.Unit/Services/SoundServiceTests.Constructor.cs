using ClipMate.Core.Services;
using ClipMate.Platform.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public class SoundServiceTests
{
    [Test]
    public async Task Constructor_WithNullConfigurationService_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = new Mock<ILogger<SoundService>>();

        // Act & Assert
        await Assert.That(() => new SoundService(null!, logger.Object))
            .Throws<ArgumentNullException>()
            .WithParameterName("configurationService");
    }

    [Test]
    public async Task Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var configService = new Mock<IConfigurationService>();

        // Act & Assert
        await Assert.That(() => new SoundService(configService.Object, null!))
            .Throws<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Test]
    public async Task Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var configService = new Mock<IConfigurationService>();
        var logger = new Mock<ILogger<SoundService>>();

        // Act
        var service = new SoundService(configService.Object, logger.Object);

        // Assert
        await Assert.That(service).IsNotNull();
    }
}
