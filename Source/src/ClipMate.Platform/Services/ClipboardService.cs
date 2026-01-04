using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Win32;
using Windows.Win32.Foundation;
using ClipMate.Core.Exceptions;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Platform.Helpers;
using ClipMate.Platform.Interop;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;
using DataFormats = System.Windows.DataFormats;
// Aliases to resolve WPF vs WinForms ambiguity
using WpfClipboard = System.Windows.Clipboard;
using WpfDataObject = System.Windows.DataObject;

namespace ClipMate.Platform.Services;

/// <summary>
/// Service for monitoring and capturing clipboard changes using Win32 APIs via CsWin32.
/// Captures all formats: Text, RTF, HTML, Images, and Files.
/// Uses a channel-based pattern for publishing captured clips.
/// </summary>
public class ClipboardService : IClipboardService, IDisposable
{
    private const int _channelCapacity = 100; // Max queued clips before backpressure
    private readonly IApplicationProfileService _applicationProfileService;
    private readonly IClipboardFormatEnumerator _clipboardFormatEnumerator;
    private readonly Channel<Clip> _clipsChannel;

    private readonly ILogger<ClipboardService> _logger;
    private readonly ISoundService _soundService;
    private readonly IWin32ClipboardInterop _win32;
    private HwndSource? _hwndSource;
    private DateTime _lastClipboardChange = DateTime.MinValue;
    private bool _lastClipboardWasEmpty;
    private string _lastContentHash = string.Empty;
    private string? _suppressCaptureForHash;
    private DateTime _suppressCaptureUntil = DateTime.MinValue;

