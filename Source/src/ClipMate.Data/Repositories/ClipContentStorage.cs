using System.Text;
using ClipMate.Core.Models;

namespace ClipMate.Data.Repositories;

/// <summary>
/// Writes a clip's transient content (TextContent, RtfContent, HtmlContent, ImageData, FilePathsJson)
/// into ClipData and BLOB table entries on a DbContext. Shared by clip capture and database import.
/// </summary>
internal static class ClipContentStorage
{
    /// <summary>
    /// Stores clip content in the appropriate BLOB tables based on available formats.
    /// Creates ClipData entries for each format and stores actual content separately.
    /// Transient properties (TextContent, ImageData, etc.) are NOT persisted to Clips table.
    /// </summary>
    public static async Task AddContentAsync(ClipMateDbContext context, Clip clip, CancellationToken cancellationToken = default)
    {
        var clipDataEntries = new List<ClipData>();

        // Handle text formats (CF_TEXT = 1, CF_UNICODETEXT = 13)
        if (!string.IsNullOrEmpty(clip.TextContent))
        {
            var clipData = new ClipData
            {
                Id = Guid.NewGuid(),
                ClipId = clip.Id,
                FormatName = Formats.UnicodeText.Name,
                Format = Formats.UnicodeText.Code,
                Size = clip.TextContent.Length * 2, // Unicode bytes
                StorageType = 1, // TEXT
            };

            clipDataEntries.Add(clipData);

            // Store in BlobTxt table
            var blobTxt = new BlobTxt
            {
                Id = Guid.NewGuid(),
                ClipDataId = clipData.Id,
                ClipId = clip.Id, // Denormalized for performance
                Data = clip.TextContent,
            };

            context.BlobTxt.Add(blobTxt);
        }

        // Handle RTF format (CF_RTF)
        if (!string.IsNullOrEmpty(clip.RtfContent))
        {
            var clipData = new ClipData
            {
                Id = Guid.NewGuid(),
                ClipId = clip.Id,
                FormatName = "CF_RTF",
                Format = Formats.RichText.Code,
                Size = clip.RtfContent.Length * 2,
                StorageType = 1, // TEXT
            };

            clipDataEntries.Add(clipData);

            var blobTxt = new BlobTxt
            {
                Id = Guid.NewGuid(),
                ClipDataId = clipData.Id,
                ClipId = clip.Id,
                Data = clip.RtfContent,
            };

            context.BlobTxt.Add(blobTxt);
        }

        // Handle HTML format (CF_HTML)
        if (!string.IsNullOrEmpty(clip.HtmlContent))
        {
            var clipData = new ClipData
            {
                Id = Guid.NewGuid(),
                ClipId = clip.Id,
                FormatName = Formats.Html.Name,
                Format = Formats.Html.Code,
                Size = clip.HtmlContent.Length * 2,
                StorageType = 1, // TEXT
            };

            clipDataEntries.Add(clipData);

            var blobTxt = new BlobTxt
            {
                Id = Guid.NewGuid(),
                ClipDataId = clipData.Id,
                ClipId = clip.Id,
                Data = clip.HtmlContent,
            };

            context.BlobTxt.Add(blobTxt);
        }

        // Handle image format - detect and store in original format
        if (clip.ImageData is { Length: > 0 })
        {
            var imageFormat = DetectImageFormat(clip.ImageData);
            var storageType = imageFormat == ImageFormat.Jpeg
                ? 2
                : 3; // 2=JPEG, 3=PNG

            var clipData = new ClipData
            {
                Id = Guid.NewGuid(),
                ClipId = clip.Id,
                FormatName = Formats.Dib.Name,
                Format = Formats.Dib.Code,
                Size = clip.ImageData.Length,
                StorageType = storageType,
            };

            clipDataEntries.Add(clipData);

            // Store in appropriate blob table based on detected format
            if (imageFormat == ImageFormat.Jpeg)
            {
                var blobJpg = new BlobJpg
                {
                    Id = Guid.NewGuid(),
                    ClipDataId = clipData.Id,
                    ClipId = clip.Id,
                    Data = clip.ImageData,
                };

                context.BlobJpg.Add(blobJpg);
            }
            else // PNG or fallback
            {
                var blobPng = new BlobPng
                {
                    Id = Guid.NewGuid(),
                    ClipDataId = clipData.Id,
                    ClipId = clip.Id,
                    Data = clip.ImageData,
                };

                context.BlobPng.Add(blobPng);
            }
        }

        // Handle files format (CF_HDROP = 15)
        if (!string.IsNullOrEmpty(clip.FilePathsJson))
        {
            var clipData = new ClipData
            {
                Id = Guid.NewGuid(),
                ClipId = clip.Id,
                FormatName = Formats.HDrop.Name,
                Format = Formats.HDrop.Code,
                Size = clip.FilePathsJson.Length * 2,
                StorageType = 4, // BLOB (generic)
            };

            clipDataEntries.Add(clipData);

            var blobBlob = new BlobBlob
            {
                Id = Guid.NewGuid(),
                ClipDataId = clipData.Id,
                ClipId = clip.Id,
                Data = Encoding.UTF8.GetBytes(clip.FilePathsJson),
            };

            context.BlobBlob.Add(blobBlob);

            if (clip.FileDropEffect is { } dropEffect)
            {
                var dropEffectData = new ClipData
                {
                    Id = Guid.NewGuid(),
                    ClipId = clip.Id,
                    FormatName = Formats.PreferredDropEffect.Name,
                    Format = Formats.PreferredDropEffect.Code,
                    Size = sizeof(int),
                    StorageType = 4, // BLOB (generic)
                };

                clipDataEntries.Add(dropEffectData);

                context.BlobBlob.Add(new BlobBlob
                {
                    Id = Guid.NewGuid(),
                    ClipDataId = dropEffectData.Id,
                    ClipId = clip.Id,
                    Data = BitConverter.GetBytes(dropEffect),
                });
            }
        }

        // Add all ClipData entries
        if (clipDataEntries.Count > 0)
            await context.ClipData.AddRangeAsync(clipDataEntries, cancellationToken);
    }

