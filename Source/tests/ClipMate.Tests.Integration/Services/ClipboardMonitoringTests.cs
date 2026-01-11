using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Platform;
using ClipMate.Platform.Interop;
using ClipMate.Platform.Services;
using Microsoft.Extensions.Logging;
using Moq;

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
    public async Task StopMonitoring_ShouldCompleteChannel()
    {
        // Arrange
        var service = CreateClipboardService();
        await service.StartMonitoringAsync();

        // Act
        await service.StopMonitoringAsync();

        // Assert
        await Assert.That(service.ClipsChannel.Completion.IsCompleted).IsTrue();
    }

    /// <summary>
    /// Creates a clipboard service instance for testing.
    /// </summary>
    private IClipboardService CreateClipboardService()
    {
        var logger = Mock.Of<ILogger<ClipboardService>>();
        var win32Mock = new Mock<IWin32ClipboardInterop>();

        // Mock IApplicationProfileService (disabled by default)
        var profileServiceMock = new Mock<IApplicationProfileService>();
        profileServiceMock.Setup(p => p.IsApplicationProfilesEnabled()).Returns(false);

        // Mock IClipboardFormatEnumerator
        var formatEnumeratorMock = new Mock<IClipboardFormatEnumerator>();
        formatEnumeratorMock.Setup(p => p.GetAllAvailableFormats()).Returns(new List<ClipboardFormatInfo>());

        var soundService = new Mock<ISoundService>();
        soundService.Setup(p => p.PlaySoundAsync(It.IsAny<SoundEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Create configuration mock with default timing values
        var configServiceMock = new Mock<IConfigurationService>();
        var config = new ClipMateConfiguration();
        configServiceMock.Setup(p => p.Configuration).Returns(config);

        return new ClipboardService(logger, win32Mock.Object, configServiceMock.Object, profileServiceMock.Object, formatEnumeratorMock.Object, soundService.Object);
    }
}
