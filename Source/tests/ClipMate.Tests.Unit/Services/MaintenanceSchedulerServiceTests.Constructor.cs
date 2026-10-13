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
                _mockMaintenanceService,
                _mockConfigService,
                _mockIdleDetector,
                _mockLogger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_ShouldThrow_WhenMaintenanceServiceNull()
    {
        // Act & Assert
        await Assert.That(() => new MaintenanceSchedulerService(
                _mockRetentionService,
                null!,
                _mockConfigService,
                _mockIdleDetector,
                _mockLogger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_ShouldThrow_WhenConfigServiceNull()
    {
        // Act & Assert
        await Assert.That(() => new MaintenanceSchedulerService(
                _mockRetentionService,
                _mockMaintenanceService,
                null!,
                _mockIdleDetector,
                _mockLogger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_ShouldThrow_WhenIdleDetectorNull()
    {
        // Act & Assert
        await Assert.That(() => new MaintenanceSchedulerService(
                _mockRetentionService,
                _mockMaintenanceService,
                _mockConfigService,
                null!,
                _mockLogger))
            .Throws<ArgumentNullException>();
    }

    [Test]
    [Category("Constructor")]
    public async Task Constructor_ShouldThrow_WhenLoggerNull()
    {
        // Act & Assert
        await Assert.That(() => new MaintenanceSchedulerService(
                _mockRetentionService,
                _mockMaintenanceService,
                _mockConfigService,
                _mockIdleDetector,
                null!))
            .Throws<ArgumentNullException>();
    }
}
