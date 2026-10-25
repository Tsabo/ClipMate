using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Media.Imaging;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Platform.Helpers;
using Microsoft.Extensions.Logging;

namespace ClipMate.Platform.Services;

/// <summary>
/// Turns the formats read from the clipboard into a clip, after the clipboard is closed and off the clipboard thread.
/// The known formats (text, RTF, HTML, image, file list and drop effect) fill the clip's typed fields; every other
/// format is kept raw, in the source's order.
/// </summary>
internal sealed class ClipboardSnapshotDecoder
{
    private const string _pngFormat = "PNG";

    // Formats a typed field holds, so they are never also kept raw. CF_TEXT is the ANSI twin of CF_UNICODETEXT and
    // CF_DIBV5/PNG alternatives of the one image ClipMate keeps.
    private static readonly HashSet<string> _typedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "CF_UNICODETEXT", "CF_TEXT", Formats.RichText.Name, Formats.Html.Name,
        "CF_DIB", "CF_DIBV5", _pngFormat, "CF_HDROP", Formats.PreferredDropEffect.Name,
    };

    private readonly IApplicationProfileService _applicationProfileService;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger _logger;

    public ClipboardSnapshotDecoder(IConfigurationService configurationService, IApplicationProfileService applicationProfileService, ILogger logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _applicationProfileService = applicationProfileService ?? throw new ArgumentNullException(nameof(applicationProfileService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// The clip for the formats read, or null when there is nothing to keep (no formats, or the Application Profile of
    /// <paramref name="applicationName" /> is switched off and <paramref name="evenWhenProfileSwitchedOff" /> isn't set).
    /// </summary>
    public async Task<Clip?> DecodeAsync(IReadOnlyList<RawClipboardFormat> formats, string? applicationName, bool evenWhenProfileSwitchedOff = false,
        CancellationToken cancellationToken = default)
    {
        var allowed = await GetAllowedFormatsAsync(formats, applicationName, evenWhenProfileSwitchedOff, cancellationToken);
        if (allowed == null)
            return null;

        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            CapturedAt = DateTimeOffset.Now,
            RawFormats = allowed.Where(p => !_typedFormats.Contains(p.Name)).ToList(),
        };

        byte[]? Find(string name) => allowed.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Data;

        if (Find("CF_UNICODETEXT") is { } unicodeText)
            clip.TextContent = NullIfEmpty(DecodeUnicode(unicodeText));

        // A URL without any text on the clipboard also becomes the clip's text, as ClipMate 7 does
        if (!formats.Any(p => p.Name is "CF_UNICODETEXT" or "CF_TEXT"))
        {
            if (Find("UniformResourceLocatorW") is { } unicodeUrl)
                clip.TextContent = NullIfEmpty(DecodeUnicode(unicodeUrl));
            else if (Find("UniformResourceLocator") is { } ansiUrl)
                clip.TextContent = NullIfEmpty(DecodeAnsi(ansiUrl));
        }

        if (Find(Formats.RichText.Name) is { } rtf)
            clip.RtfContent = NullIfEmpty(DecodeAnsi(rtf));

        if (Find(Formats.Html.Name) is { } html)
        {
            clip.HtmlContent = NullIfEmpty(DecodeUtf8(html));
            if (clip.HtmlContent != null)
                clip.SourceUrl = ExtractSourceUrlFromHtml(clip.HtmlContent);
        }

        var image = DecodeImage(Find(_pngFormat), Find("CF_DIB") ?? Find("CF_DIBV5"));
        if (image != null)
            clip.ImageData = image.Value.Data;

        var filePaths = Find("CF_HDROP") is { } hDrop ? DecodeFileDrop(hDrop) : [];
        if (filePaths.Count > 0)
        {
            clip.FilePathsJson = JsonSerializer.Serialize(filePaths);
            if (Find(Formats.PreferredDropEffect.Name) is { Length: >= sizeof(int) } dropEffect)
                clip.FileDropEffect = BitConverter.ToInt32(dropEffect);
        }

        // The clip's type and title come from the first of these it has: text (including RTF and HTML), image, files
        if (!string.IsNullOrEmpty(clip.TextContent))
        {
            clip.Type = clip.HtmlContent != null ? ClipType.Html : clip.RtfContent != null ? ClipType.RichText : ClipType.Text;
            clip.Title = GenerateTitleFromText(clip.TextContent);
            clip.Size = CalculateTextSize(clip) + (clip.ImageData?.Length ?? 0);
        }
        else if (image != null)
        {
            clip.Type = ClipType.Image;
            clip.Title = $"Image {image.Value.Width}×{image.Value.Height}";
            clip.Size = image.Value.Data.Length;
        }
        else if (filePaths.Count > 0)
        {
            // "HDROP Expansion" preference: a Text clip listing the paths, keeping the file list
            clip.Type = _configurationService.Configuration.Preferences.AutoExpandHdropFilePointers ? ClipType.Text : ClipType.Files;
            clip.TextContent = string.Join(Environment.NewLine, filePaths);
            clip.Title = filePaths.Count == 1 ? Path.GetFileName(filePaths[0]) : $"{filePaths.Count} files";
            clip.Size = clip.FilePathsJson!.Length * 2;
        }
        else if (clip.RtfContent != null || clip.HtmlContent != null || clip.RawFormats.Count > 0)
        {
            clip.Type = ClipType.Custom;
            clip.Title = $"({(clip.RtfContent != null ? Formats.RichText.Name : clip.HtmlContent != null ? Formats.Html.Name : clip.RawFormats[0].Name)})";
            clip.Size = CalculateTextSize(clip);
        }
        else
            return null;

        clip.Size += clip.RawFormats.Sum(p => p.Data.Length);
        PopulateStandardFields(clip);

        return clip;
    }

    /// <summary>
    /// Whether the bytes start with a PNG or JPEG signature: "PNG", "JPEG" or "Unknown".
    /// </summary>
    internal static string DetectImageFormat(byte[] imageData)
    {
        if (imageData.Length < 8)
            return "Unknown";

        ReadOnlySpan<byte> data = imageData;
        ReadOnlySpan<byte> pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (data[..8].SequenceEqual(pngSignature))
            return "PNG";

        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return "JPEG";

        return "Unknown";
    }

    /// <summary>
    /// The formats the Application Profile lets through (all of them when profiles are off or there is no Source
    /// Application), or null when the application's profile is switched off.
    /// </summary>
    private async Task<IReadOnlyList<RawClipboardFormat>?> GetAllowedFormatsAsync(IReadOnlyList<RawClipboardFormat> formats, string? applicationName,
        bool evenWhenProfileSwitchedOff, CancellationToken cancellationToken)
    {
        if (!_applicationProfileService.IsApplicationProfilesEnabled() || string.IsNullOrEmpty(applicationName))
            return formats;

        var profileFormats = formats.Select(p => ProfileFormat(p.Name)).Distinct(StringComparer.Ordinal).ToList();
        var captured = await _applicationProfileService.ApplyProfileAsync(applicationName, profileFormats, evenWhenProfileSwitchedOff, cancellationToken);
        if (captured == null)
        {
            _logger.LogDebug("Application profile of {AppName} is switched off", applicationName);

            return null;
        }

        var allowed = formats.Where(p => captured.Contains(ProfileFormat(p.Name))).ToList();
        foreach (var item in formats.Except(allowed))
            _logger.LogDebug("Format {FormatName} filtered out for {AppName}", item.Name, applicationName);

        return allowed;
    }

    // Every image format follows the profile's image setting, the drop effect its file list, and Unicode text and the
    // locale (which ClipMate 7 keeps with any text) its text setting
    internal static string ProfileFormat(string name) => name switch
    {
        _pngFormat or "CF_DIBV5" => "CF_DIB",
        "CF_UNICODETEXT" or "CF_LOCALE" => "CF_TEXT",
        var _ when name.Equals(Formats.PreferredDropEffect.Name, StringComparison.OrdinalIgnoreCase) => "CF_HDROP",
        var _ => name,
    };

    private (byte[] Data, int Width, int Height)? DecodeImage(byte[]? png, byte[]? dib)
    {
        try
        {
            // A PNG keeps transparency; otherwise the DIB (Windows synthesizes it from a bitmap) is re-encoded as PNG
            if (png != null && DetectImageFormat(png) == "PNG")
            {
                var frame = BitmapDecoder.Create(new MemoryStream(png), BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];

                return (png, frame.PixelWidth, frame.PixelHeight);
            }

            if (dib != null && DibToBmp(dib) is { } bmp)
            {
                var frame = new BmpBitmapDecoder(new MemoryStream(bmp), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(frame));
                using var stream = new MemoryStream();
                encoder.Save(stream);

                return (stream.ToArray(), frame.PixelWidth, frame.PixelHeight);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not decode the clipboard image");
        }

        return null;
    }

    /// <summary>
    /// A BMP file for a packed DIB: the 14-byte file header, whose pixel offset skips the colour masks and palette.
    /// </summary>
    private static byte[]? DibToBmp(byte[] dib)
    {
        const int fileHeaderSize = 14;
        const int biBitFields = 3;
        if (dib.Length < 40)
            return null;

        var headerSize = BitConverter.ToInt32(dib, 0);
        var bitCount = BitConverter.ToUInt16(dib, 14);
        var compression = BitConverter.ToInt32(dib, 16);
        var colorsUsed = BitConverter.ToInt32(dib, 32);
        var masks = headerSize == 40 && compression == biBitFields ? 3 * sizeof(int) : 0;
        var paletteEntries = colorsUsed != 0 ? colorsUsed : bitCount <= 8 ? 1 << bitCount : 0;

        var bmp = new byte[fileHeaderSize + dib.Length];
        bmp[0] = (byte)'B';
        bmp[1] = (byte)'M';
        BitConverter.GetBytes(bmp.Length).CopyTo(bmp, 2);
        BitConverter.GetBytes(fileHeaderSize + headerSize + masks + paletteEntries * 4).CopyTo(bmp, 10);
        dib.CopyTo(bmp, fileHeaderSize);

        return bmp;
    }

    /// <summary>
    /// The paths in CF_HDROP data: a DROPFILES header, then null-terminated paths (wide or ANSI) ending with an empty one.
    /// </summary>
    private static List<string> DecodeFileDrop(byte[] data)
    {
        const int dropFilesSize = 20;
        if (data.Length < dropFilesSize)
            return [];

        var offset = BitConverter.ToInt32(data, 0);
        var wide = BitConverter.ToInt32(data, 16) != 0;
        if (offset < dropFilesSize || offset > data.Length)
            return [];

        var list = wide
            ? Encoding.Unicode.GetString(data, offset, (data.Length - offset) & ~1)
            : DecodeAnsi(data[offset..], stopAtNull: false);

        return list.Split('\0').TakeWhile(p => p.Length > 0).ToList();
    }

    private static string DecodeUnicode(byte[] data)
    {
        var text = Encoding.Unicode.GetString(data, 0, data.Length & ~1);
        var end = text.IndexOf('\0');

        return end >= 0 ? text[..end] : text;
    }

    private static string DecodeUtf8(byte[] data)
    {
        var end = Array.IndexOf(data, (byte)0);

        return Encoding.UTF8.GetString(data, 0, end >= 0 ? end : data.Length);
    }

    // The system ANSI code page, as the clipboard's ANSI formats use
    private static unsafe string DecodeAnsi(byte[] data, bool stopAtNull = true)
    {
        var end = stopAtNull ? Array.IndexOf(data, (byte)0) : -1;
        var length = end >= 0 ? end : data.Length;
        if (length == 0)
            return string.Empty;

        fixed (byte* bytes = data)
            return Marshal.PtrToStringAnsi((IntPtr)bytes, length);
    }

    private static string? NullIfEmpty(string text) => text.Length == 0 ? null : text;

    private void PopulateStandardFields(Clip clip)
    {
        clip.ContentHash = ContentHasher.HashClip(clip);
        clip.Checksum = clip.ContentHash.GetHashCode();
        clip.ViewTab = clip.Type switch
        {
            ClipType.RichText => 1,
            ClipType.Html => 2,
            var _ => 0,
        };

        clip.Locale = CultureInfo.CurrentCulture.LCID;
        clip.LastModified = clip.CapturedAt;
        clip.Creator = $"{Environment.UserName}@{Environment.MachineName}";

        _logger.LogDebug("Clip decoded: Type={Type}, Size={Size}, Title={Title}, raw formats {RawFormats}",
            clip.Type, clip.Size, clip.Title, clip.RawFormats.Count);
    }

    private static int CalculateTextSize(Clip clip) =>
        ((clip.TextContent?.Length ?? 0) + (clip.RtfContent?.Length ?? 0) + (clip.HtmlContent?.Length ?? 0)) * 2;

    /// <summary>
    /// Generates a title from the first line of text (max 60 chars).
    /// </summary>
    private static string GenerateTitleFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "(Empty)";

        var textSpan = text.AsSpan();
        var lineEndIndex = textSpan.IndexOfAny('\r', '\n');
        var firstLine = lineEndIndex >= 0 ? textSpan[..lineEndIndex].Trim() : textSpan.Trim();
        if (firstLine.IsEmpty)
            return "(Empty)";

        const int maxLength = 60;

        return firstLine.Length > maxLength
            ? string.Concat(firstLine[..(maxLength - 3)], "...")
            : firstLine.ToString();
    }

    /// <summary>
    /// The SourceURL header of CF_HTML data, truncated to the 250-character database field.
    /// </summary>
    private static string? ExtractSourceUrlFromHtml(string htmlContent)
    {
        const string sourceUrlMarker = "SourceURL:";
        var sourceUrlIndex = htmlContent.IndexOf(sourceUrlMarker, StringComparison.OrdinalIgnoreCase);
        if (sourceUrlIndex == -1)
            return null;

        var urlStart = sourceUrlIndex + sourceUrlMarker.Length;
        var urlEnd = htmlContent.IndexOfAny(['\r', '\n'], urlStart);
        if (urlEnd == -1)
            urlEnd = htmlContent.Length;

        var url = htmlContent[urlStart..urlEnd].Trim();
        if (string.IsNullOrEmpty(url))
            return null;

        return url.Length > 250 ? url[..250] : url;
    }
}
