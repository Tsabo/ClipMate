using ClipMate.Core.Services.Abstractions;
using ClipMate.Data;
using ClipMate.Data.Services;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;

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
        var contextFactory = A.Fake<IDatabaseContextFactory>();
        var autoAppendService = A.Fake<IAutoAppendService>();
        var serviceProvider = CreateMockServiceProvider();
        var messenger = A.Fake<IMessenger>();
        var logger = CreateLogger<ClipboardCoordinator>();
        var coordinator = new ClipboardCoordinator(clipboardService, configurationService,
            A.Fake<ICurrentClipWriter>(), contextFactory, autoAppendService, serviceProvider, messenger, logger);

        // Act
        await coordinator.StartAsync(CancellationToken.None);

        // Give background task a moment to start
        await Task.Delay(100);

        // Cleanup
        channel.Writer.Complete();
        await coordinator.StopAsync(CancellationToken.None);

        // Assert
        A.CallTo(() => clipboardService.StartMonitoringAsync(A<CancellationToken>.Ignored)).MustHaveHappenedOnceExactly();
    }
}
