namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Service lifecycle tests for MaintenanceSchedulerService (StartAsync, StopAsync, Dispose).
/// </summary>
public partial class MaintenanceSchedulerServiceTests
{
    [Test]
    [Category("Lifecycle")]
    public async Task StartAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert - Should complete without throwing
        await service.StartAsync(CancellationToken.None);

        // Cleanup
        service.Dispose();
    }

    [Test]
    [Category("Lifecycle")]
    public async Task StopAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        // Act & Assert - Should complete without throwing
        await service.StopAsync(CancellationToken.None);

        // Cleanup
        service.Dispose();
    }

    [Test]
    [Category("Lifecycle")]
    public async Task StopAsync_WithoutStart_ShouldCompleteSuccessfully()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert - Should handle stop without start
        await service.StopAsync(CancellationToken.None);

        // Cleanup
        service.Dispose();
    }

    [Test]
    [Category("Lifecycle")]
    public async Task Dispose_ShouldCompleteSuccessfully()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        // Act & Assert - Should dispose without throwing
        service.Dispose();
    }

    [Test]
    [Category("Lifecycle")]
    public async Task Dispose_CalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        // Act & Assert - Multiple dispose calls should be safe
        service.Dispose();
        service.Dispose();
        service.Dispose();
    }

    [Test]
    [Category("Lifecycle")]
    public Task Dispose_WithoutStart_ShouldCompleteSuccessfully()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert - Should dispose without start
        service.Dispose();
        return Task.CompletedTask;
    }
}
