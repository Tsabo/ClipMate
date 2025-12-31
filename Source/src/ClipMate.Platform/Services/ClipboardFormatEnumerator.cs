using Windows.Win32;
using Windows.Win32.Foundation;
using Microsoft.Extensions.Logging;

namespace ClipMate.Platform.Services;

/// <summary>
/// Enumerates clipboard formats using Win32 APIs.
/// Provides access to all formats currently available on the clipboard.
/// </summary>
public sealed class ClipboardFormatEnumerator : IClipboardFormatEnumerator
{
    private readonly ILogger<ClipboardFormatEnumerator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClipboardFormatEnumerator" /> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
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
            // BUGFIX: Small delay to allow clipboard owner to finish writing formats
            // Some applications (like SnagitEditor) write formats asynchronously after WM_CLIPBOARDUPDATE fires
            // Without this delay, EnumClipboardFormats may return 0 formats even though formats are being written
            // 100ms is a balance between waiting for slow apps and not delaying normal clipboard operations
            Thread.Sleep(100);

            // Try to open the clipboard with retry logic (clipboard may be locked by another app)
            const int maxRetries = 5;
            const int delayMs = 10;
            var clipboardOpened = false;

            for (var i = 0; i < maxRetries; i++)
            {
                if (PInvoke.OpenClipboard(HWND.Null))
                {
                    clipboardOpened = true;
                    break;
                }

                if (i >= maxRetries - 1)
                    continue;

                _logger.LogDebug("Clipboard locked, retry {Retry}/{MaxRetries}", i + 1, maxRetries);
                Thread.Sleep(delayMs * (i + 1)); // Exponential backoff
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

                    if (string.IsNullOrEmpty(formatName))
                        continue;

                    formats.Add(new ClipboardFormatInfo(formatName, format));
                    _logger.LogDebug("Enumerated clipboard format: {FormatName} (code: {FormatCode})", formatName, format);
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
        // Check if it's a standard format (O(1) lookup)
        if (Formats.TryGetStandardFormatName(format, out var standardName) && standardName != null)
            return standardName;

        // It's a custom registered format - get the name from Windows
        try
        {
            const int maxNameLength = 256;
            unsafe
            {
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
