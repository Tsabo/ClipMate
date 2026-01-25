using ClipMate.Data.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public partial class SetupServiceTests
{
    [Test]
    [Category("Constructor")]
    public async Task Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.That(() => new SetupService(null!))
            .ThrowsException()
            .WithMessageContaining("logger");
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SetupService>>();

        // Act
        var service = new SetupService(mockLogger.Object);

        // Assert
        await Assert.That(service).IsNotNull();
    }
}
