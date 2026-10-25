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

    [Test]
    public async Task PopulateContent_StoredRawFormats_LoadsThemButNotTheFileListOrDropEffect()
    {
        // Arrange: text copied with an application's private format and CF_LOCALE, plus a file list and its drop effect
        var clip = new Clip { Id = Guid.NewGuid(), Type = ClipType.Text };
        var text = new ClipData { Id = Guid.NewGuid(), ClipId = clip.Id, FormatName = Formats.UnicodeText.Name, Format = Formats.UnicodeText.Code, StorageType = StorageType.Text };
        var files = new ClipData { Id = Guid.NewGuid(), ClipId = clip.Id, FormatName = Formats.HDrop.Name, Format = Formats.HDrop.Code, StorageType = StorageType.Binary };
        var dropEffect = new ClipData { Id = Guid.NewGuid(), ClipId = clip.Id, FormatName = Formats.PreferredDropEffect.Name, Format = Formats.PreferredDropEffect.Code, StorageType = StorageType.Binary };
        var privateFormat = new ClipData { Id = Guid.NewGuid(), ClipId = clip.Id, FormatName = "Acme Private", Format = 0xC123, StorageType = StorageType.Binary };
        var locale = new ClipData { Id = Guid.NewGuid(), ClipId = clip.Id, FormatName = "CF_LOCALE", Format = Formats.Locale.Code, StorageType = StorageType.Binary };

        var content = new ClipContent(clip, [text, files, dropEffect, privateFormat, locale],
            [new BlobTxt { ClipId = clip.Id, ClipDataId = text.Id, Data = "hello" }],
            [],
            [],
            [
                new BlobBlob { ClipId = clip.Id, ClipDataId = files.Id, Data = "[\"C:\\\\a.txt\"]"u8.ToArray() },
                new BlobBlob { ClipId = clip.Id, ClipDataId = dropEffect.Id, Data = BitConverter.GetBytes(2) },
                new BlobBlob { ClipId = clip.Id, ClipDataId = locale.Id, Data = [9, 4, 0, 0] },
                new BlobBlob { ClipId = clip.Id, ClipDataId = privateFormat.Id, Data = [1, 2, 3] },
            ]);

        // Act
        var target = new Clip { Id = clip.Id, Type = ClipType.Text };
        content.PopulateContent(target);

        // Assert: in the order of the stored formats
        await Assert.That(target.RawFormats.Select(p => p.Name)).IsEquivalentTo(["Acme Private", "CF_LOCALE"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(target.RawFormats[0].Code).IsEqualTo(0xC123u);
        await Assert.That(target.RawFormats[0].Data).IsEquivalentTo(new byte[] { 1, 2, 3 });
        await Assert.That(target.FileDropEffect).IsEqualTo(2);
    }
}