    /// <summary>
    /// Detects image format from byte array by examining magic bytes.
    /// </summary>
    internal static ImageFormat DetectImageFormat(byte[] imageData)
    {
        if (imageData.Length < 4)
            return ImageFormat.Unknown;

        // Check PNG signature: 89 50 4E 47 (‰PNG)
        if (imageData.Length >= 8 &&
            imageData[0] == 0x89 &&
            imageData[1] == 0x50 &&
            imageData[2] == 0x4E &&
            imageData[3] == 0x47 &&
            imageData[4] == 0x0D &&
            imageData[5] == 0x0A &&
            imageData[6] == 0x1A &&
            imageData[7] == 0x0A)
            return ImageFormat.Png;

        // Check JPEG signature: FF D8 FF
        if (imageData.Length >= 3 &&
            imageData[0] == 0xFF &&
            imageData[1] == 0xD8 &&
            imageData[2] == 0xFF)
            return ImageFormat.Jpeg;

        // Check GIF signature: GIF87a or GIF89a
        if (imageData.Length >= 6 &&
            imageData[0] == 0x47 && // G
            imageData[1] == 0x49 && // I
            imageData[2] == 0x46 && // F
            imageData[3] == 0x38 && // 8
            (imageData[4] == 0x37 || imageData[4] == 0x39) && // 7 or 9
            imageData[5] == 0x61) // a
            return ImageFormat.Gif;

        // Check BMP signature: BM
        if (imageData.Length >= 2 &&
            imageData[0] == 0x42 && // B
            imageData[1] == 0x4D) // M
            return ImageFormat.Bmp;

        return ImageFormat.Unknown;
    }

    internal enum ImageFormat
    {
        Unknown,
        Png,
        Jpeg,
        Gif,
        Bmp,
    }
}
