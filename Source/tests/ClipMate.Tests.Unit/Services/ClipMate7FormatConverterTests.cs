using System.Text;
using System.Text.Json;
using ClipMate.Core.Constants;
using ClipMate.Core.Models;
using ClipMate.Data.Legacy.ClipMate7;
using ClipMate.Platform.Helpers;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// Tests for converting ClipMate 7 clip formats into ClipMate clip content.
/// </summary>
public class ClipMate7FormatConverterTests
{
    private static readonly byte[] _pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];

    private static Clip Convert(out int unsupported, params ClipMate7Format[] formats)
    {
        var clip = new Clip { Id = Guid.NewGuid() };
        unsupported = ClipMate7FormatConverter.Apply(clip, formats);
        return clip;
    }

    [Test]
    public async Task Apply_AnsiText_DecodesWindows1252()
    {
        // Arrange: “Hi” ® in Windows-1252, with a trailing NUL
        byte[] data = [0x93, (byte)'H', (byte)'i', 0x94, (byte)' ', 0xAE, 0x00];

        // Act
        var clip = Convert(out var unsupported, new ClipMate7Format("TEXT", Formats.Text.Code, data));

        // Assert
        await Assert.That(clip.TextContent).IsEqualTo("\u201CHi\u201D \u00AE");
        await Assert.That(clip.Type).IsEqualTo(ClipType.Text);
        await Assert.That(clip.ContentHash).IsEqualTo(ContentHasher.HashText(clip.TextContent!));
        await Assert.That(unsupported).IsEqualTo(0);
    }

    [Test]
    public async Task Apply_UnicodeAndAnsiText_PrefersUnicode()
    {
        var clip = Convert(out _,
            new ClipMate7Format("TEXT", Formats.Text.Code, Encoding.ASCII.GetBytes("ansi")),
            new ClipMate7Format("CF_UNICODETEXT", Formats.UnicodeText.Code, Encoding.Unicode.GetBytes("unicode\0")));

        await Assert.That(clip.TextContent).IsEqualTo("unicode");
    }

    [Test]
    public async Task Apply_HtmlWithText_IsHtmlClipAndTrimsNul()
    {
        var html = Encoding.UTF8.GetBytes("Version:0.9\r\n<b>caf\u00E9</b>\0");

        var clip = Convert(out _,
            new ClipMate7Format("HTML Format", -3, html),
            new ClipMate7Format("TEXT", Formats.Text.Code, Encoding.ASCII.GetBytes("cafe")));

        await Assert.That(clip.HtmlContent).IsEqualTo("Version:0.9\r\n<b>caf\u00E9</b>");
        await Assert.That(clip.Type).IsEqualTo(ClipType.Html);
    }

    [Test]
    public async Task Apply_HtmlNotUtf8_FallsBackToWindows1252()
    {
        byte[] html = [(byte)'<', (byte)'b', (byte)'>', 0xE9, (byte)'<', (byte)'/', (byte)'b', (byte)'>'];

        var clip = Convert(out _, new ClipMate7Format("HTML Format", -3, html));

        await Assert.That(clip.HtmlContent).IsEqualTo("<b>\u00E9</b>");
    }

    [Test]
    public async Task Apply_RichText_SetsRtfContent()
    {
        var clip = Convert(out _,
            new ClipMate7Format("Rich Text Format", -1, Encoding.ASCII.GetBytes("{\\rtf1 hi}\0")),
            new ClipMate7Format("TEXT", Formats.Text.Code, Encoding.ASCII.GetBytes("hi")));

        await Assert.That(clip.RtfContent).IsEqualTo("{\\rtf1 hi}");
        await Assert.That(clip.Type).IsEqualTo(ClipType.RichText);
    }

    [Test]
    public async Task Apply_PngBitmap_IsImageClip()
    {
        var clip = Convert(out var unsupported, new ClipMate7Format("BITMAP", Formats.Bitmap.Code, _pngSignature));

        await Assert.That(clip.ImageData).IsEquivalentTo(_pngSignature);
        await Assert.That(clip.Type).IsEqualTo(ClipType.Image);
        await Assert.That(clip.ContentHash).IsEqualTo(ContentHasher.HashBytes(_pngSignature));
        await Assert.That(unsupported).IsEqualTo(0);
    }

    [Test]
    public async Task Apply_RawBitmapWithoutPngOrJpegSignature_IsUnsupported()
    {
        var clip = Convert(out var unsupported, new ClipMate7Format("BITMAP", Formats.Bitmap.Code, [0x28, 0x00, 0x00, 0x00, 0x01]));

        await Assert.That(unsupported).IsEqualTo(1);
        await Assert.That(ClipMate7FormatConverter.HasContent(clip)).IsFalse();
    }

    [Test]
    public async Task Apply_WideHDrop_SetsFilePathsAndText()
    {
        // DROPFILES: pFiles = 20, pt = (0,0), fNC = 0, fWide = 1, then double-NUL-terminated UTF-16 paths
        var paths = Encoding.Unicode.GetBytes("C:\\a.txt\0C:\\b.txt\0\0");
        var data = new byte[20 + paths.Length];
        BitConverter.GetBytes(20).CopyTo(data, 0);
        BitConverter.GetBytes(1).CopyTo(data, 16);
        paths.CopyTo(data, 20);

        var clip = Convert(out _, new ClipMate7Format("HDROP", Formats.HDrop.Code, data));

        await Assert.That(JsonSerializer.Deserialize<List<string>>(clip.FilePathsJson!)).IsEquivalentTo(["C:\\a.txt", "C:\\b.txt"]);
        await Assert.That(clip.TextContent).IsEqualTo($"C:\\a.txt{Environment.NewLine}C:\\b.txt");
        await Assert.That(clip.Type).IsEqualTo(ClipType.Files);
    }

    [Test]
    public async Task Apply_UnknownFormat_CountsUnsupportedAndKeepsOthers()
    {
        var clip = Convert(out var unsupported,
            new ClipMate7Format("Text", 0, Encoding.Unicode.GetBytes("custom")),
            new ClipMate7Format("TEXT", Formats.Text.Code, Encoding.ASCII.GetBytes("plain")));

        await Assert.That(unsupported).IsEqualTo(1);
        await Assert.That(clip.TextContent).IsEqualTo("plain");
        await Assert.That(ClipMate7FormatConverter.HasContent(clip)).IsTrue();
    }
}
