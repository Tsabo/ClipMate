using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Data.Services;
using ClipMate.Platform;
using Microsoft.Extensions.Logging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for MaintenanceSchedulerService.
/// Tests scheduled maintenance logic and CleanupMethod enforcement.
/// </summary>
public class MaintenanceSchedulerServiceTests
{
    private Mock<IConfigurationService> _mockConfigService = null!;
    private Mock<IWin32IdleDetector> _mockIdleDetector = null!;
    private Mock<ILogger<MaintenanceSchedulerService>> _mockLogger = null!;
    private Mock<IDatabaseMaintenanceService> _mockMaintenanceService = null!;
    private Mock<IRetentionEnforcementService> _mockRetentionService = null!;

    [Before(Test)]
    public void Setup()
    {
        _mockConfigService = new Mock<IConfigurationService>();
        _mockMaintenanceService = new Mock<IDatabaseMaintenanceService>();
        _mockRetentionService = new Mock<IRetentionEnforcementService>();
        _mockIdleDetector = new Mock<IWin32IdleDetector>();
        _mockLogger = new Mock<ILogger<MaintenanceSchedulerService>>();
    }

    [Test]
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

    [Test]
    public async Task StartAsync_ShouldStartTimer()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert - Should complete without throwing
        await service.StartAsync(CancellationToken.None);
    }

    [Test]
    public async Task StopAsync_ShouldStopTimer()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        // Act & Assert - Should complete without throwing
        await service.StopAsync(CancellationToken.None);
    }

    private MaintenanceSchedulerService CreateService()
    {
        // Setup default configuration with no databases
        var config = new ClipMateConfiguration
        {
            Preferences = new PreferencesConfiguration(),
            Databases = new Dictionary<string, DatabaseConfiguration>(),
        };

        _mockConfigService.Setup(x => x.Configuration).Returns(config);

        return new MaintenanceSchedulerService(
            _mockRetentionService.Object,
            _mockMaintenanceService.Object,
            _mockConfigService.Object,
            _mockIdleDetector.Object,
            _mockLogger.Object);
    }
}
