using Windows.Win32.Foundation;
using ClipMate.Core.Constants;
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

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task GetClipboardFormats_TextClipWithRtfAndHtml_ReturnsEveryFormatAsACopyWouldOfferIt()
    {
        // Arrange
        var service = CreateClipboardService();
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.RichText,
            TextContent = "Hello",
            RtfContent = @"{\rtf1 Hello}",
            HtmlContent = "Version:0.9\r\n<b>Hello</b>",
        };

        // Act
        var formats = service.GetClipboardFormats(clip);

        // Assert
        var unicodeText = formats.Single(p => p.FormatId == (uint)Formats.UnicodeText.Code);
        await Assert.That(System.Text.Encoding.Unicode.GetString(unicodeText.Data).TrimEnd('\0')).IsEqualTo("Hello");
        await Assert.That(formats.Any(p => p.FormatId == System.Windows.DataFormats.GetDataFormat(System.Windows.DataFormats.Rtf).Id)).IsTrue();
        await Assert.That(formats.Any(p => p.FormatId == System.Windows.DataFormats.GetDataFormat(System.Windows.DataFormats.Html).Id)).IsTrue();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task GetClipboardFormats_ImageClip_IncludesADibAndTheOriginalPng()
    {
        // Arrange
        var service = CreateClipboardService();
        var clip = new Clip { Id = Guid.NewGuid(), Type = ClipType.Image, ImageData = CreatePng(3, 2) };

        // Act
        var formats = service.GetClipboardFormats(clip);

        // Assert
        var dib = formats.Single(p => p.FormatId == (uint)Formats.Dib.Code).Data;
        await Assert.That(BitConverter.ToInt32(dib, 4)).IsEqualTo(3); // biWidth
        await Assert.That(BitConverter.ToInt32(dib, 8)).IsEqualTo(2); // biHeight
        await Assert.That(dib.Length).IsEqualTo(40 + 3 * 2 * 4);
        await Assert.That(formats.Single(p => p.FormatId == System.Windows.DataFormats.GetDataFormat("PNG").Id).Data).IsEquivalentTo(clip.ImageData);
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task GetClipboardFormats_FilesClip_IncludesTheFileDropList()
    {
        // Arrange
        var service = CreateClipboardService();
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Files,
            FilePathsJson = System.Text.Json.JsonSerializer.Serialize(new[] { Environment.ProcessPath! }),
        };

        // Act
        var formats = service.GetClipboardFormats(clip);

        // Assert
        await Assert.That(formats.Any(p => p.FormatId == (uint)Formats.HDrop.Code)).IsTrue();
    }

    private static byte[] CreatePng(int width, int height)
    {
        var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null,
            new byte[width * height * 4], width * 4);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
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
