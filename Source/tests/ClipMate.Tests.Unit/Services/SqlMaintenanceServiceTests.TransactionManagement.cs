namespace ClipMate.Tests.Unit.Services;

public partial class SqlMaintenanceServiceTests
{
    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task BeginTransactionAsync_StartsTransaction()
    {
        // Arrange
        await using var service = CreateService();

        // Act
        await service.BeginTransactionAsync();

        // Assert
        await Assert.That(service.HasActiveTransaction).IsTrue();
        await Assert.That(service.IsTransactionCommitted).IsFalse();
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task BeginTransactionAsync_WhenAlreadyActive_ThrowsInvalidOperationException()
    {
        // Arrange
        await using var service = CreateService();
        await service.BeginTransactionAsync();

        // Act & Assert
        await Assert.That(async () => await service.BeginTransactionAsync())
            .Throws<InvalidOperationException>()
            .WithMessage("A transaction is already active. Commit or rollback before starting a new one.");
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task BeginTransactionAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var service = CreateService();
        await service.DisposeAsync();

        // Act & Assert
        await Assert.That(async () => await service.BeginTransactionAsync())
            .Throws<ObjectDisposedException>();
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task CommitTransactionAsync_WithActiveTransaction_CommitsSuccessfully()
    {
        // Arrange
        await using var service = CreateService();
        await service.BeginTransactionAsync();

        // Act
        await service.CommitTransactionAsync();

        // Assert
        await Assert.That(service.HasActiveTransaction).IsFalse();
        await Assert.That(service.IsTransactionCommitted).IsTrue();
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task CommitTransactionAsync_WithoutActiveTransaction_ThrowsInvalidOperationException()
    {
        // Arrange
        await using var service = CreateService();

        // Act & Assert
        await Assert.That(async () => await service.CommitTransactionAsync())
            .Throws<InvalidOperationException>()
            .WithMessage("No active transaction to commit.");
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task CommitTransactionAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var service = CreateService();
        await service.DisposeAsync();

        // Act & Assert
        await Assert.That(async () => await service.CommitTransactionAsync())
            .Throws<ObjectDisposedException>();
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task RollbackTransactionAsync_WithActiveTransaction_RollsBackSuccessfully()
    {
        // Arrange
        await using var service = CreateService();
        await service.BeginTransactionAsync();

        // Act
        await service.RollbackTransactionAsync();

        // Assert
        await Assert.That(service.HasActiveTransaction).IsFalse();
        await Assert.That(service.IsTransactionCommitted).IsFalse();
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task RollbackTransactionAsync_WithoutActiveTransaction_ThrowsInvalidOperationException()
    {
        // Arrange
        await using var service = CreateService();

        // Act & Assert
        await Assert.That(async () => await service.RollbackTransactionAsync())
            .Throws<InvalidOperationException>()
            .WithMessage("No active transaction to rollback.");
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task RollbackTransactionAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var service = CreateService();
        await service.DisposeAsync();

        // Act & Assert
        await Assert.That(async () => await service.RollbackTransactionAsync())
            .Throws<ObjectDisposedException>();
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task TransactionLifecycle_BeginCommitBegin_WorksCorrectly()
    {
        // Arrange
        await using var service = CreateService();

        // Act & Assert - First transaction
        await service.BeginTransactionAsync();
        await Assert.That(service.HasActiveTransaction).IsTrue();
        await Assert.That(service.IsTransactionCommitted).IsFalse();

        await service.CommitTransactionAsync();
        await Assert.That(service.HasActiveTransaction).IsFalse();
        await Assert.That(service.IsTransactionCommitted).IsTrue();

        // Act & Assert - Second transaction
        await service.BeginTransactionAsync();
        await Assert.That(service.HasActiveTransaction).IsTrue();
        await Assert.That(service.IsTransactionCommitted).IsFalse();
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task TransactionLifecycle_BeginRollbackBegin_WorksCorrectly()
    {
        // Arrange
        await using var service = CreateService();

        // Act & Assert - First transaction
        await service.BeginTransactionAsync();
        await Assert.That(service.HasActiveTransaction).IsTrue();

        await service.RollbackTransactionAsync();
        await Assert.That(service.HasActiveTransaction).IsFalse();
        await Assert.That(service.IsTransactionCommitted).IsFalse();

        // Act & Assert - Second transaction
        await service.BeginTransactionAsync();
        await Assert.That(service.HasActiveTransaction).IsTrue();
    }
}
