using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Channels;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Windows.Win32;
using Windows.Win32.Foundation;
using ClipMate.Core.Exceptions;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Platform.Helpers;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;
using DataFormats = System.Windows.DataFormats;
// Aliases to resolve WPF vs WinForms ambiguity
using WpfClipboard = System.Windows.Clipboard;
using WpfDataObject = System.Windows.DataObject;

namespace ClipMate.Platform.Services;

/// <summary>
///     Service for monitoring and capturing clipboard changes using Win32 APIs via CsWin32.
///     Captures all formats: Text, RTF, HTML, Images, and Files.
///     Uses a channel-based pattern for publishing captured clips.
/// </summary>
public class ClipboardService : IClipboardService, IDisposable
{
    private const int _debounceMilliseconds = 50;
    private const int _channelCapacity = 100; // Max queued clips before backpressure
    
    private readonly ILogger<ClipboardService> _logger;
    private readonly Channel<Clip> _clipsChannel;
    private HwndSource? _hwndSource;
    private DateTime _lastClipboardChange = DateTime.MinValue;
    private string _lastContentHash = string.Empty;

    public ClipboardService(ILogger<ClipboardService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Create bounded channel with drop oldest policy to prevent memory issues
        _clipsChannel = Channel.CreateBounded<Clip>(new BoundedChannelOptions(_channelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true, // Only clipboard monitor writes
            SingleReader = false // Multiple consumers allowed
        });
    }

    /// <inheritdoc />
    public ChannelReader<Clip> ClipsChannel => _clipsChannel.Reader;

    /// <inheritdoc />
    public bool IsMonitoring { get; private set; }

    /// <inheritdoc />
    public Task StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        if (IsMonitoring)
        {
            _logger.LogDebug("Clipboard monitoring already active");
            return Task.CompletedTask;
        }

