using ClipMate.Data.Services;
using Microsoft.EntityFrameworkCore;

namespace ClipMate.Tests.Unit.Services;

public partial class SqlMaintenanceServiceTests
{
    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task DisposeAsync_WithoutTransaction_DisposesSuccessfully()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.DisposeAsync();

        // Assert - Should not throw
        await Assert.That(service.HasActiveTransaction).IsFalse();
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task DisposeAsync_WithUncommittedTransaction_RollsBackAutomatically()
    {
        // Arrange
        var service = CreateService();
        await service.BeginTransactionAsync();

        // Act
        await service.DisposeAsync();

        // Assert - Should not throw, transaction rolled back
        await Assert.That(service.HasActiveTransaction).IsFalse();
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task DisposeAsync_WithCommittedTransaction_DisposesSuccessfully()
    {
        // Arrange
        var service = CreateService();
        await service.BeginTransactionAsync();
        await service.CommitTransactionAsync();

        // Act
        await service.DisposeAsync();

        // Assert
        await Assert.That(service.HasActiveTransaction).IsFalse();
        await Assert.That(service.IsTransactionCommitted).IsTrue();
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task DisposeAsync_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await service.DisposeAsync();
        await service.DisposeAsync();
        await service.DisposeAsync();

        // Should not throw - verify no exceptions occurred
        await Assert.That(service.HasActiveTransaction).IsFalse();
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task DisposeAsync_WithOwnsContextTrue_DisposesContext()
    {
        // Arrange
        var (dbContext, mockLogger, connection) = CreateMocks();
        var service = new SqlMaintenanceService(dbContext, mockLogger.Object, true);

        // Act
        await service.DisposeAsync();

        // Assert - Context should be disposed, further operations should throw
        await Assert.That(async () => await dbContext.Clips.ToListAsync())
            .Throws<ObjectDisposedException>();

        // Cleanup
        await connection.DisposeAsync();
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task DisposeAsync_WithOwnsContextFalse_DoesNotDisposeContext()
    {
        // Arrange
        var (dbContext, mockLogger, connection) = CreateMocks();
        var service = new SqlMaintenanceService(dbContext, mockLogger.Object, false);

        // Act
        await service.DisposeAsync();

        // Assert - Context should still be usable
        var clips = await dbContext.Clips.ToListAsync();
        await Assert.That(clips).IsNotNull();

        // Cleanup
        await dbContext.DisposeAsync();
        await connection.DisposeAsync();
    }
}
