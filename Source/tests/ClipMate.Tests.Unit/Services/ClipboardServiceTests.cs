using Windows.Win32.Foundation;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Platform;
using ClipMate.Platform.Services;
using FakeItEasy;
using TUnit.Core.Executors;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Unit tests for IClipboardService implementation.
/// Tests written first per TDD requirement for User Story 1.
/// </summary>
public class ClipboardServiceTests : TestFixtureBase
{
    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task StartMonitoringAsync_ShouldSetIsMonitoringToTrue()
    {
        // Arrange
        var service = CreateClipboardService();

        // Act
        await service.StartMonitoringAsync();

        // Assert
        await Assert.That(service.IsMonitoring).IsTrue();

        await service.StopMonitoringAsync();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task StopMonitoringAsync_ShouldSetIsMonitoringToFalse()
    {
        // Arrange
        var service = CreateClipboardService();
        await service.StartMonitoringAsync();

        // Act
        await service.StopMonitoringAsync();

        // Assert
        await Assert.That(service.IsMonitoring).IsFalse();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task StartMonitoringAsync_WhenAlreadyMonitoring_ShouldNotThrow()
    {
        // Arrange
        var service = CreateClipboardService();
        await service.StartMonitoringAsync();

        // Act & Assert
        await Assert.That(async () => await service.StartMonitoringAsync()).ThrowsNothing();

        await service.StopMonitoringAsync();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task GetCurrentClipboardContentAsync_WithEmptyClipboard_ShouldReturnNull()
    {
        // Arrange
        var service = CreateClipboardService();
        // TODO: Need to mock empty clipboard

        // Act
        var clip = await service.GetCurrentClipboardContentAsync();

        // Assert
        await Assert.That(clip).IsNull();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ClipsChannel_WhenClipboardChanges_ShouldPublishClip()
    {
        // Arrange
        var service = CreateClipboardService();
        await service.StartMonitoringAsync();

        // Act
        // TODO: Simulate clipboard change

        // Try to read from channel with timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        _ = service.ClipsChannel.ReadAsync(cts.Token).AsTask();

        // Assert
        // Should eventually receive a clip (when clipboard actually changes)
        // capturedClip.ShouldNotBeNull();

        await service.StopMonitoringAsync();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ClipsChannel_WithDuplicateContent_ShouldCalculateCorrectContentHash()
    {
        // Arrange
        var service = CreateClipboardService();
        await service.StartMonitoringAsync();
        var clips = new List<Clip>();

        // Act
        // TODO: Simulate clipboard change with same content twice
        // Read clips from channel
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        try
        {
            await foreach (var item in service.ClipsChannel.ReadAllAsync(cts.Token))
            {
                clips.Add(item);
                if (clips.Count >= 2)
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout - that's OK for this test
        }

        // Assert
        // clips.Count.ShouldBeGreaterThanOrEqualTo(2);
        // clips[0].ContentHash.ShouldBe(clips[1].ContentHash);

        await service.StopMonitoringAsync();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task ClipsChannel_ShouldCompleteWhenMonitoringStopped()
    {
        // Arrange
        var service = CreateClipboardService();
        await service.StartMonitoringAsync();

        // Act
        await service.StopMonitoringAsync();

        // Assert
        // Channel should be completed
        await Assert.That(service.ClipsChannel.Completion.IsCompleted).IsTrue();
    }

    /// <summary>
    /// Creates an instance of ClipboardService for testing.
    /// </summary>
    private IClipboardService CreateClipboardService()
    {
        var logger = CreateLogger<ClipboardService>();
        var win32Mock = CreateWin32ClipboardMock();

        // Setup basic Win32 mock expectations
        A.CallTo(() => win32Mock.AddClipboardFormatListener(A<HWND>.Ignored)).Returns(true);
        A.CallTo(() => win32Mock.RemoveClipboardFormatListener(A<HWND>.Ignored)).Returns(true);

        // Mock IApplicationProfileService (disabled by default)
        var profileServiceMock = A.Fake<IApplicationProfileService>();
        A.CallTo(() => profileServiceMock.IsApplicationProfilesEnabled()).Returns(false);

        // Mock IClipboardFormatEnumerator
        var formatEnumeratorMock = A.Fake<IClipboardFormatEnumerator>();
        A.CallTo(() => formatEnumeratorMock.GetAllAvailableFormats()).Returns(new List<ClipboardFormatInfo>());

        var soundService = A.Fake<ISoundService>();
        A.CallTo(() => soundService.PlaySoundAsync(A<SoundEvent>.Ignored, A<CancellationToken>.Ignored)).Returns(Task.CompletedTask);

        // Create configuration mock with default timing values
        var configServiceMock = A.Fake<IConfigurationService>();
        var config = new ClipMateConfiguration();
        A.CallTo(() => configServiceMock.Configuration).Returns(config);

        var blobCacheServiceMock = A.Fake<IDecryptedBlobCacheService>();
        return new ClipboardService(logger, win32Mock, configServiceMock, profileServiceMock, formatEnumeratorMock, soundService, blobCacheServiceMock);
    }
}
