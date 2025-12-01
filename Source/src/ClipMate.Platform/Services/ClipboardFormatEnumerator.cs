using Windows.Win32;
using Microsoft.Extensions.Logging;

namespace ClipMate.Platform.Services;

/// <summary>
/// Enumerates clipboard formats using Win32 APIs.
/// Provides access to all formats currently available on the clipboard.
/// </summary>
public class ClipboardFormatEnumerator : IClipboardFormatEnumerator
{
    // Standard clipboard format names mapping
    private static readonly Dictionary<uint, string> _standardFormatNames = new()
    {
        [(uint)Formats.Text.Code] = Formats.Text.Name,
        [(uint)Formats.Bitmap.Code] = Formats.Bitmap.Name,
        [(uint)Formats.Metafilepict.Code] = Formats.Metafilepict.Name,
        [(uint)Formats.Sylk.Code] = Formats.Sylk.Name,
        [(uint)Formats.Dif.Code] = Formats.Dif.Name,
        [(uint)Formats.Tiff.Code] = Formats.Tiff.Name,
        [(uint)Formats.OemText.Code] = Formats.OemText.Name,
        [(uint)Formats.Dib.Code] = Formats.Dib.Name,
        [(uint)Formats.Palette.Code] = Formats.Palette.Name,
        [(uint)Formats.PenData.Code] = Formats.PenData.Name,
        [(uint)Formats.Riff.Code] = Formats.Riff.Name,
        [(uint)Formats.Wave.Code] = Formats.Wave.Name,
        [(uint)Formats.UnicodeText.Code] = Formats.UnicodeText.Name,
        [(uint)Formats.EnhMetafile.Code] = Formats.EnhMetafile.Name,
        [(uint)Formats.HDrop.Code] = Formats.HDrop.Name,
        [(uint)Formats.Locale.Code] = Formats.Locale.Name,
    };

    private readonly ILogger<ClipboardFormatEnumerator> _logger;

    public ClipboardFormatEnumerator(ILogger<ClipboardFormatEnumerator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public IReadOnlyList<ClipboardFormatInfo> GetAllAvailableFormats()
    {
        var formats = new List<ClipboardFormatInfo>();

        try
        {
            // Try to open the clipboard with retry logic (clipboard may be locked by another app)
            const int maxRetries = 5;
            const int delayMs = 10;
            var clipboardOpened = false;

            for (var i = 0; i < maxRetries; i++)
            {
                if (PInvoke.OpenClipboard(default))
                {
                    clipboardOpened = true;
                    break;
                }

                if (i < maxRetries - 1)
                {
                    _logger.LogDebug("Clipboard locked, retry {Retry}/{MaxRetries}", i + 1, maxRetries);
                    Thread.Sleep(delayMs * (i + 1)); // Exponential backoff
                }
            }

            if (!clipboardOpened)
            {
                _logger.LogWarning("Failed to open clipboard for format enumeration after {MaxRetries} retries", maxRetries);
                return formats;
            }

            try
            {
                // Enumerate all formats
                uint format = 0;
                while ((format = PInvoke.EnumClipboardFormats(format)) != 0)
                {
                    var formatName = GetFormatName(format);

                    if (!string.IsNullOrEmpty(formatName))
                    {
                        formats.Add(new ClipboardFormatInfo(formatName, format));
                        _logger.LogDebug("Enumerated clipboard format: {FormatName} (code: {FormatCode})", formatName, format);
                    }
                }

                _logger.LogInformation("Enumerated {Count} clipboard formats", formats.Count);
            }
            finally
            {
                PInvoke.CloseClipboard();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enumerating clipboard formats");
        }

        return formats;
    }

    /// <summary>
    /// Gets the name of a clipboard format.
    /// </summary>
    /// <param name="format">The format code.</param>
    /// <returns>The format name, or null if not found.</returns>
    private string? GetFormatName(uint format)
    {
        // Check if it's a standard format
        if (_standardFormatNames.TryGetValue(format, out var standardName))
            return standardName;

        // It's a custom registered format - get the name from Windows
        try
        {
            const int maxNameLength = 256;
            unsafe
            {
                // Allocate buffer for format name
                var buffer = stackalloc char[maxNameLength];

                var length = PInvoke.GetClipboardFormatName(format, buffer, maxNameLength);

                if (length > 0)
                    return new string(buffer, 0, length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get clipboard format name for code {FormatCode}", format);
        }

        return null;
    }
}
