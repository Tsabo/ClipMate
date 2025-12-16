using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for ClipboardCoordinator StopAsync method.
/// </summary>
public partial class ClipboardCoordinatorTests
{
    [Test]
    public async Task StopAsync_ShouldStopClipboardMonitoring()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var channel);
        var configurationService = CreateMockConfigurationService();
        var serviceProvider = CreateMockServiceProvider();
        var messenger = new Mock<IMessenger>().Object;
        var soundService = CreateMockSoundService();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService.Object, configurationService.Object, serviceProvider, messenger, logger);

        await coordinator.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Let background task start

        // Act
        channel.Writer.Complete();
        await coordinator.StopAsync(CancellationToken.None);

        // Assert
        clipboardService.Verify(p => p.StopMonitoringAsync(), Times.Once);
    }
}
