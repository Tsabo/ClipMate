using ClipMate.Data.Services;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Constructor validation tests for MaintenanceSchedulerService.
/// </summary>
public partial class MaintenanceSchedulerServiceTests
{
    [Test]
    [Category("Constructor")]
    public async Task Constructor_ShouldThrow_WhenRetentionServiceNull()
    {
        // Act & Assert
        await Assert.That(() => new MaintenanceSchedulerService(
                null!,
                _mockMaintenanceService.Object,
                _mockConfigService.Object,
                _mockIdleDetector.Object,
                _mockLogger.Object))
            .Throws<ArgumentNullException>();
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_ShouldThrow_WhenMaintenanceServiceNull()
    {
        // Act & Assert
        await Assert.That(() => new MaintenanceSchedulerService(
                _mockRetentionService.Object,
                null!,
                _mockConfigService.Object,
                _mockIdleDetector.Object,
                _mockLogger.Object))
            .Throws<ArgumentNullException>();
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_ShouldThrow_WhenConfigServiceNull()
    {
        // Act & Assert
        await Assert.That(() => new MaintenanceSchedulerService(
                _mockRetentionService.Object,
                _mockMaintenanceService.Object,
                null!,
                _mockIdleDetector.Object,
                _mockLogger.Object))
            .Throws<ArgumentNullException>();
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_ShouldThrow_WhenIdleDetectorNull()
    {
        // Act & Assert
        await Assert.That(() => new MaintenanceSchedulerService(
                _mockRetentionService.Object,
                _mockMaintenanceService.Object,
                _mockConfigService.Object,
                null!,
                _mockLogger.Object))
            .Throws<ArgumentNullException>();
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_ShouldThrow_WhenLoggerNull()
    {
        // Act & Assert
        await Assert.That(() => new MaintenanceSchedulerService(
                _mockRetentionService.Object,
                _mockMaintenanceService.Object,
                _mockConfigService.Object,
                _mockIdleDetector.Object,
                null!))
            .Throws<ArgumentNullException>();
    }
}