        try
        {
            // Create a message-only window for receiving clipboard notifications
            var hwndSourceParams = new HwndSourceParameters("ClipboardMonitor")
            {
                Width = 0,
                Height = 0,
                PositionX = 0,
                PositionY = 0,
                WindowStyle = 0,
            };

            _hwndSource = new HwndSource(hwndSourceParams);
            _hwndSource.AddHook(WndProc);

            var hwnd = new HWND(_hwndSource.Handle);

            // Register for clipboard notifications
            if (!PInvoke.AddClipboardFormatListener(hwnd))
            {
                var error = Marshal.GetLastWin32Error();
                throw new ClipboardException($"Failed to register clipboard listener. Error: {error}");
            }

            IsMonitoring = true;
            _logger.LogInformation("Clipboard monitoring started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start clipboard monitoring");
            throw new ClipboardException("Failed to start clipboard monitoring", ex);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopMonitoringAsync()
    {
        if (!IsMonitoring)
        {
            return Task.CompletedTask;
        }

        try
        {
            if (_hwndSource != null)
            {
                var hwnd = new HWND(_hwndSource.Handle);
                PInvoke.RemoveClipboardFormatListener(hwnd);

                _hwndSource.RemoveHook(WndProc);
                _hwndSource.Dispose();
                _hwndSource = null;
            }

            IsMonitoring = false;
            
            // Complete the channel - no more clips will be written
            _clipsChannel.Writer.Complete();
            
            _logger.LogInformation("Clipboard monitoring stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping clipboard monitoring");
            _clipsChannel.Writer.Complete(ex);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<Clip?> GetCurrentClipboardContentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Must run on STA thread (UI thread) for WPF Clipboard API
            return await Application.Current.Dispatcher.InvokeAsync(() => ExtractClipboardData());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting clipboard content");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SetClipboardContentAsync(Clip clip, CancellationToken cancellationToken = default)
    {
        if (clip == null)
        {
            throw new ArgumentNullException(nameof(clip));
        }

        try
        {
            // Must run on STA thread (UI thread) for WPF Clipboard API
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // WPF Clipboard API handles OpenClipboard/CloseClipboard internally
                switch (clip.Type)
                {
                    case ClipType.Text:
                        SetTextToClipboard(clip);
                        break;
                    case ClipType.Image:
                        SetImageToClipboard(clip);
                        break;
                    case ClipType.Files:
                        SetFilesToClipboard(clip);
                        break;
                    default:
                        _logger.LogWarning("Unsupported clip type: {ClipType}", clip.Type);
                        break;
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting clipboard content");
            throw new ClipboardException("Failed to set clipboard content", ex);
        }
    }

    public void Dispose()
    {
        StopMonitoringAsync().Wait();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int wmClipboardUpdate = 0x031D;

        if (msg == wmClipboardUpdate)
        {
            handled = true;
            _ = Task.Run(async () => await HandleClipboardChangeAsync());
        }

        return IntPtr.Zero;
    }

    private async Task HandleClipboardChangeAsync()
    {
        try
        {
            // Debouncing: ignore if too soon after last change
            var now = DateTime.UtcNow;
            if ((now - _lastClipboardChange).TotalMilliseconds < _debounceMilliseconds)
            {
                return;
            }

            _lastClipboardChange = now;

            var clip = await GetCurrentClipboardContentAsync();
            if (clip == null)
            {
                return;
            }

            // Duplicate detection: ignore if same content hash
            if (clip.ContentHash == _lastContentHash)
            {
                _logger.LogDebug("Ignoring duplicate clipboard content");
                return;
            }

            _lastContentHash = clip.ContentHash;

            // Get source application info
            var foregroundWindow = PInvoke.GetForegroundWindow();
            if (!foregroundWindow.IsNull)
            {
                clip.SourceApplicationName = GetProcessName(foregroundWindow);
                clip.SourceApplicationTitle = GetWindowTitle(foregroundWindow);
            }

            // Write to channel (non-blocking, drops oldest if full)
            await _clipsChannel.Writer.WriteAsync(clip);
            
            _logger.LogDebug("Published clip to channel: {ClipType}, Size: {Size} bytes, Hash: {Hash}",
                clip.Type, clip.Size, clip.ContentHash);
        }
        catch (ChannelClosedException)
        {
            _logger.LogDebug("Clipboard channel closed, ignoring new clip");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling clipboard change");
        }
    }

    /// <summary>
    ///     Extracts all available clipboard data in priority order.
    ///     Priority: Text > RTF > HTML > Image > Files
    /// </summary>
    private Clip? ExtractClipboardData()
    {
        Clip? clip = null;

        // Check what formats are available
        var hasText = WpfClipboard.ContainsText();
        var hasImage = WpfClipboard.ContainsImage();
        var hasFiles = WpfClipboard.ContainsFileDropList();

        // Priority 1: Text formats (includes RTF and HTML)
        if (hasText || WpfClipboard.ContainsData(DataFormats.Rtf) ||
            WpfClipboard.ContainsData(DataFormats.Html))
        {
            clip = ExtractTextClip();
        }
        // Priority 2: Images
        else if (hasImage)
        {
            clip = ExtractImageClip();
        }
        // Priority 3: Files
        else if (hasFiles)
        {
            clip = ExtractFilesClip();
        }

        // Populate standard fields
        if (clip != null)
        {
            PopulateStandardFields(clip);
        }

        return clip;
    }

    /// <summary>
    ///     Extracts text-based clipboard content (Plain Text, RTF, HTML).
    /// </summary>
    private Clip? ExtractTextClip()
    {
        try
        {
            var clip = new Clip
            {
                Id = Guid.NewGuid(),
                Type = ClipType.Text,
                CapturedAt = DateTime.UtcNow,
            };

            // Extract Plain Text (CF_UNICODETEXT = 13)
            if (WpfClipboard.ContainsText())
            {
                clip.TextContent = WpfClipboard.GetText();
            }

            // Extract RTF (CF_RTF)
            if (WpfClipboard.ContainsData(DataFormats.Rtf))
            {
                clip.RtfContent = WpfClipboard.GetData(DataFormats.Rtf) as string;
                if (!string.IsNullOrEmpty(clip.RtfContent))
                {
                    clip.Type = ClipType.RichText;
                }
            }

            // Extract HTML (CF_HTML) and Source URL
            if (WpfClipboard.ContainsData(DataFormats.Html))
            {
                clip.HtmlContent = WpfClipboard.GetData(DataFormats.Html) as string;
                if (!string.IsNullOrEmpty(clip.HtmlContent))
                {
                    clip.Type = ClipType.Html;

                    // Extract source URL from HTML clipboard format
                    // HTML clipboard format includes metadata like SourceURL in the header
                    clip.SourceUrl = ExtractSourceUrlFromHtml(clip.HtmlContent);
                }
            }

            // Must have at least some text content
            if (string.IsNullOrEmpty(clip.TextContent))
            {
                return null;
            }

            // Generate content hash from primary content
            clip.ContentHash = ContentHasher.HashText(clip.TextContent);

            // Calculate size (all text formats combined)
            clip.Size = CalculateTextSize(clip);

            // Auto-generate title from first line
            clip.Title = GenerateTitleFromText(clip.TextContent);

            return clip;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from clipboard");
            return null;
        }
    }

    /// <summary>
    ///     Extracts image clipboard content (CF_BITMAP = 2, CF_DIB = 8).
    /// </summary>
    private Clip? ExtractImageClip()
    {
        try
        {
            if (!WpfClipboard.ContainsImage())
            {
                return null;
            }

            var image = WpfClipboard.GetImage();
            if (image == null)
            {
                return null;
            }

            // Convert BitmapSource to byte array (PNG format for storage)
            var imageData = ConvertBitmapSourceToBytes(image);
            if (imageData == null || imageData.Length == 0)
            {
                return null;
            }

            var clip = new Clip
            {
                Id = Guid.NewGuid(),
                Type = ClipType.Image,
                ImageData = imageData,
                CapturedAt = DateTime.UtcNow,
                TextContent = $"[Image: {image.PixelWidth}x{image.PixelHeight}]",
                // Generate content hash from image data
                ContentHash = ContentHasher.HashBytes(imageData),
                // Size in bytes
                Size = imageData.Length,
                // Title for image
                Title = $"Image {image.PixelWidth}×{image.PixelHeight}", // For search/display
            };

            _logger.LogDebug("Captured image: {Width}x{Height}, {Size} bytes",
                image.PixelWidth, image.PixelHeight, imageData.Length);

            return clip;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting image from clipboard");
            return null;
        }
    }

    /// <summary>
    ///     Extracts file list clipboard content (CF_HDROP = 15).
    /// </summary>
    private Clip? ExtractFilesClip()
    {
        try
        {
            if (!WpfClipboard.ContainsFileDropList())
            {
                return null;
            }

            var fileDropList = WpfClipboard.GetFileDropList();
            if (fileDropList.Count == 0)
            {
                return null;
            }

            // Convert to string array and serialize to JSON
            var filePaths = new List<string>();
            foreach (var path in fileDropList)
            {
                if (!string.IsNullOrEmpty(path))
                {
                    filePaths.Add(path);
                }
            }

            if (filePaths.Count == 0)
            {
                return null;
            }

            var filePathsJson = JsonSerializer.Serialize(filePaths);

            var clip = new Clip
            {
                Id = Guid.NewGuid(),
                Type = ClipType.Files,
                FilePathsJson = filePathsJson,
                TextContent = string.Join(Environment.NewLine, filePaths), // For search
                CapturedAt = DateTime.UtcNow,
                // Generate content hash from file paths
                ContentHash = ContentHasher.HashText(filePathsJson),
                // Size is just the JSON length (actual file sizes not counted)
                Size = filePathsJson.Length * 2, // Unicode = 2 bytes per char
                // Title shows file count
                Title = filePaths.Count == 1
                    ? Path.GetFileName(filePaths[0])
                    : $"{filePaths.Count} files",
            };

            _logger.LogDebug("Captured {Count} files", filePaths.Count);

            return clip;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting files from clipboard");
            return null;
        }
    }

    /// <summary>
    ///     Populates standard metadata fields on a clip.
    /// </summary>
    private void PopulateStandardFields(Clip clip)
    {
        // Calculate checksum (simple hash for duplicate detection)
        clip.Checksum = clip.ContentHash.GetHashCode();

        // Set view tab based on type
        clip.ViewTab = clip.Type switch
        {
            ClipType.Text => 0,
            ClipType.RichText => 1,
            ClipType.Html => 2,
            _ => 0,
        };

        // Default locale
        clip.Locale = CultureInfo.CurrentCulture.LCID;

        // Default flags
        clip.Encrypted = false;
        clip.Macro = false;
        clip.Del = false;

        // Timestamps
        clip.LastModified = clip.CapturedAt;

        // Creator (username@workstation)
        clip.Creator = $"{Environment.UserName}@{Environment.MachineName}";

        _logger.LogDebug("Clip populated: Type={Type}, Size={Size}, Title={Title}",
            clip.Type, clip.Size, clip.Title);
    }

    /// <summary>
    ///     Calculates total size of text-based clip (all formats combined).
    /// </summary>
    private static int CalculateTextSize(Clip clip)
    {
        var size = 0;
        if (!string.IsNullOrEmpty(clip.TextContent))
        {
            size += clip.TextContent.Length * 2; // Unicode
        }

        if (!string.IsNullOrEmpty(clip.RtfContent))
        {
            size += clip.RtfContent.Length * 2;
        }

        if (!string.IsNullOrEmpty(clip.HtmlContent))
        {
            size += clip.HtmlContent.Length * 2;
        }

        return size;
    }

    /// <summary>
    ///     Generates a title from the first line of text (max 60 chars).
    /// </summary>
    private static string GenerateTitleFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "(Empty)";
        }

        // Get first line
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var firstLine = lines.Length > 0
            ? lines[0].Trim()
            : text.Trim();

        // Truncate to 60 chars (database field limit)
        const int maxLength = 60;
        if (firstLine.Length > maxLength)
        {
            return string.Concat(firstLine.AsSpan(0, maxLength - 3), "...");
        }

        return firstLine;
    }

    /// <summary>
    ///     Converts BitmapSource to PNG byte array for storage.
    /// </summary>
    private byte[]? ConvertBitmapSourceToBytes(BitmapSource image)
    {
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));

            using var memoryStream = new MemoryStream();
            encoder.Save(memoryStream);
            return memoryStream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting image to bytes");
            return null;
        }
    }

    private void SetTextToClipboard(Clip clip)
    {
        try
        {
            var dataObject = new WpfDataObject();

            // Set plain text (always)
            if (!string.IsNullOrEmpty(clip.TextContent))
            {
                dataObject.SetText(clip.TextContent);
            }

            // Set RTF if available
            if (!string.IsNullOrEmpty(clip.RtfContent))
            {
                dataObject.SetData(DataFormats.Rtf, clip.RtfContent);
            }

            // Set HTML if available
            if (!string.IsNullOrEmpty(clip.HtmlContent))
            {
                dataObject.SetData(DataFormats.Html, clip.HtmlContent);
            }

            WpfClipboard.SetDataObject(dataObject, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting text to clipboard");
            throw;
        }
    }

    private void SetImageToClipboard(Clip clip)
    {
        try
        {
            if (clip.ImageData == null || clip.ImageData.Length == 0)
            {
                throw new InvalidOperationException("Image data is empty");
            }

            // Convert byte array back to BitmapSource
            using var memoryStream = new MemoryStream(clip.ImageData);
            var decoder = BitmapDecoder.Create(memoryStream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            if (decoder.Frames.Count > 0)
            {
                WpfClipboard.SetImage(decoder.Frames[0]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting image to clipboard");
            throw;
        }
    }

    private void SetFilesToClipboard(Clip clip)
    {
        try
        {
            if (string.IsNullOrEmpty(clip.FilePathsJson))
            {
                throw new InvalidOperationException("File paths are empty");
            }

            var filePaths = JsonSerializer.Deserialize<List<string>>(clip.FilePathsJson);
            if (filePaths == null || filePaths.Count == 0)
            {
                throw new InvalidOperationException("No file paths found");
            }

            var fileDropList = new StringCollection();
            fileDropList.AddRange(filePaths.ToArray());

            WpfClipboard.SetFileDropList(fileDropList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting files to clipboard");
            throw;
        }
    }

    /// <summary>
    ///     Extracts the source URL from HTML clipboard format.
    ///     HTML clipboard format includes metadata headers like "SourceURL:https://..."
    /// </summary>
    private string? ExtractSourceUrlFromHtml(string htmlContent)
    {
        try
        {
            if (string.IsNullOrEmpty(htmlContent))
            {
                return null;
            }

            // Look for SourceURL: in the header
            const string sourceUrlMarker = "SourceURL:";
            var sourceUrlIndex = htmlContent.IndexOf(sourceUrlMarker, StringComparison.OrdinalIgnoreCase);

            if (sourceUrlIndex == -1)
            {
                return null;
            }

            // Extract URL from the line
            var urlStart = sourceUrlIndex + sourceUrlMarker.Length;
            var urlEnd = htmlContent.IndexOfAny(['\r', '\n'], urlStart);

            if (urlEnd == -1)
            {
                urlEnd = htmlContent.Length;
            }

            var url = htmlContent[urlStart..urlEnd].Trim();

            // Accept any non-empty value from SourceURL field
            // Applications put various URL schemes here (http, https, file, vscode-file, etc.)
            if (!string.IsNullOrEmpty(url))
            {
                // Truncate to 250 chars (database field limit)
                if (url.Length > 250)
                {
                    url = url[..250];
                }

                _logger.LogDebug("Extracted source URL: {Url}", url);
                return url;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to extract source URL from HTML");
            return null;
        }
    }

    private unsafe string? GetProcessName(HWND hwnd)
    {
        try
        {
            uint processId = 0;
            _ = PInvoke.GetWindowThreadProcessId(hwnd, &processId);

            if (processId != 0)
            {
                var process = Process.GetProcessById((int)processId);
                return process.ProcessName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get process name");
        }

        return null;
    }

    private unsafe string? GetWindowTitle(HWND hwnd)
    {
        try
        {
            var length = PInvoke.GetWindowTextLength(hwnd);
            if (length == 0)
            {
                return null;
            }

            var buffer = new char[length + 1];
            fixed (char* pBuffer = buffer)
            {
                var result = PInvoke.GetWindowText(hwnd, pBuffer, length + 1);
                if (result > 0)
                {
                    return new string(buffer, 0, result);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get window title");
        }

        return null;
    }
}
