using ClipMate.Data.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

public partial class SqlMaintenanceServiceTests
{
    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SqlMaintenanceService>>();

        // Act & Assert
        await Assert.That(() => new SqlMaintenanceService(null!, mockLogger.Object))
            .Throws<ArgumentNullException>()
            .WithParameterName("dbContext");
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var (dbContext, _, connection) = CreateMocks();

        // Act & Assert
        await Assert.That(() => new SqlMaintenanceService(dbContext, null!))
            .Throws<ArgumentNullException>()
            .WithParameterName("logger");

        // Cleanup
        await connection.DisposeAsync();
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var (dbContext, mockLogger, connection) = CreateMocks();

        // Act
        var service = new SqlMaintenanceService(dbContext, mockLogger.Object);

        // Assert
        await Assert.That(service).IsNotNull();
        await Assert.That(service.HasActiveTransaction).IsFalse();
        await Assert.That(service.IsTransactionCommitted).IsFalse();

        // Cleanup
        await service.DisposeAsync();
        await connection.DisposeAsync();
    }
}
