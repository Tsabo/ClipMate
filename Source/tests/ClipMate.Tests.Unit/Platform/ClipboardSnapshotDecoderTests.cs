using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipMate.Core.Constants;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Platform.Helpers;
using ClipMate.Platform.Services;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Assertions.Enums;

namespace ClipMate.Tests.Unit.Platform;

/// <summary>
/// Tests for turning a raw clipboard read into a clip: the known formats fill its typed fields, the rest stay raw.
/// </summary>
public class ClipboardSnapshotDecoderTests
{
    private const string _pngFormat = "PNG";
    private static readonly RawClipboardFormat _privateFormat = new("Acme Private", 0xC123, [1, 2, 3]);
    private static readonly RawClipboardFormat _locale = new("CF_LOCALE", (uint)Formats.Locale.Code, [9, 4, 0, 0]);

    [Test]
    public async Task DecodeAsync_TextWithOtherFormats_FillsTheTextAndKeepsTheRestRawInTheSourcesOrder()
    {
        var decoder = CreateDecoder();

        var clip = await decoder.DecodeAsync([_privateFormat, UnicodeText("Hello\r\nworld"), Ansi("CF_TEXT", 1, "Hello"), _locale], null);

        await Assert.That(clip!.Type).IsEqualTo(ClipType.Text);
        await Assert.That(clip.TextContent).IsEqualTo("Hello\r\nworld");
        await Assert.That(clip.Title).IsEqualTo("Hello");
        await Assert.That(clip.RawFormats.Select(p => p.Name)).IsEquivalentTo(["Acme Private", "CF_LOCALE"], CollectionOrdering.Matching);
        await Assert.That(clip.ContentHash).IsEqualTo(ContentHasher.HashText("Hello\r\nworld"));
    }

    [Test]
    public async Task DecodeAsync_RtfAndHtml_FillTheirFieldsAndTheSourceUrl()
    {
        var decoder = CreateDecoder();
        const string html = "Version:0.9\r\nSourceURL:https://example.com/page\r\n<b>Héllo</b>";

        var clip = await decoder.DecodeAsync(
        [
            UnicodeText("Héllo"),
            Ansi(Formats.RichText.Name, 0xC0B0, @"{\rtf1 Hello}"),
            new RawClipboardFormat(Formats.Html.Name, 0xC0A0, [..Encoding.UTF8.GetBytes(html), 0]),
        ], null);

        await Assert.That(clip!.Type).IsEqualTo(ClipType.Html);
        await Assert.That(clip.RtfContent).IsEqualTo(@"{\rtf1 Hello}");
        await Assert.That(clip.HtmlContent).IsEqualTo(html);
        await Assert.That(clip.SourceUrl).IsEqualTo("https://example.com/page");
        await Assert.That(clip.RawFormats).IsEmpty();
    }

    [Test]
    public async Task DecodeAsync_FileListAndDropEffect_IsAFilesClip()
    {
        var configuration = new ClipMate.Core.Models.Configuration.ClipMateConfiguration();
        configuration.Preferences.AutoExpandHdropFilePointers = false;
        var decoder = CreateDecoder(configuration: configuration);
        string[] files = [@"C:\Docs\a.txt", @"C:\Docs\b.txt"];

        var clip = await decoder.DecodeAsync([HDrop(files), new RawClipboardFormat(Formats.PreferredDropEffect.Name, 0xC0C0, BitConverter.GetBytes(2))], null);

        await Assert.That(clip!.Type).IsEqualTo(ClipType.Files);
        await Assert.That(clip.FilePathsJson).IsEqualTo(JsonSerializer.Serialize(files));
        await Assert.That(clip.FileDropEffect).IsEqualTo(2);
        await Assert.That(clip.Title).IsEqualTo("2 files");
        await Assert.That(clip.RawFormats).IsEmpty();
    }

