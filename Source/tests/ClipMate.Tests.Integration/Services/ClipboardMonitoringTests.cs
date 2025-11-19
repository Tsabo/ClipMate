using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Platform.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for clipboard monitoring lifecycle.
/// Tests the full clipboard monitoring workflow with real dependencies.
/// </summary>
public class ClipboardMonitoringTests : IntegrationTestBase
{
    [StaFact]
    public async Task StartMonitoring_ShouldActivateClipboardListener()
    {
        // Arrange
        var service = CreateClipboardService();

        // Act
        await service.StartMonitoringAsync();

        // Assert
        service.IsMonitoring.ShouldBeTrue();

        // Cleanup
        await service.StopMonitoringAsync();
    }

    [StaFact]
    public async Task StopMonitoring_ShouldDeactivateClipboardListener()
    {
        // Arrange
        var service = CreateClipboardService();
        await service.StartMonitoringAsync();

        // Act
        await service.StopMonitoringAsync();

        // Assert
        service.IsMonitoring.ShouldBeFalse();
    }

    [StaFact(Skip = "Requires actual clipboard interaction - cannot be automated without Win32 clipboard simulation")]
    public async Task ClipboardChange_ShouldPublishToChannel()
    {
        // Arrange
        var service = CreateClipboardService();
        Clip? capturedClip = null;

        // Act
        await service.StartMonitoringAsync();
        
        // Try to read from channel with timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            capturedClip = await service.ClipsChannel.ReadAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Timeout - OK for this test since we're not actually changing clipboard
        }
        
        await service.StopMonitoringAsync();

        // Assert
        // Note: This test requires actual clipboard interaction
        // capturedClip.ShouldNotBeNull();
    }

    [StaFact]
    public async Task MultipleStartCalls_ShouldNotThrow()
    {
        // Arrange
        var service = CreateClipboardService();

        // Act & Assert
        await service.StartMonitoringAsync();
        await Should.NotThrowAsync(async () => await service.StartMonitoringAsync());

        // Cleanup
        await service.StopMonitoringAsync();
    }

    [StaFact]
    public async Task MultipleStopCalls_ShouldNotThrow()
    {
        // Arrange
        var service = CreateClipboardService();
        await service.StartMonitoringAsync();
        await service.StopMonitoringAsync();

        // Act & Assert
        await Should.NotThrowAsync(async () => await service.StopMonitoringAsync());
    }

    [StaFact]
    public async Task StopMonitoring_ShouldCompleteChannel()
    {
        // Arrange
        var service = CreateClipboardService();
        await service.StartMonitoringAsync();

        // Act
        await service.StopMonitoringAsync();

        // Assert
        service.ClipsChannel.Completion.IsCompleted.ShouldBeTrue();
    }

    /// <summary>
    /// Creates a clipboard service instance for testing.
    /// </summary>
    private IClipboardService CreateClipboardService()
    {
        var logger = Mock.Of<ILogger<ClipboardService>>();
        return new ClipboardService(logger);
    }
}