    public ClipboardService(ILogger<ClipboardService> logger,
        IWin32ClipboardInterop win32Interop,
        IApplicationProfileService applicationProfileService,
        IClipboardFormatEnumerator clipboardFormatEnumerator,
        ISoundService soundService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _win32 = win32Interop ?? throw new ArgumentNullException(nameof(win32Interop));
        _applicationProfileService = applicationProfileService ?? throw new ArgumentNullException(nameof(applicationProfileService));
        _clipboardFormatEnumerator = clipboardFormatEnumerator ?? throw new ArgumentNullException(nameof(clipboardFormatEnumerator));
        _soundService = soundService ?? throw new ArgumentNullException(nameof(soundService));

        // Create bounded channel with drop oldest policy to prevent memory issues
        _clipsChannel = Channel.CreateBounded<Clip>(new BoundedChannelOptions(_channelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true, // Only clipboard monitor writes
            SingleReader = false, // Multiple consumers allowed
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
            if (!_win32.AddClipboardFormatListener(hwnd))
            {
                var error = Marshal.GetLastWin32Error();

                throw new ClipboardException($"Failed to register clipboard listener. Error: {error}");
            }

            IsMonitoring = true;
            _logger.LogInformation("Clipboard monitoring started");
        }
        catch (InvalidOperationException ex)
        {
            // HwndSource requires STA thread with dispatcher (not available in tests)
            _logger.LogWarning(ex, "Cannot start clipboard monitoring: No dispatcher available (not on UI thread)");
            // For testing purposes, mark as monitoring without actual window
            IsMonitoring = true;

            return Task.CompletedTask;
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
            return Task.CompletedTask;

        try
        {
            if (_hwndSource != null)
            {
                var hwnd = new HWND(_hwndSource.Handle);
                _win32.RemoveClipboardFormatListener(hwnd);

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
            // Get source application info
            var foregroundWindow = _win32.GetForegroundWindow();
            string? applicationName = null;
            if (!foregroundWindow.IsNull)
                applicationName = GetProcessName(foregroundWindow);

            var task = Application.Current.Dispatcher.InvokeAsync(async () =>
                await ExtractClipboardDataAsync(applicationName, cancellationToken));

            return await await task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current clipboard content");

            return null;
        }
    }

    /// <inheritdoc />
    public async Task SetClipboardContentAsync(Clip clip, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clip);

        try
        {
            // Must run on STA thread (UI thread) for WPF Clipboard API
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // WPF Clipboard API handles OpenClipboard/CloseClipboard internally
                switch (clip.Type)
                {
                    case ClipType.Text:
                    case ClipType.Html:
                    case ClipType.RichText:
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

                // Suppress capture of this specific content by storing its hash
                // IMPORTANT: Set this AFTER successfully modifying the clipboard, not before
                // Use the clip's existing ContentHash - it's already computed for all clip types
                _suppressCaptureForHash = clip.ContentHash;
                _suppressCaptureUntil = DateTime.UtcNow.AddMilliseconds(500);

                var hashPreview = _suppressCaptureForHash.Length >= 8
                    ? _suppressCaptureForHash.Substring(0, 8)
                    : _suppressCaptureForHash;

                _logger.LogDebug("Set clipboard suppression hash: {Hash} for clip type {Type}",
                    hashPreview, clip.Type);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting clipboard content");

            throw new ClipboardException("Failed to set clipboard content", ex);
        }
    }

    public void Dispose() => StopMonitoringAsync().Wait();

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
            // Use 150ms to filter out rapid-fire WM_CLIPBOARDUPDATE events from apps
            // that write clipboard formats sequentially (like SnagitEditor)
            var now = DateTime.UtcNow;

            if ((now - _lastClipboardChange).TotalMilliseconds < 150)
                return;

            _lastClipboardChange = now;

            var clip = await GetCurrentClipboardContentAsync();

            // Check if clipboard is empty (no formats we support)
            var isClipboardEmpty = clip == null && IsClipboardTrulyEmpty();

            if (isClipboardEmpty)
            {
                // Clipboard was cleared - check if we should play the erase sound
                // Only play if: 1) We didn't just clear it, and 2) It previously had content
                var shouldPlayEraseSound = !_lastClipboardWasEmpty &&
                                           DateTime.UtcNow >= _suppressCaptureUntil;

                if (shouldPlayEraseSound)
                {
                    _logger.LogDebug("Clipboard was cleared by external application");
                    await _soundService.PlaySoundAsync(SoundEvent.Erase);
                }

                _lastClipboardWasEmpty = true;
                _lastContentHash = string.Empty;
                return;
            }

            if (clip == null)
            {
                // Clipboard has content but not in a format we support
                _lastClipboardWasEmpty = false;
                return;
            }

            // Clipboard has content we captured
            _lastClipboardWasEmpty = false;

            // Update timestamp AFTER getting clipboard content to prevent duplicate processing
            // if multiple WM_CLIPBOARDUPDATE events fire simultaneously
            _lastClipboardChange = DateTime.UtcNow;

            // Check if we should suppress this capture (we set the clipboard programmatically)
            // Use time-based suppression window to handle multiple clipboard events
            if (_suppressCaptureForHash != null &&
                clip.ContentHash == _suppressCaptureForHash &&
                DateTime.UtcNow < _suppressCaptureUntil)
            {
                var hashPreview = clip.ContentHash.Length >= 8
                    ? clip.ContentHash.Substring(0, 8)
                    : clip.ContentHash;

                _logger.LogDebug("Suppressing clipboard capture - content was set programmatically (hash: {Hash})", hashPreview);
                return;
            }

            // Clear suppression if time window has expired
            if (DateTime.UtcNow >= _suppressCaptureUntil)
                _suppressCaptureForHash = null;

            // Duplicate detection: ignore if same content hash
            if (clip.ContentHash == _lastContentHash)
            {
                _logger.LogDebug("Ignoring duplicate clipboard content");

                // Only play sound if this is a user-initiated duplicate (not rapid-fire events from slow clipboard owners)
                // If duplicate arrives >200ms after last change, it's likely intentional (user copied same thing twice)
                var timeSinceLastChange = (DateTime.UtcNow - _lastClipboardChange).TotalMilliseconds;
                if (timeSinceLastChange > 200)
                    await _soundService.PlaySoundAsync(SoundEvent.Ignore);

                return;
            }

            _lastContentHash = clip.ContentHash;
            // Get source application info
            var foregroundWindow = _win32.GetForegroundWindow();
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
    /// Extracts all available clipboard data in priority order with application profile filtering.
    /// Priority: Text (including RTF/HTML) > Images > Files
    /// </summary>
    private async Task<Clip?> ExtractClipboardDataAsync(string? applicationName = null, CancellationToken cancellationToken = default)
    {
        Clip? clip = null;

        // If application profiles are enabled, filter formats
        HashSet<string>? allowedFormats = null;
        if (_applicationProfileService.IsApplicationProfilesEnabled() && !string.IsNullOrEmpty(applicationName))
        {
            // Enumerate all available clipboard formats
            var availableFormats = _clipboardFormatEnumerator.GetAllAvailableFormats();
            _logger.LogDebug("Clipboard has {Count} formats available for application {AppName}",
                availableFormats.Count, applicationName);

            // Filter formats based on application profile
            allowedFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var format in availableFormats)
            {
                var shouldCapture = await _applicationProfileService.ShouldCaptureFormatAsync(
                    applicationName, format.FormatName, cancellationToken);

                if (shouldCapture)
                {
                    allowedFormats.Add(format.FormatName);
                    _logger.LogDebug("Format {FormatName} allowed for {AppName}", format.FormatName, applicationName);
                }
                else
                    _logger.LogDebug("Format {FormatName} filtered out for {AppName}", format.FormatName, applicationName);
            }

            if (allowedFormats.Count == 0)
            {
                _logger.LogDebug("All clipboard formats filtered out for {AppName}", applicationName);

                return null;
            }
        }

        // Check what formats are available (considering profile filtering if enabled)
        var hasText = IsFormatAllowed(Formats.UnicodeText.Name, allowedFormats) && WpfClipboard.ContainsText();
        var hasRtf = IsFormatAllowed(Formats.RichText.Name, allowedFormats) && WpfClipboard.ContainsData(DataFormats.Rtf);
        var hasHtml = IsFormatAllowed(Formats.Html.Name, allowedFormats) && WpfClipboard.ContainsData(DataFormats.Html);
        var hasImage = (IsFormatAllowed(Formats.Dib.Name, allowedFormats) || IsFormatAllowed(Formats.Bitmap.Name, allowedFormats)) && WpfClipboard.ContainsImage();
        var hasFiles = IsFormatAllowed(Formats.HDrop.Name, allowedFormats) && WpfClipboard.ContainsFileDropList();

        // Priority 1: Text formats (includes RTF and HTML)
        if (hasText || hasRtf || hasHtml)
            clip = ExtractTextClip(hasText, hasRtf, hasHtml);
        // Priority 2: Images
        else if (hasImage)
            clip = ExtractImageClip();
        // Priority 3: Files
        else if (hasFiles)
            clip = ExtractFilesClip();

        // Populate standard fields
        if (clip != null)
            PopulateStandardFields(clip);

        return clip;
    }

    /// <summary>
    /// Checks if a clipboard format is allowed based on application profile filtering.
    /// If no filtering is active (allowedFormats is null), all formats are allowed.
    /// Handles both "CF_" prefixed format names (from enumeration) and non-prefixed names (from constants).
    /// </summary>
    private static bool IsFormatAllowed(string formatName, HashSet<string>? allowedFormats)
    {
        if (allowedFormats == null)
            return true;

        // Check exact match first
        if (allowedFormats.Contains(formatName))
            return true;

        // Check with CF_ prefix added (for backward compatibility with old profiles)
        if (allowedFormats.Contains($"CF_{formatName}"))
            return true;

        // Check with CF_ prefix removed (for checking constants against enumerated names)
        if (formatName.StartsWith("CF_", StringComparison.Ordinal) && allowedFormats.Contains(formatName[3..]))
            return true;

        return false;
    }


    /// <summary>
    /// Extracts text-based clipboard content (Plain Text, RTF, HTML) based on allowed formats.
    /// </summary>
    private Clip? ExtractTextClip(bool extractText = true, bool extractRtf = true, bool extractHtml = true)
    {
        try
        {
            var clip = new Clip
            {
                Id = Guid.NewGuid(),
                Type = ClipType.Text,
                CapturedAt = DateTimeOffset.Now,
            };

            // Extract Plain Text (CF_UNICODETEXT = 13)
            if (extractText && WpfClipboard.ContainsText())
                clip.TextContent = WpfClipboard.GetText();

            // Extract RTF (CF_RTF)
            if (extractRtf && WpfClipboard.ContainsData(DataFormats.Rtf))
            {
                clip.RtfContent = WpfClipboard.GetData(DataFormats.Rtf) as string;
                if (!string.IsNullOrEmpty(clip.RtfContent))
                    clip.Type = ClipType.RichText;
            }

            // Extract HTML (CF_HTML) and Source URL
            if (extractHtml && WpfClipboard.ContainsData(DataFormats.Html))
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
                return null;

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
    /// Extracts image clipboard content, converting to proper PNG or JPEG format.
    /// </summary>
    private Clip? ExtractImageClip()
    {
        try
        {
            if (!WpfClipboard.ContainsImage())
                return null;

            // Check if PNG format is available directly in the clipboard
            // If PNG exists, use it directly to preserve transparency
            var hasPngFormat = WpfClipboard.ContainsData("PNG");
            byte[]? pngData = null;

            if (hasPngFormat)
            {
                try
                {
                    if (WpfClipboard.GetData("PNG") is MemoryStream pngStream)
                    {
                        pngData = pngStream.ToArray();
                        _logger.LogDebug("Found PNG format in clipboard: {Size} bytes", pngData.Length);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to retrieve PNG data from clipboard, falling back to BitmapSource");
                }
            }

            // If we have valid PNG data, use it directly
            if (pngData is { Length: > 0 } && DetectImageFormatFromBytes(pngData) == "PNG")
            {
                _logger.LogInformation("Using PNG format directly from clipboard - preserving transparency");

                // Decode PNG to get dimensions for the title
                using var pngStream = new MemoryStream(pngData);
                var decoder = BitmapDecoder.Create(
                    pngStream,
                    BitmapCreateOptions.DelayCreation,
                    BitmapCacheOption.None);

                var frame = decoder.Frames[0];

                var pngClip = new Clip
                {
                    Id = Guid.NewGuid(),
                    Type = ClipType.Image,
                    ImageData = pngData,
                    CapturedAt = DateTimeOffset.Now,
                    ContentHash = ContentHasher.HashBytes(pngData),
                    Size = pngData.Length,
                    Title = $"Image {frame.PixelWidth}×{frame.PixelHeight}",
                };

                _logger.LogDebug("Captured PNG image from clipboard PNG format: {Width}x{Height}, {Size} bytes",
                    frame.PixelWidth, frame.PixelHeight, pngData.Length);

                return pngClip;
            }

            // No PNG format or failed to retrieve it - use standard BitmapSource approach
            // This will be InteropBitmap for screenshots (needs alpha fix)
            var image = WpfClipboard.GetImage();

            if (image == null)
                return null;

            // InteropBitmap is specifically used for DIB/DIBv5 clipboard formats (screenshots, screen captures)
            // These have the known alpha=0 bug.
            var isInteropBitmap = image is InteropBitmap;

            _logger.LogDebug("Extracting image from clipboard: {Width}x{Height}, Format: {Format}, DpiX: {DpiX}, DpiY: {DpiY}, Type: {BitmapType}, IsInteropBitmap: {IsInterop}",
                image.PixelWidth, image.PixelHeight, image.Format, image.DpiX, image.DpiY, image.GetType().Name, isInteropBitmap);

            // Convert BitmapSource to byte array
            // Pass whether this is an InteropBitmap (which needs alpha channel fixing)
            var imageData = ConvertBitmapSourceToBytes(image, isInteropBitmap);
            if (imageData == null || imageData.Length == 0)
            {
                _logger.LogError("Failed to convert bitmap to bytes");

                return null;
            }

            // Verify the generated image is valid by checking magic bytes
            var detectedFormat = DetectImageFormatFromBytes(imageData);
            _logger.LogDebug("Generated image format: {Format}, Size: {Size} bytes", detectedFormat, imageData.Length);

            if (detectedFormat == "Unknown")
            {
                _logger.LogError("Generated image data is not a valid PNG. First 16 bytes: {Bytes}",
                    BitConverter.ToString(imageData.Take(16).ToArray()));

                return null;
            }

            var clip = new Clip
            {
                Id = Guid.NewGuid(),
                Type = ClipType.Image,
                ImageData = imageData,
                CapturedAt = DateTimeOffset.Now,
                // Don't set TextContent for image-only clips to avoid storing unnecessary CF_UNICODETEXT format
                ContentHash = ContentHasher.HashBytes(imageData),
                Size = imageData.Length,
                Title = $"Image {image.PixelWidth}×{image.PixelHeight}",
            };

            _logger.LogDebug("Captured PNG image: {Width}x{Height}, {Size} bytes",
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
    /// Detects image format from byte array.
    /// </summary>
    private string DetectImageFormatFromBytes(byte[] imageData)
    {
        if (imageData.Length < 8)
            return "Unknown";

        // Check PNG signature: 89 50 4E 47 0D 0A 1A 0A
        if (imageData[0] == 0x89 && imageData[1] == 0x50 && imageData[2] == 0x4E && imageData[3] == 0x47 &&
            imageData[4] == 0x0D && imageData[5] == 0x0A && imageData[6] == 0x1A && imageData[7] == 0x0A)
            return "PNG";

        // Check JPEG signature: FF D8 FF
        if (imageData[0] == 0xFF && imageData[1] == 0xD8 && imageData[2] == 0xFF)
            return "JPEG";

        return "Unknown";
    }

    /// <summary>
    /// Extracts file list clipboard content (CF_HDROP = 15).
    /// </summary>
    private Clip? ExtractFilesClip()
    {
        try
        {
            if (!WpfClipboard.ContainsFileDropList())
                return null;

            var fileDropList = WpfClipboard.GetFileDropList();

            if (fileDropList.Count == 0)
                return null;

            // Convert to string array and serialize to JSON
            var filePaths = new List<string>();
            foreach (var path in fileDropList)
            {
                if (!string.IsNullOrEmpty(path))
                    filePaths.Add(path);
            }

            if (filePaths.Count == 0)
                return null;

            var filePathsJson = JsonSerializer.Serialize(filePaths);

            var clip = new Clip
            {
                Id = Guid.NewGuid(),
                Type = ClipType.Files,
                FilePathsJson = filePathsJson,
                TextContent = string.Join(Environment.NewLine, filePaths), // For search
                CapturedAt = DateTimeOffset.Now,
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
    /// Populates standard metadata fields on a clip.
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
            var _ => 0,
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
    /// Calculates total size of text-based clip (all formats combined).
    /// </summary>
    private static int CalculateTextSize(Clip clip)
    {
        var size = 0;
        if (!string.IsNullOrEmpty(clip.TextContent))
            size += clip.TextContent.Length * 2; // Unicode

        if (!string.IsNullOrEmpty(clip.RtfContent))
            size += clip.RtfContent.Length * 2;

        if (!string.IsNullOrEmpty(clip.HtmlContent))
            size += clip.HtmlContent.Length * 2;

        return size;
    }

    /// <summary>
    /// Generates a title from the first line of text (max 60 chars).
    /// </summary>
    private static string GenerateTitleFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "(Empty)";

        // Get first line
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var firstLine = lines.Length > 0
            ? lines[0].Trim()
            : text.Trim();

        // Truncate to 60 chars (database field limit)
        const int maxLength = 60;

        return firstLine.Length > maxLength
            ? string.Concat(firstLine.AsSpan(0, maxLength - 3), "...")
            : firstLine;
    }

    /// <summary>
    /// Converts BitmapSource to PNG byte array for storage.
    /// </summary>
    /// <param name="image">The BitmapSource to convert</param>
    /// <param name="isInteropBitmap">
    /// True if the image is an InteropBitmap (DIB format from screenshots), which has the
    /// alpha=0 bug
    /// </param>
    private byte[]? ConvertBitmapSourceToBytes(BitmapSource image, bool isInteropBitmap)
    {
        try
        {
            // Calculate the stride (bytes per row)
            var stride = (image.PixelWidth * image.Format.BitsPerPixel + 7) / 8;
            var pixelData = new byte[stride * image.PixelHeight];

            // Copy pixel data from the source image
            image.CopyPixels(pixelData, stride, 0);

            // Fix alpha channel transparency bug ONLY for InteropBitmap sources
            // InteropBitmap is used for DIB/DIBv5 (Device Independent Bitmap) from Windows screenshots
            // which often incorrectly sets alpha=0 for all pixels even though the image is opaque.
            // Other bitmap types (from PNG files, etc.) should preserve transparency exactly as-is.
            if (isInteropBitmap &&
                (image.Format == PixelFormats.Bgra32 ||
                 image.Format == PixelFormats.Pbgra32))
            {
                // Check if any alpha bytes are non-zero
                var hasNonZeroAlpha = false;
                for (var i = 3; i < pixelData.Length; i += 4)
                {
                    if (pixelData[i] == 0)
                        continue;

                    hasNonZeroAlpha = true;

                    break;
                }

                // If ALL alpha bytes are 0, this is the InteropBitmap transparency bug
                // Fix by setting all alpha to 255 (fully opaque)
                if (!hasNonZeroAlpha)
                {
                    // Check if RGB channels have data (not a truly blank image)
                    var hasColorData = false;
                    for (var i = 0; i < Math.Min(1000 * 4, pixelData.Length) && !hasColorData; i++)
                    {
                        if (i % 4 != 3 && pixelData[i] != 0) // Skip alpha channel, check B, G, R
                            hasColorData = true;
                    }

                    if (hasColorData)
                    {
                        // This is the InteropBitmap transparency bug - image has color but all alpha=0
                        // Fix by setting all alpha to 255 (opaque)
                        for (var i = 3; i < pixelData.Length; i += 4)
                            pixelData[i] = 255;

                        _logger.LogInformation("Fixed InteropBitmap transparency bug: all alpha was 0, set to 255 (opaque)");
                    }
                }
            }

            // Create a new WriteableBitmap from the pixel data
            var writableBitmap = new WriteableBitmap(
                image.PixelWidth,
                image.PixelHeight,
                image.DpiX,
                image.DpiY,
                image.Format,
                image.Palette);

            // Write the pixel data to the writable bitmap
            writableBitmap.WritePixels(
                new Int32Rect(0, 0, image.PixelWidth, image.PixelHeight),
                pixelData,
                stride,
                0);

            writableBitmap.Freeze();

            // Encode to PNG
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(writableBitmap));

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
                dataObject.SetText(clip.TextContent);

            // Set RTF if available
            if (!string.IsNullOrEmpty(clip.RtfContent))
                dataObject.SetData(DataFormats.Rtf, clip.RtfContent);

            // Set HTML if available
            if (!string.IsNullOrEmpty(clip.HtmlContent))
                dataObject.SetData(DataFormats.Html, clip.HtmlContent);

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
                throw new InvalidOperationException("Image data is empty");

            // Convert byte array back to BitmapSource
            using var memoryStream = new MemoryStream(clip.ImageData);
            var decoder = BitmapDecoder.Create(memoryStream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            if (decoder.Frames.Count <= 0)
                return;

            // Create a writable copy to ensure pixel data is fully accessible
            // This prevents issues when pasting into some applications
            var writableBitmap = new WriteableBitmap(decoder.Frames[0]);
            writableBitmap.Freeze(); // Make immutable for clipboard

            WpfClipboard.SetImage(writableBitmap);
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
                throw new InvalidOperationException("File paths are empty");

            var filePaths = JsonSerializer.Deserialize<List<string>>(clip.FilePathsJson);

            if (filePaths == null || filePaths.Count == 0)
                throw new InvalidOperationException("No file paths found");

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
    /// Extracts the source URL from HTML clipboard format.
    /// HTML clipboard format includes metadata headers like "SourceURL:https://..."
    /// </summary>
    private string? ExtractSourceUrlFromHtml(string htmlContent)
    {
        try
        {
            if (string.IsNullOrEmpty(htmlContent))
                return null;

            // Look for SourceURL: in the header
            const string sourceUrlMarker = "SourceURL:";
            var sourceUrlIndex = htmlContent.IndexOf(sourceUrlMarker, StringComparison.OrdinalIgnoreCase);

            if (sourceUrlIndex == -1)
                return null;

            // Extract URL from the line
            var urlStart = sourceUrlIndex + sourceUrlMarker.Length;
            var urlEnd = htmlContent.IndexOfAny(['\r', '\n'], urlStart);

            if (urlEnd == -1)
                urlEnd = htmlContent.Length;

            var url = htmlContent[urlStart..urlEnd].Trim();

            // Accept any non-empty value from SourceURL field
            // Applications put various URL schemes here (http, https, file, vscode-file, etc.)
            if (string.IsNullOrEmpty(url))
                return null;

            // Truncate to 250 chars (database field limit)
            if (url.Length > 250)
                url = url[..250];

            _logger.LogDebug("Extracted source URL: {Url}", url);

            return url;
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
                var processName = process.ProcessName;

                // Don't capture profiles for ClipMate itself
                return processName.Equals("ClipMate.App", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : processName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get process name");
        }

        return null;
    }

    /// <summary>
    /// Checks if the clipboard is truly empty (no formats available).
    /// Returns true if clipboard has no data at all, false if it has any formats.
    /// </summary>
    private bool IsClipboardTrulyEmpty()
    {
        try
        {
            return Application.Current.Dispatcher.Invoke(() =>
            {
                // Check all common formats
                if (WpfClipboard.ContainsText())
                    return false;

                if (WpfClipboard.ContainsImage())
                    return false;

                if (WpfClipboard.ContainsFileDropList())
                    return false;

                if (WpfClipboard.ContainsData(DataFormats.Rtf))
                    return false;

                if (WpfClipboard.ContainsData(DataFormats.Html))
                    return false;

                if (WpfClipboard.ContainsAudio())
                    return false;

                // Check if any formats exist at all using format enumerator
                var formats = _clipboardFormatEnumerator.GetAllAvailableFormats();
                return formats.Count == 0;
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking if clipboard is empty");
            return false; // Assume not empty if we can't check
        }
    }

    private unsafe string? GetWindowTitle(HWND hwnd)
    {
        try
        {
            var length = _win32.GetWindowTextLength(hwnd);

            if (length == 0)
                return null;

            var buffer = new char[length + 1];
            fixed (char* pBuffer = buffer)
            {
                var result = _win32.GetWindowText(hwnd, pBuffer, length + 1);

                if (result > 0)
                    return new string(buffer, 0, result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get window title");
        }

        return null;
    }
}
