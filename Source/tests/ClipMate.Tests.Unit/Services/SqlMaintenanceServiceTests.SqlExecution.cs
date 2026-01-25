namespace ClipMate.Tests.Unit.Services;

public partial class SqlMaintenanceServiceTests
{
    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task ExecuteQueryAsync_WithNullSql_ThrowsArgumentException()
    {
        // Arrange
        await using var service = CreateService();

        // Act & Assert
        await Assert.That(async () => await service.ExecuteQueryAsync(null!))
            .Throws<ArgumentException>();
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task ExecuteQueryAsync_WithEmptySql_ThrowsArgumentException()
    {
        // Arrange
        await using var service = CreateService();

        // Act & Assert
        await Assert.That(async () => await service.ExecuteQueryAsync(string.Empty))
            .Throws<ArgumentException>();
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task ExecuteQueryAsync_WithWhitespaceSql_ThrowsArgumentException()
    {
        // Arrange
        await using var service = CreateService();

        // Act & Assert
        await Assert.That(async () => await service.ExecuteQueryAsync("   "))
            .Throws<ArgumentException>();
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task ExecuteQueryAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var service = CreateService();
        await service.DisposeAsync();

        // Act & Assert
        await Assert.That(async () => await service.ExecuteQueryAsync("SELECT 1"))
            .Throws<ObjectDisposedException>();
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task ExecuteNonQueryAsync_WithNullSql_ThrowsArgumentException()
    {
        // Arrange
        await using var service = CreateService();

        // Act & Assert
        await Assert.That(async () => await service.ExecuteNonQueryAsync(null!))
            .Throws<ArgumentException>();
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task ExecuteNonQueryAsync_WithEmptySql_ThrowsArgumentException()
    {
        // Arrange
        await using var service = CreateService();

        // Act & Assert
        await Assert.That(async () => await service.ExecuteNonQueryAsync(string.Empty))
            .Throws<ArgumentException>();
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task ExecuteNonQueryAsync_WithWhitespaceSql_ThrowsArgumentException()
    {
        // Arrange
        await using var service = CreateService();

        // Act & Assert
        await Assert.That(async () => await service.ExecuteNonQueryAsync("   "))
            .Throws<ArgumentException>();
    }

    [Test]
    [Category("Unit")]
    [Category("SqlMaintenanceService")]
    public async Task ExecuteNonQueryAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var service = CreateService();
        await service.DisposeAsync();

        // Act & Assert
        await Assert.That(async () => await service.ExecuteNonQueryAsync("SELECT 1"))
            .Throws<ObjectDisposedException>();
    }
}
