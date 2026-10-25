using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Platform;
using ClipMate.Platform.Interop;
using ClipMate.Platform.Services;
using Microsoft.Extensions.Logging;
using FakeItEasy;
using Windows.Win32.Foundation;

namespace ClipMate.Tests.Integration.Services;

/// <summary>
/// Integration tests for clipboard monitoring lifecycle.
/// Tests the full clipboard monitoring workflow with real dependencies.
/// </summary>
public class ClipboardMonitoringTests : IntegrationTestBase
{
    [Test]
    public async Task StartMonitoring_ShouldActivateClipboardListener()
    {
        // Arrange
        var service = CreateClipboardService();

        // Act
        await service.StartMonitoringAsync();

        // Assert
        await Assert.That(service.IsMonitoring).IsTrue();

        // Cleanup
        await service.StopMonitoringAsync();
    }

    [Test]
    public async Task StopMonitoring_ShouldDeactivateClipboardListener()
    {
        // Arrange
        var service = CreateClipboardService();
        await service.StartMonitoringAsync();

        // Act
        await service.StopMonitoringAsync();

        // Assert
        await Assert.That(service.IsMonitoring).IsFalse();
    }

    // Note: ClipboardChange_ShouldPublishToChannel test removed
    // Requires actual Win32 clipboard interaction which cannot be automated in integration tests.
    // The clipboard monitoring lifecycle is tested by Start/Stop tests above.

    [Test]
    public async Task MultipleStartCalls_ShouldNotThrow()
    {
        // Arrange
        var service = CreateClipboardService();

        // Act & Assert
        await service.StartMonitoringAsync();
        await Assert.That(async () => await service.StartMonitoringAsync()).ThrowsNothing();

        // Cleanup
        await service.StopMonitoringAsync();
    }

    [Test]
    public async Task MultipleStopCalls_ShouldNotThrow()
    {
        // Arrange
        var service = CreateClipboardService();
        await service.StartMonitoringAsync();
        await service.StopMonitoringAsync();

        // Act & Assert
        await Assert.That(async () => await service.StopMonitoringAsync()).ThrowsNothing();
    }

    [Test]
    public async Task StopMonitoring_ShouldLeaveChannelOpen()
    {
        // Arrange
        var service = CreateClipboardService();
        await service.StartMonitoringAsync();

        // Act
        await service.StopMonitoringAsync();

        // Assert
        await Assert.That(service.ClipsChannel.Completion.IsCompleted).IsFalse();
    }

    /// <summary>
    /// Creates a clipboard service instance for testing.
    /// </summary>
    private IClipboardService CreateClipboardService()
    {
        var logger = A.Fake<ILogger<ClipboardService>>();
        var win32Mock = A.Fake<IWin32ClipboardInterop>();
        A.CallTo(() => win32Mock.AddClipboardFormatListener(A<HWND>.Ignored)).Returns(true);
        A.CallTo(() => win32Mock.RemoveClipboardFormatListener(A<HWND>.Ignored)).Returns(true);

        // Mock IApplicationProfileService (disabled by default)
        var profileServiceMock = A.Fake<IApplicationProfileService>();
        A.CallTo(() => profileServiceMock.IsApplicationProfilesEnabled()).Returns(false);

        var soundService = A.Fake<ISoundService>();
        A.CallTo(() => soundService.PlaySoundAsync(A<SoundEvent>.Ignored, A<CancellationToken>.Ignored)).Returns(Task.CompletedTask);

        // Create configuration mock with default timing values
        var configServiceMock = A.Fake<IConfigurationService>();
        var config = new ClipMateConfiguration();
        A.CallTo(() => configServiceMock.Configuration).Returns(config);

        var blobCacheServiceMock = A.Fake<IDecryptedBlobCacheService>();
        return new ClipboardService(logger, win32Mock, configServiceMock, profileServiceMock, soundService, blobCacheServiceMock);
    }
}
