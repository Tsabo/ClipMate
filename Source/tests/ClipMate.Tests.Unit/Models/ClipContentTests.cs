using ClipMate.Core.Constants;
using ClipMate.Core.Models;

namespace ClipMate.Tests.Unit.Models;

/// <summary>
/// Tests for filling a clip's content from its stored Formats.
/// </summary>
public class ClipContentTests
{
    [Test]
    public async Task PopulateContent_TextClipWithAStoredImage_LoadsTheTextAndTheImage()
    {
        // Arrange: a screenshot tool's copy, stored as a text clip holding its filename and its image
        var clip = new Clip { Id = Guid.NewGuid(), Type = ClipType.Text };
        var text = new ClipData { Id = Guid.NewGuid(), ClipId = clip.Id, FormatName = Formats.UnicodeText.Name, Format = Formats.UnicodeText.Code, StorageType = StorageType.Text };
        var image = new ClipData { Id = Guid.NewGuid(), ClipId = clip.Id, FormatName = Formats.Dib.Name, Format = Formats.Dib.Code, StorageType = StorageType.Png };
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        var content = new ClipContent(clip, [text, image],
            [new BlobTxt { ClipId = clip.Id, ClipDataId = text.Id, Data = "capture.png" }],
            [],
            [new BlobPng { ClipId = clip.Id, ClipDataId = image.Id, Data = png }],
            []);

        // Act
        var target = new Clip { Id = clip.Id, Type = ClipType.Text };
        content.PopulateContent(target);

        // Assert
        await Assert.That(target.TextContent).IsEqualTo("capture.png");
        await Assert.That(target.ImageData).IsEquivalentTo(png);
    }
}
