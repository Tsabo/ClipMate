using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;

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
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        var autoAppendService = A.Fake<IAutoAppendService>();
        var serviceProvider = CreateMockServiceProvider();
        var messenger = A.Fake<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService, configurationService, contextFactory, autoAppendService, serviceProvider, messenger, logger);

        await coordinator.StartAsync(CancellationToken.None);
        await Task.Delay(100); // Let background task start

        // Act
        channel.Writer.Complete();
        await coordinator.StopAsync(CancellationToken.None);

        // Assert
        A.CallTo(() => clipboardService.StopMonitoringAsync()).MustHaveHappenedOnceExactly();
    }
}
