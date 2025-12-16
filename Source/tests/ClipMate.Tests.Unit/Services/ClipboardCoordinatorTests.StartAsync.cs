using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using Moq;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for ClipboardCoordinator StartAsync method.
/// </summary>
public partial class ClipboardCoordinatorTests
{
    [Test]
    public async Task StartAsync_ShouldStartClipboardMonitoring()
    {
        // Arrange
        var clipboardService = CreateMockClipboardService(out var channel);
        var configurationService = CreateMockConfigurationService();
        var serviceProvider = CreateMockServiceProvider();
        var messenger = new Mock<IMessenger>().Object;
        var soundService = CreateMockSoundService();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService.Object, configurationService.Object, serviceProvider, messenger, logger);

        // Act
        await coordinator.StartAsync(CancellationToken.None);

        // Give background task a moment to start
        await Task.Delay(100);

        // Cleanup
        channel.Writer.Complete();
        await coordinator.StopAsync(CancellationToken.None);

        // Assert
        clipboardService.Verify(p => p.StartMonitoringAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
