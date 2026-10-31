using ClipMate.Core.Services.Abstractions;
using ClipMate.Platform.Services;
using Microsoft.Extensions.Logging;
using FakeItEasy;

namespace ClipMate.Tests.Unit.Services;

public class SoundServiceTests
{
    [Test]
    public async Task Constructor_WithNullConfigurationService_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = A.Fake<ILogger<SoundService>>();

        // Act & Assert
        await Assert.That(() => new SoundService(null!, logger))
            .Throws<ArgumentNullException>()
            .WithParameterName("configurationService");
    }

    [Test]
    public async Task Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var configService = A.Fake<IConfigurationService>();

        // Act & Assert
        await Assert.That(() => new SoundService(configService, null!))
            .Throws<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Test]
    public async Task Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var configService = A.Fake<IConfigurationService>();
        var logger = A.Fake<ILogger<SoundService>>();

        // Act
        var service = new SoundService(configService, logger);

        // Assert
        await Assert.That(service).IsNotNull();
    }
}
