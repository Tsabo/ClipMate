using ClipMate.Core.Models;
using ClipMate.Platform.Helpers;

namespace ClipMate.Tests.Unit.Helpers;

/// <summary>
/// Tests for the clip fingerprint used for duplicate detection: it covers every Format a clip holds.
/// </summary>
public class ContentHasherTests
{
    private static readonly byte[] _image = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];
    private static readonly byte[] _otherImage = [0x89, 0x50, 0x4E, 0x47, 4, 5, 6];

    [Test]
    public async Task HashClip_SingleFormat_MatchesTheHashOfThatContentAlone()
    {
        // Clips stored before multi-Format clips existed must keep matching
        await Assert.That(ContentHasher.HashClip(new Clip { TextContent = "hello" })).IsEqualTo(ContentHasher.HashText("hello"));
        await Assert.That(ContentHasher.HashClip(new Clip { ImageData = _image })).IsEqualTo(ContentHasher.HashBytes(_image));
        await Assert.That(ContentHasher.HashClip(new Clip { FilePathsJson = "[\"C:\\\\a.txt\"]" })).IsEqualTo(ContentHasher.HashText("[\"C:\\\\a.txt\"]"));
    }

    [Test]
    public async Task HashClip_SameTextDifferentImages_AreDifferentClips()
    {
        // A screenshot tool offering the same filename text for two different screenshots
        var first = ContentHasher.HashClip(new Clip { TextContent = "capture.png", ImageData = _image });
        var second = ContentHasher.HashClip(new Clip { TextContent = "capture.png", ImageData = _otherImage });

        await Assert.That(first).IsNotEqualTo(second);
        await Assert.That(first).IsNotEqualTo(ContentHasher.HashText("capture.png"));
    }

    [Test]
    public async Task HashClip_SameFilesCopiedOrCut_AreDifferentClips()
    {
        const string files = "[\"C:\\\\a.txt\"]";

        var copied = ContentHasher.HashClip(new Clip { FilePathsJson = files, FileDropEffect = 1 });
        var cut = ContentHasher.HashClip(new Clip { FilePathsJson = files, FileDropEffect = 2 });

        await Assert.That(copied).IsNotEqualTo(cut);
    }

    [Test]
    public async Task HashClip_SameFormatsInAnyClipType_HashTheSame()
    {
        // The type only decides how a clip is listed; the fingerprint depends on its Formats
        var asText = ContentHasher.HashClip(new Clip { Type = ClipType.Text, TextContent = "x", ImageData = _image });
        var asImage = ContentHasher.HashClip(new Clip { Type = ClipType.Image, TextContent = "x", ImageData = _image });

        await Assert.That(asText).IsEqualTo(asImage);
    }

    [Test]
    public async Task HashClip_NoContent_IsEmpty()
    {
        await Assert.That(ContentHasher.HashClip(new Clip())).IsEqualTo(string.Empty);
    }
}