    [Test]
    public async Task DecodeAsync_Dib_IsAnImageClipStoredAsPng()
    {
        var decoder = CreateDecoder();

        var clip = await decoder.DecodeAsync([new RawClipboardFormat("CF_DIB", (uint)Formats.Dib.Code, DataObjectFormats.EncodeDib(CreatePng(3, 2)))], null);

        await Assert.That(clip!.Type).IsEqualTo(ClipType.Image);
        await Assert.That(clip.Title).IsEqualTo("Image 3×2");
        var frame = BitmapDecoder.Create(new MemoryStream(clip.ImageData!), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        await Assert.That(frame.Decoder).IsTypeOf<PngBitmapDecoder>();
        await Assert.That(frame.PixelWidth).IsEqualTo(3);
        await Assert.That(frame.PixelHeight).IsEqualTo(2);
    }

    [Test]
    public async Task DecodeAsync_OnlyPrivateFormats_IsACustomClipHashedByThem()
    {
        var decoder = CreateDecoder();

        var clip = await decoder.DecodeAsync([_privateFormat], null);

        await Assert.That(clip!.Type).IsEqualTo(ClipType.Custom);
        await Assert.That(clip.RawFormats.Single()).IsEqualTo(_privateFormat);
        await Assert.That(clip.ContentHash).IsEqualTo(ContentHasher.HashClip(new Clip { RawFormats = [_privateFormat] }));
        await Assert.That(clip.Title).IsEqualTo("(Acme Private)");
    }

    [Test]
    public async Task DecodeAsync_UrlWithoutText_TakesTheUnicodeUrlAsTextAndKeepsTheUrlFormatsRaw()
    {
        var decoder = CreateDecoder();
        var unicodeUrl = new RawClipboardFormat("UniformResourceLocatorW", 0xC0E1, DataObjectFormats.EncodeUnicodeText("https://example.com/é"));
        var ansiUrl = Ansi("UniformResourceLocator", 0xC0E0, "https://example.com/?");

        var clip = await decoder.DecodeAsync([ansiUrl, unicodeUrl], null);

        await Assert.That(clip!.Type).IsEqualTo(ClipType.Text);
        await Assert.That(clip.TextContent).IsEqualTo("https://example.com/é");
        await Assert.That(clip.Title).IsEqualTo("https://example.com/é");
        await Assert.That(clip.RawFormats.Select(p => p.Name)).IsEquivalentTo(["UniformResourceLocator", "UniformResourceLocatorW"], CollectionOrdering.Matching);
        await Assert.That(clip.ContentHash).IsEqualTo(ContentHasher.HashText("https://example.com/é"));
    }

    [Test]
    public async Task DecodeAsync_AnsiUrlWithoutText_TakesItAsText()
    {
        var decoder = CreateDecoder();

        var clip = await decoder.DecodeAsync([Ansi("UniformResourceLocator", 0xC0E0, "https://example.com/")], null);

        await Assert.That(clip!.TextContent).IsEqualTo("https://example.com/");
    }

    [Test]
    public async Task DecodeAsync_UrlWithText_KeepsTheText()
    {
        var decoder = CreateDecoder();

        var clip = await decoder.DecodeAsync([UnicodeText("Example"), Ansi("UniformResourceLocator", 0xC0E0, "https://example.com/")], null);

        await Assert.That(clip!.TextContent).IsEqualTo("Example");
    }

    [Test]
    public async Task DecodeAsync_ProfileSwitchesOffTheTextOfAUrl_AddsNoText()
    {
        var decoder = CreateDecoder(CreateProfiles("CF_UNICODETEXT"));

        var clip = await decoder.DecodeAsync([UnicodeText("Example"), Ansi("UniformResourceLocator", 0xC0E0, "https://example.com/")], "acme");

        await Assert.That(clip!.TextContent).IsNull();
        await Assert.That(clip.Type).IsEqualTo(ClipType.Custom);
    }

    [Test]
    public async Task DecodeAsync_NoFormats_IsNoClip()
    {
        var decoder = CreateDecoder();

        await Assert.That(await decoder.DecodeAsync([], null)).IsNull();
    }

    [Test]
    public async Task DecodeAsync_ProfileSwitchesOffHtmlAndARawFormat_DropsThemAndKeepsTheRest()
    {
        var profiles = CreateProfiles(Formats.Html.Name, "CF_LOCALE");
        var decoder = CreateDecoder(profiles);

        var clip = await decoder.DecodeAsync([UnicodeText("x"), new RawClipboardFormat(Formats.Html.Name, 0xC0A0, "<b>x</b>"u8.ToArray()), _privateFormat, _locale], "acme");

        await Assert.That(clip!.HtmlContent).IsNull();
        await Assert.That(clip.TextContent).IsEqualTo("x");
        await Assert.That(clip.RawFormats.Single()).IsEqualTo(_privateFormat);
    }

    [Test]
    public async Task DecodeAsync_WithProfiles_AsksAboutEveryFormatImagesAsTheDibAndTheDropEffectAsTheFileList()
    {
        var profiles = CreateProfiles();
        var decoder = CreateDecoder(profiles);
        string[] files = [@"C:\Docs\a.txt"];

        await decoder.DecodeAsync(
        [
            new RawClipboardFormat(_pngFormat, 0xC0D0, CreatePng(1, 1)), new RawClipboardFormat("CF_DIBV5", (uint)Formats.DibV5.Code, [1]),
            HDrop(files), new RawClipboardFormat(Formats.PreferredDropEffect.Name, 0xC0C0, BitConverter.GetBytes(2)), _privateFormat,
        ], "acme");

        A.CallTo(() => profiles.GetCapturedFormatsAsync("acme",
                A<IReadOnlyCollection<string>>.That.IsSameSequenceAs(new[] { "CF_DIB", "CF_HDROP", "Acme Private" }), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task DecodeAsync_ProfileDisabled_IsNoClip()
    {
        var profiles = A.Fake<IApplicationProfileService>();
        A.CallTo(() => profiles.IsApplicationProfilesEnabled()).Returns(true);
        A.CallTo(() => profiles.GetCapturedFormatsAsync("acme", A<IReadOnlyCollection<string>>._, A<CancellationToken>._)).Returns((IReadOnlySet<string>?)null);
        var decoder = CreateDecoder(profiles);

        await Assert.That(await decoder.DecodeAsync([UnicodeText("x"), _privateFormat], "acme")).IsNull();
    }

    // A profile service keeping every format except the ones given
    private static IApplicationProfileService CreateProfiles(params string[] switchedOff)
    {
        var profiles = A.Fake<IApplicationProfileService>();
        A.CallTo(() => profiles.IsApplicationProfilesEnabled()).Returns(true);
        A.CallTo(() => profiles.GetCapturedFormatsAsync("acme", A<IReadOnlyCollection<string>>._, A<CancellationToken>._))
            .ReturnsLazily((string _, IReadOnlyCollection<string> names, CancellationToken _) =>
                Task.FromResult<IReadOnlySet<string>?>(names.Except(switchedOff).ToHashSet()));

        return profiles;
    }

    private static ClipboardSnapshotDecoder CreateDecoder(IApplicationProfileService? profiles = null,
        ClipMate.Core.Models.Configuration.ClipMateConfiguration? configuration = null)
    {
        var configurationService = A.Fake<IConfigurationService>();
        A.CallTo(() => configurationService.Configuration).Returns(configuration ?? new ClipMate.Core.Models.Configuration.ClipMateConfiguration());
        if (profiles == null)
        {
            profiles = A.Fake<IApplicationProfileService>();
            A.CallTo(() => profiles.IsApplicationProfilesEnabled()).Returns(false);
        }

        return new ClipboardSnapshotDecoder(configurationService, profiles, NullLogger.Instance);
    }

    private static RawClipboardFormat UnicodeText(string text) =>
        new("CF_UNICODETEXT", (uint)Formats.UnicodeText.Code, DataObjectFormats.EncodeUnicodeText(text));

    private static RawClipboardFormat Ansi(string name, uint code, string text) => new(name, code, [..Encoding.ASCII.GetBytes(text), 0]);

    private static RawClipboardFormat HDrop(string[] files)
    {
        // DROPFILES: pFiles, pt, fNC, fWide, then the wide paths, each null-terminated, and a final null
        var paths = Encoding.Unicode.GetBytes(string.Concat(files.Select(p => p + "\0")) + "\0");
        var data = new byte[20 + paths.Length];
        BitConverter.GetBytes(20).CopyTo(data, 0);
        BitConverter.GetBytes(1).CopyTo(data, 16);
        paths.CopyTo(data, 20);

        return new RawClipboardFormat("CF_HDROP", (uint)Formats.HDrop.Code, data);
    }

    private static byte[] CreatePng(int width, int height)
    {
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, new byte[width * height * 4], width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
