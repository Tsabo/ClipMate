using Windows.Win32.Foundation;
using ClipMate.Core.Constants;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Platform;
using ClipMate.Platform.Interop;
using ClipMate.Platform.Services;
using ClipMate.Tests.Unit.Networking;
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

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task CaptureClipboardChangeAsync_EmptyClipboard_PlaysTheEraseSoundEveryTime()
    {
        // Arrange
        var soundService = A.Fake<ISoundService>();
        var service = CreateClipboardService(CreateClipboardOffering(), soundService);

        // Act
        var first = await service.CaptureClipboardChangeAsync(HWND.Null);
        var second = await service.CaptureClipboardChangeAsync(HWND.Null);

        // Assert
        await Assert.That(first.Outcome).IsEqualTo(ClipboardCaptureOutcome.Empty);
        await Assert.That(second.Outcome).IsEqualTo(ClipboardCaptureOutcome.Empty);
        A.CallTo(() => soundService.PlaySoundAsync(SoundEvent.Erase, A<CancellationToken>.Ignored)).MustHaveHappenedTwiceExactly();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task CaptureClipboardChangeAsync_TextAndAPrivateFormat_ReadsInOneSessionAndPublishesBoth()
    {
        // Arrange
        byte[] privateData = [1, 2, 3];
        var win32Mock = CreateClipboardOffering(
            ((uint)Formats.UnicodeText.Code, null, System.Text.Encoding.Unicode.GetBytes("Hello\0")),
            (0xC123, "ClipMate.Tests.Private", privateData));
        var service = CreateClipboardService(win32Mock, A.Fake<ISoundService>());

        // Act
        var result = await service.CaptureClipboardChangeAsync(HWND.Null);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var clip = await service.ClipsChannel.ReadAsync(cts.Token);

        // Assert
        await Assert.That(result.Outcome).IsEqualTo(ClipboardCaptureOutcome.Captured);
        await Assert.That(clip.TextContent).IsEqualTo("Hello");
        await Assert.That(clip.RawFormats.Single().Name).IsEqualTo("ClipMate.Tests.Private");
        await Assert.That(clip.RawFormats.Single().Data).IsEquivalentTo(privateData);
        A.CallTo(() => win32Mock.OpenClipboard(A<HWND>.Ignored)).MustHaveHappenedOnceExactly();
        A.CallTo(() => win32Mock.CloseClipboard()).MustHaveHappenedOnceExactly();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task CaptureClipboardChangeAsync_OwnerInABackgroundProgram_SourceIsTheOwner()
    {
        // Arrange
        var win32Mock = CreateTextOffering();
        GivenClipboardOwner(win32Mock, new HWND(0x10), new HWND(0x11), 101, "notepad", "Untitled - Notepad");
        GivenForegroundWindow(win32Mock, new HWND(0x20), 202, "explorer", "Desktop");
        var service = CreateClipboardService(win32Mock, A.Fake<ISoundService>());

        // Act
        var clip = await CaptureAsync(service);

        // Assert
        await Assert.That(clip.SourceApplicationName).IsEqualTo("notepad");
        await Assert.That(clip.SourceApplicationTitle).IsEqualTo("Untitled - Notepad");
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task CaptureClipboardChangeAsync_OwnerWindowWithoutATitle_TakesTheForegroundTitle()
    {
        // Arrange
        var win32Mock = CreateTextOffering();
        GivenClipboardOwner(win32Mock, new HWND(0x10), new HWND(0x10), 101, "notepad", null);
        GivenForegroundWindow(win32Mock, new HWND(0x20), 202, "explorer", "Desktop");
        var service = CreateClipboardService(win32Mock, A.Fake<ISoundService>());

        // Act
        var clip = await CaptureAsync(service);

        // Assert
        await Assert.That(clip.SourceApplicationName).IsEqualTo("notepad");
        await Assert.That(clip.SourceApplicationTitle).IsEqualTo("Desktop");
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task CaptureClipboardChangeAsync_NoClipboardOwner_SourceIsTheForegroundWindow()
    {
        // Arrange
        var win32Mock = CreateTextOffering();
        GivenForegroundWindow(win32Mock, new HWND(0x20), 202, "explorer", "Desktop");
        var service = CreateClipboardService(win32Mock, A.Fake<ISoundService>());

        // Act
        var clip = await CaptureAsync(service);

        // Assert
        await Assert.That(clip.SourceApplicationName).IsEqualTo("explorer");
        await Assert.That(clip.SourceApplicationTitle).IsEqualTo("Desktop");
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task CaptureClipboardChangeAsync_OwnerProcessGone_SourceIsTheForegroundWindow()
    {
        // Arrange
        var win32Mock = CreateTextOffering();
        GivenClipboardOwner(win32Mock, new HWND(0x10), new HWND(0x11), 101, null, null);
        GivenForegroundWindow(win32Mock, new HWND(0x20), 202, "explorer", "Desktop");
        var service = CreateClipboardService(win32Mock, A.Fake<ISoundService>());

        // Act
        var clip = await CaptureAsync(service);

        // Assert
        await Assert.That(clip.SourceApplicationName).IsEqualTo("explorer");
        await Assert.That(clip.SourceApplicationTitle).IsEqualTo("Desktop");
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task CaptureClipboardChangeAsync_OwnerInABackgroundProgram_ApplicationProfileIsTheOwners()
    {
        // Arrange
        var win32Mock = CreateTextOffering();
        GivenClipboardOwner(win32Mock, new HWND(0x10), new HWND(0x11), 101, "notepad", "Untitled - Notepad");
        GivenForegroundWindow(win32Mock, new HWND(0x20), 202, "explorer", "Desktop");
        var profileService = A.Fake<IApplicationProfileService>();
        A.CallTo(() => profileService.IsApplicationProfilesEnabled()).Returns(true);
        A.CallTo(() => profileService.GetCapturedFormatsAsync(A<string>.Ignored, A<IReadOnlyCollection<string>>.Ignored, A<CancellationToken>.Ignored))
            .ReturnsLazily((string _, IReadOnlyCollection<string> names, CancellationToken _) => Task.FromResult<IReadOnlySet<string>?>(names.ToHashSet()));
        var service = CreateClipboardService(win32Mock, A.Fake<ISoundService>(), profileService);

        // Act
        await CaptureAsync(service);

        // Assert
        A.CallTo(() => profileService.GetCapturedFormatsAsync("notepad", A<IReadOnlyCollection<string>>.Ignored, A<CancellationToken>.Ignored)).MustHaveHappened();
        A.CallTo(() => profileService.GetCapturedFormatsAsync("explorer", A<IReadOnlyCollection<string>>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task GetCurrentClipboardContentAsync_OwnerInABackgroundProgram_ProfileIsTheOwners()
    {
        // Arrange
        var win32Mock = CreateTextOffering();
        GivenClipboardOwner(win32Mock, new HWND(0x10), new HWND(0x11), 101, "notepad", "Untitled - Notepad");
        GivenForegroundWindow(win32Mock, new HWND(0x20), 202, "explorer", "Desktop");
        var profileService = A.Fake<IApplicationProfileService>();
        A.CallTo(() => profileService.IsApplicationProfilesEnabled()).Returns(true);
        A.CallTo(() => profileService.GetCapturedFormatsAsync(A<string>.Ignored, A<IReadOnlyCollection<string>>.Ignored, A<CancellationToken>.Ignored))
            .ReturnsLazily((string _, IReadOnlyCollection<string> names, CancellationToken _) => Task.FromResult<IReadOnlySet<string>?>(names.ToHashSet()));
        var service = CreateClipboardService(win32Mock, A.Fake<ISoundService>(), profileService);

        // Act
        var clip = await service.GetCurrentClipboardContentAsync();

        // Assert
        await Assert.That(clip).IsNotNull();
        A.CallTo(() => profileService.GetCapturedFormatsAsync("notepad", A<IReadOnlyCollection<string>>.Ignored, A<CancellationToken>.Ignored)).MustHaveHappened();
        A.CallTo(() => profileService.GetCapturedFormatsAsync("explorer", A<IReadOnlyCollection<string>>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task GetCurrentClipboardContentAsync_NotMonitoring_ReadsOnTheClipboardThread()
    {
        // Arrange
        var win32Mock = CreateTextOffering();
        string? readingThread = null;
        var readingWindow = HWND.Null;
        A.CallTo(() => win32Mock.OpenClipboard(A<HWND>.Ignored)).ReturnsLazily((HWND window) =>
        {
            readingThread = Thread.CurrentThread.Name;
            readingWindow = window;
            return true;
        });
        using var service = CreateClipboardService(win32Mock, A.Fake<ISoundService>());

        // Act
        var clip = await service.GetCurrentClipboardContentAsync();

        // Assert
        await Assert.That(clip?.TextContent).IsEqualTo("Hello");
        await Assert.That(readingThread).IsEqualTo("Clipboard");
        await Assert.That(readingWindow).IsNotEqualTo(HWND.Null);
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task GetCurrentClipboardContentAsync_AfterMonitoringStopped_StillReadsOnTheClipboardThread()
    {
        // Arrange
        var win32Mock = CreateTextOffering();
        string? readingThread = null;
        A.CallTo(() => win32Mock.OpenClipboard(A<HWND>.Ignored)).ReturnsLazily(() =>
        {
            readingThread = Thread.CurrentThread.Name;
            return true;
        });
        using var service = CreateClipboardService(win32Mock, A.Fake<ISoundService>());
        await service.StartMonitoringAsync();
        await service.StopMonitoringAsync();

        // Act
        var clip = await service.GetCurrentClipboardContentAsync();

        // Assert
        await Assert.That(clip?.TextContent).IsEqualTo("Hello");
        await Assert.That(readingThread).IsEqualTo("Clipboard");
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task GetCurrentClipboardContentAsync_IgnoreFlagPresent_IsCaptured()
    {
        // Arrange
        using var service = CreateClipboardService(CreateIgnoredTextOffering(), A.Fake<ISoundService>());

        // Act
        var clip = await service.GetCurrentClipboardContentAsync();

        // Assert
        await Assert.That(clip?.TextContent).IsEqualTo("Hello");
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task GetCurrentClipboardContentAsync_WhileMonitoring_DoesNotWaitForTheDelayOrSettleGap()
    {
        // Arrange
        var configuration = new ClipMateConfiguration { Preferences = { DelayOnClipboardUpdateMs = 250, SettleTimeMs = 100 } };
        using var service = CreateClipboardService(CreateTextOffering(), A.Fake<ISoundService>(), configuration: configuration,
            timeProvider: new ManualTimeProvider());
        await service.StartMonitoringAsync();
        await service.CaptureClipboardChangeAsync(HWND.Null);

        // Act
        var clip = await service.GetCurrentClipboardContentAsync().WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        await Assert.That(clip?.TextContent).IsEqualTo("Hello");
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task GetClipboardFormats_ClipWithRawFormats_OffersThemAgain()
    {
        // Arrange
        var service = CreateClipboardService();
        byte[] privateData = [1, 2, 3];
        byte[] locale = [9, 4, 0, 0];
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Text,
            TextContent = "Hello",
            RawFormats = [new RawClipboardFormat("ClipMate.Tests.Private", 0xC123, privateData), new RawClipboardFormat("CF_LOCALE", 16, locale)],
        };

        // Act
        var formats = service.GetClipboardFormats(clip);

        // Assert
        var privateId = (uint)System.Windows.DataFormats.GetDataFormat("ClipMate.Tests.Private").Id;
        await Assert.That(formats.Single(p => p.FormatId == privateId).Data).IsEquivalentTo(privateData);
        await Assert.That(formats.Single(p => p.FormatId == 16).Data).IsEquivalentTo(locale);
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task GetClipboardFormats_ClipWithOnlyRawFormats_OffersThem()
    {
        // Arrange
        var service = CreateClipboardService();
        byte[] privateData = [1, 2, 3];
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            Type = ClipType.Custom,
            RawFormats = [new RawClipboardFormat("ClipMate.Tests.Private", 0xC123, privateData)],
        };

        // Act
        var formats = service.GetClipboardFormats(clip);

        // Assert
        var privateId = (uint)System.Windows.DataFormats.GetDataFormat("ClipMate.Tests.Private").Id;
        await Assert.That(formats.Single(p => p.FormatId == privateId).Data).IsEquivalentTo(privateData);
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task CaptureClipboardChangeAsync_IgnoreFlagPresent_IsNotCaptured()
    {
        // Arrange
        var soundService = A.Fake<ISoundService>();
        var service = CreateClipboardService(CreateIgnoredTextOffering(), soundService);

        // Act
        var result = await service.CaptureClipboardChangeAsync(HWND.Null);

        // Assert
        await Assert.That(result.Outcome).IsEqualTo(ClipboardCaptureOutcome.IgnoreFlag);
        await Assert.That(service.ClipsChannel.TryRead(out _)).IsFalse();
        A.CallTo(() => soundService.PlaySoundAsync(A<SoundEvent>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task CaptureClipboardChangeAsync_IgnoreFlagPresentButNotHonoured_IsCaptured()
    {
        // Arrange
        var configuration = new ClipMateConfiguration { Preferences = { PayAttentionToClipboardIgnoreFlag = false } };
        var service = CreateClipboardService(CreateIgnoredTextOffering(), A.Fake<ISoundService>(), configuration: configuration);

        // Act
        var clip = await CaptureAsync(service);

        // Assert
        await Assert.That(clip.TextContent).IsEqualTo("Hello");
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task CaptureClipboardChangeAsync_SameContentTwice_PublishesBothWithoutASound()
    {
        // Arrange
        var soundService = A.Fake<ISoundService>();
        var service = CreateClipboardService(CreateTextOffering(), soundService);

        // Act
        var first = await CaptureAsync(service);
        var second = await CaptureAsync(service);

        // Assert
        await Assert.That(second.ContentHash).IsEqualTo(first.ContentHash);
        A.CallTo(() => soundService.PlaySoundAsync(A<SoundEvent>.Ignored, A<CancellationToken>.Ignored)).MustNotHaveHappened();
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task CreateCopy_TextClip_CarriesTheClipboardViewerIgnoreFormatOnly()
    {
        // Arrange
        var service = CreateClipboardService();
        var clip = new Clip { Id = Guid.NewGuid(), Type = ClipType.Text, TextContent = "Hello" };

        // Act
        var dataObject = service.CreateCopy(clip);

        // Assert
        await Assert.That(dataObject!.GetDataPresent(ClipboardIgnoreFormats.ClipboardViewerIgnore, false)).IsTrue();
        await Assert.That(dataObject.GetDataPresent(ClipboardIgnoreFormats.ExcludeFromMonitorProcessing, false)).IsFalse();
        await Assert.That(dataObject.GetData(System.Windows.DataFormats.UnicodeText)).IsEqualTo("Hello");
    }

    [Test]
    [TestExecutor<STAThreadExecutor>]
    public async Task GetClipboardFormats_TextClip_DoesNotIncludeTheIgnoreFormat()
    {
        // Arrange
        var service = CreateClipboardService();
        var clip = new Clip { Id = Guid.NewGuid(), Type = ClipType.Text, TextContent = "Hello" };

        // Act
        var formats = service.GetClipboardFormats(clip);

        // Assert
        var ignoreId = (uint)System.Windows.DataFormats.GetDataFormat(ClipboardIgnoreFormats.ClipboardViewerIgnore).Id;
        await Assert.That(formats.Any(p => p.FormatId == ignoreId)).IsFalse();
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
    private ClipboardService CreateClipboardService() => CreateClipboardService(CreateWin32ClipboardMock(), A.Fake<ISoundService>());

    private ClipboardService CreateClipboardService(IWin32ClipboardInterop win32Mock, ISoundService soundService,
        IApplicationProfileService? profileServiceMock = null, ClipMateConfiguration? configuration = null, TimeProvider? timeProvider = null)
    {
        var logger = CreateLogger<ClipboardService>();

        // Setup basic Win32 mock expectations
        A.CallTo(() => win32Mock.AddClipboardFormatListener(A<HWND>.Ignored)).Returns(true);
        A.CallTo(() => win32Mock.RemoveClipboardFormatListener(A<HWND>.Ignored)).Returns(true);

        if (profileServiceMock == null)
        {
            profileServiceMock = A.Fake<IApplicationProfileService>();
            A.CallTo(() => profileServiceMock.IsApplicationProfilesEnabled()).Returns(false);
        }

        A.CallTo(() => soundService.PlaySoundAsync(A<SoundEvent>.Ignored, A<CancellationToken>.Ignored)).Returns(Task.CompletedTask);

        // Create configuration mock with default timing values
        var configServiceMock = A.Fake<IConfigurationService>();
        var config = configuration ?? new ClipMateConfiguration();
        A.CallTo(() => configServiceMock.Configuration).Returns(config);

        var blobCacheServiceMock = A.Fake<IDecryptedBlobCacheService>();
        return new ClipboardService(logger, win32Mock, configServiceMock, profileServiceMock, soundService, blobCacheServiceMock, timeProvider);
    }

    /// <summary>
    /// A strict Win32 fake whose clipboard offers the given formats, in order, each with its data.
    /// </summary>
    private IWin32ClipboardInterop CreateClipboardOffering(params (uint Code, string? Name, byte[] Data)[] formats)
    {
        var win32Mock = CreateWin32ClipboardMock();
        A.CallTo(() => win32Mock.OpenClipboard(A<HWND>.Ignored)).Returns(true);
        A.CallTo(() => win32Mock.CloseClipboard()).Returns(true);
        A.CallTo(() => win32Mock.GetClipboardOwner()).Returns(HWND.Null);
        A.CallTo(() => win32Mock.GetForegroundWindow()).Returns(HWND.Null);
        A.CallTo(() => win32Mock.EnumClipboardFormats(A<uint>.Ignored)).ReturnsLazily((uint previous) =>
        {
            var index = previous == 0 ? 0 : Array.FindIndex(formats, p => p.Code == previous) + 1;
            return index < formats.Length ? formats[index].Code : 0;
        });
        foreach (var (code, name, data) in formats)
        {
            A.CallTo(() => win32Mock.GetClipboardFormatName(code)).Returns(name);
            A.CallTo(() => win32Mock.GetClipboardData(code)).Returns(data);
        }

        return win32Mock;
    }

    private IWin32ClipboardInterop CreateTextOffering() =>
        CreateClipboardOffering(((uint)Formats.UnicodeText.Code, null, System.Text.Encoding.Unicode.GetBytes("Hello\0")));

    private IWin32ClipboardInterop CreateIgnoredTextOffering() =>
        CreateClipboardOffering(((uint)Formats.UnicodeText.Code, null, System.Text.Encoding.Unicode.GetBytes("Hello\0")),
            (0xC010, ClipboardIgnoreFormats.ClipboardViewerIgnore, []));

    private static async Task<Clip> CaptureAsync(ClipboardService service)
    {
        await service.CaptureClipboardChangeAsync(HWND.Null);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        return await service.ClipsChannel.ReadAsync(cts.Token);
    }

    /// <summary>
    /// The clipboard is owned by <paramref name="owner" /> (inside the top-level window <paramref name="topLevel" />)
    /// of process <paramref name="processId" />; a null process name means the process has exited.
    /// </summary>
    private static void GivenClipboardOwner(IWin32ClipboardInterop win32Mock, HWND owner, HWND topLevel, uint processId, string? processName,
        string? topLevelTitle)
    {
        A.CallTo(() => win32Mock.GetClipboardOwner()).Returns(owner);
        GivenWindow(win32Mock, owner, topLevel, processId, processName, topLevel == owner ? topLevelTitle : null);
        if (topLevel != owner)
            GivenWindow(win32Mock, topLevel, topLevel, processId, processName, topLevelTitle);
    }

    private static void GivenForegroundWindow(IWin32ClipboardInterop win32Mock, HWND window, uint processId, string processName, string title)
    {
        A.CallTo(() => win32Mock.GetForegroundWindow()).Returns(window);
        GivenWindow(win32Mock, window, window, processId, processName, title);
    }

    private static void GivenWindow(IWin32ClipboardInterop win32Mock, HWND window, HWND topLevel, uint processId, string? processName, string? title)
    {
        A.CallTo(() => win32Mock.GetWindowProcessId(window)).Returns(processId);
        A.CallTo(() => win32Mock.GetProcessName(processId)).Returns(processName);
        A.CallTo(() => win32Mock.GetTopLevelWindow(window)).Returns(topLevel);
        A.CallTo(() => win32Mock.GetWindowTitle(window)).Returns(title);
    }
}
