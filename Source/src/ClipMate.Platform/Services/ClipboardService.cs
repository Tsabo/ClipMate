using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Channels;
using System.Windows.Media.Imaging;
using Windows.Win32.Foundation;
using ClipMate.Core.Events;
using ClipMate.Core.Exceptions;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Platform.Helpers;
using ClipMate.Platform.Interop;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using ThrottleDebounce.Retry;
using DataFormats = System.Windows.DataFormats;
// Aliases to resolve WPF vs WinForms ambiguity
using WpfDataObject = System.Windows.DataObject;

namespace ClipMate.Platform.Services;

/// <summary>
/// Service for monitoring and capturing clipboard changes using Win32 APIs via CsWin32.
/// Each change is read raw in one clipboard session on the clipboard thread, then decoded into a clip on the thread
/// pool and published through a channel.
/// </summary>
public class ClipboardService : IClipboardService, IDisposable
{
    private const int _channelCapacity = 100; // Max queued clips before backpressure

    /// <summary>
    /// The Source Application of a change nothing owns and that has no text.
    /// </summary>
    public const string NoOwnerApplication = "WINDOWS";
    private readonly IDecryptedBlobCacheService _blobCacheService;
    private readonly Channel<Clip> _clipsChannel;
    private readonly Lock _clipboardThreadLock = new();
    private readonly IConfigurationService _configurationService;
    private readonly Task _decodeLoop;
    private readonly ClipboardSnapshotDecoder _decoder;
    private readonly ILogger<ClipboardService> _logger;
    private readonly IMessenger _messenger;
    private readonly Channel<PendingCapture> _pendingCaptures;
    private readonly RawClipboardReader _reader;
    private readonly ISoundService _soundService;
    private readonly TimeProvider _timeProvider;
    private readonly IWin32ClipboardInterop _win32;
    // Set on the UI thread (Power Paste), read on the clipboard thread
    private volatile bool _captureSuspended;
    private ClipboardCaptureScheduler? _captureScheduler;
    private Task<ClipboardThread>? _clipboardThread;
    private bool _disposed;

    public ClipboardService(ILogger<ClipboardService> logger,
        IWin32ClipboardInterop win32Interop,
        IConfigurationService configurationService,
        IApplicationProfileService applicationProfileService,
        ISoundService soundService,
        IDecryptedBlobCacheService blobCacheService,
        IMessenger messenger,
        TimeProvider? timeProvider = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _win32 = win32Interop ?? throw new ArgumentNullException(nameof(win32Interop));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        ArgumentNullException.ThrowIfNull(applicationProfileService);
        _soundService = soundService ?? throw new ArgumentNullException(nameof(soundService));
        _blobCacheService = blobCacheService ?? throw new ArgumentNullException(nameof(blobCacheService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _reader = new RawClipboardReader(_win32, _timeProvider, _logger);
        _decoder = new ClipboardSnapshotDecoder(_configurationService, applicationProfileService, _logger);

        // Create bounded channel with drop oldest policy to prevent memory issues
        _clipsChannel = Channel.CreateBounded<Clip>(new BoundedChannelOptions(_channelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true, // Only the decode loop writes
            SingleReader = false, // Multiple consumers allowed
        });

        _pendingCaptures = Channel.CreateBounded<PendingCapture>(new BoundedChannelOptions(_channelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = true,
            SingleReader = true,
        });

        _decodeLoop = Task.Run(DecodeCapturesAsync);
    }

    /// <inheritdoc />
    public ChannelReader<Clip> ClipsChannel => _clipsChannel.Reader;

    /// <inheritdoc />
    public bool IsMonitoring { get; private set; }

    /// <inheritdoc />
    public int SettleGapDrops => _captureScheduler?.SettleGapDrops ?? 0;

    /// <inheritdoc />
    public async Task StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        if (IsMonitoring)
        {
            _logger.LogDebug("Clipboard monitoring already active");

            return;
        }

        ClipboardThread? clipboardThread = null;
        try
        {
            clipboardThread = await GetClipboardThreadAsync().ConfigureAwait(false);
            var thread = clipboardThread;
            var (listening, error) = await thread.Dispatcher.InvokeAsync(() =>
            {
                // Lives until Dispose, so its settle-gap drops count from start-up, as ClipMate 7's do
                _captureScheduler ??= new ClipboardCaptureScheduler(_timeProvider,
                    () => TimeSpan.FromMilliseconds(Math.Max(0, _configurationService.Configuration.Preferences.DelayOnClipboardUpdateMs)),
                    () => TimeSpan.FromMilliseconds(Math.Max(0, _configurationService.Configuration.Preferences.SettleTimeMs)),
                    action => thread.Dispatcher.InvokeAsync(action),
                    () => CaptureClipboardChangeAsync(thread.Handle),
                    TraceCapture);
                var added = _win32.AddClipboardFormatListener(thread.Handle);

                return (added, added ? 0 : Marshal.GetLastWin32Error());
            });

            if (!listening)
                throw new ClipboardException($"Failed to register clipboard listener. Error: {error}");

            IsMonitoring = true;
            _logger.LogInformation("Clipboard monitoring started");
        }
        catch (Exception ex)
        {
            if (clipboardThread != null)
                await clipboardThread.Dispatcher.InvokeAsync(DisposeCaptureScheduler);

            _logger.LogError(ex, "Failed to start clipboard monitoring");

            if (ex is ClipboardException)
                throw;

            throw new ClipboardException("Failed to start clipboard monitoring", ex);
        }
    }

    /// <inheritdoc />
    public async Task StopMonitoringAsync()
    {
        if (!IsMonitoring)
            return;

        IsMonitoring = false;

        try
        {
            // The clipboard thread and the channels stay for manual capture and for monitoring to start again
            var clipboardThread = await GetClipboardThreadAsync().ConfigureAwait(false);
            await clipboardThread.Dispatcher.InvokeAsync(StopListening(clipboardThread));

            _logger.LogInformation("Clipboard monitoring stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping clipboard monitoring");
        }
    }

    /// <inheritdoc />
    public async Task<Clip?> GetCurrentClipboardContentAsync(ManualCaptureKind kind = ManualCaptureKind.CaptureHotkey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Reading now would render Power Paste's waiting clip and count as a paste
            if (_captureSuspended)
            {
                _logger.LogDebug("Not reading the clipboard - capture is suspended");

                return null;
            }

            // Same read as a monitored change, without the delay or settle gap
            var honourIgnoreFlag = kind == ManualCaptureKind.Startup || _configurationService.Configuration.Preferences.PayAttentionToClipboardIgnoreFlag;
            var clipboardThread = await GetClipboardThreadAsync();
            var snapshot = await await clipboardThread.Dispatcher.InvokeAsync(() => _reader.ReadAsync(clipboardThread.Handle, honourIgnoreFlag));

            if (snapshot == null)
                return null;

            if (snapshot.IgnoreFlag)
            {
                _logger.LogDebug("Not capturing a clipboard marked to be ignored by clipboard viewers ({Kind})", kind);

                return null;
            }

            if (snapshot.FormatCount == 0)
            {
                if (kind == ManualCaptureKind.CaptureHotkey)
                    await _soundService.PlaySoundAsync(SoundEvent.Erase, cancellationToken);

                return null;
            }

            // ClipMate 7 captures the clipboard at startup only when it has text or an image
            if (snapshot.Formats.Count == 0 || (kind == ManualCaptureKind.Startup && !snapshot.Formats.Any(p => IsTextOrImage(p.Name))))
                return null;

            var source = GetSourceApplication(snapshot);
            var pending = new PendingCapture(snapshot.Formats, source.Name, source.Title);

            return await Task.Run(() => DecodeAsync(pending, kind == ManualCaptureKind.CaptureHotkey, cancellationToken), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current clipboard content");

            return null;
        }
    }

    /// <inheritdoc />
    public async Task<ClipboardReading?> ReadClipboardAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_captureSuspended)
                return null;

            var clipboardThread = await GetClipboardThreadAsync();
            var snapshot = await await clipboardThread.Dispatcher.InvokeAsync(() => _reader.ReadAsync(clipboardThread.Handle, false));
            if (snapshot == null)
                return null;

            var source = GetSourceApplication(snapshot);

            return new ClipboardReading(snapshot.Formats, source.Name, source.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read the clipboard for Capture Special");

            return null;
        }
    }

    /// <inheritdoc />
    public async Task<Clip?> CaptureSpecialAsync(ClipboardReading reading, IReadOnlyCollection<string> formatNames, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(formatNames);

        var chosen = formatNames.Select(ClipboardSnapshotDecoder.ProfileFormat).ToHashSet(StringComparer.Ordinal);
        var formats = reading.Formats.Where(p => chosen.Contains(ClipboardSnapshotDecoder.ProfileFormat(p.Name))).ToList();
        if (formats.Count == 0)
            return null;

        // No Source Application, so no Application Profile applies
        var clip = await Task.Run(() => _decoder.DecodeAsync(formats, null, false, cancellationToken), cancellationToken);
        if (clip == null)
            return null;

        clip.SourceApplicationName = reading.SourceApplicationName;
        clip.SourceApplicationTitle = reading.SourceApplicationTitle;

        return clip;
    }

    /// <inheritdoc />
    public async Task SetClipboardContentAsync(Clip clip, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clip);

        try
        {
            await Retrier.Attempt(
                async attempt =>
                {
                    // WPF's Clipboard needs an STA thread; awaited, so no caller blocks the UI thread waiting for it
                    await UiThread.RunAsync(() =>
                    {
                        // WPF Clipboard API handles OpenClipboard/CloseClipboard internally
                        if (CreateCopy(clip) is not { } dataObject)
                            return;

                        _win32.SetClipboardDataObject(dataObject);

                        // Every path that puts a clip on the clipboard comes here, so the clip becomes the Current Clip
                        _messenger.Send(new ClipboardCopiedEvent(clip));
                    });

                    if (attempt > 0)
                        _logger.LogInformation("Clipboard operation succeeded after {Attempt} retries", attempt);
                },
                new AsyncRetryOptions
                {
                    MaxAttempts = 4, // 1 initial attempt + 3 retries
                    Delay = retryCount => TimeSpan.FromMilliseconds(50 * Math.Pow(2, retryCount)), // 50ms, 100ms, 200ms
                    IsRetryAllowed = (ex, _) => Task.FromResult(ex is COMException { HResult: unchecked((int)0x800401D0) }), // CLIPBRD_E_CANT_OPEN
                    BeforeRetry = (_, _) =>
                    {
                        _logger.LogWarning("Clipboard locked, retrying...");
                        return Task.CompletedTask;
                    },
                    CancellationToken = cancellationToken,
                });
        }
        catch (COMException ex) when (ex.HResult == unchecked((int)0x800401D0))
        {
            _logger.LogError("Failed to set clipboard content after retries - clipboard remains locked by another application");

            throw new ClipboardException("Failed to set clipboard content - clipboard is locked by another application", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting clipboard content");

            throw new ClipboardException("Failed to set clipboard content", ex);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ClipboardFormatData> GetClipboardFormats(Clip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);

        var dataObject = CreateDataObject(clip);
        if (dataObject == null)
            return [];

        var formats = DataObjectFormats.Read(dataObject).ToList();

        // A copy offers its image as a GDI bitmap, which the clipboard keeps as a handle rather than memory; offer the
        // DIB (Windows derives the bitmap from it) so a delayed-rendered copy has the image too
        if (formats.All(p => p.FormatId != (uint)Formats.Dib.Code) && GetPlainImageData(clip) is { } imageData)
        {
            try
            {
                formats.Add(new ClipboardFormatData((uint)Formats.Dib.Code, DataObjectFormats.EncodeDib(imageData)));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not convert the image of clip {ClipId} to a DIB", clip.Id);
            }
        }

        return formats;
    }

    /// <inheritdoc />
    public void SuspendCapture()
    {
        _captureSuspended = true;
        _logger.LogDebug("Clipboard capture suspended");
    }

    /// <inheritdoc />
    public void ResumeCapture()
    {
        _captureSuspended = false;
        _logger.LogDebug("Clipboard capture resumed");
    }

    public void Dispose()
    {
        // Never waits on the clipboard thread: Dispose may run on the UI thread while a read waits for it
        Task<ClipboardThread>? started;
        lock (_clipboardThreadLock)
        {
            _disposed = true;
            started = _clipboardThread;
            _clipboardThread = null;
        }

        IsMonitoring = false;
        started?.ContinueWith(p => p.Result.BeginStop(() =>
        {
            StopListening(p.Result)();
            DisposeCaptureScheduler();
        }), TaskContinuationOptions.OnlyOnRanToCompletion);
        _pendingCaptures.Writer.TryComplete();
        _clipsChannel.Writer.TryComplete();
    }

    // The clipboard thread runs from its first use (monitoring, a manual or a startup capture) until Dispose
    private Task<ClipboardThread> GetClipboardThreadAsync()
    {
        lock (_clipboardThreadLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_clipboardThread is not { IsFaulted: false, IsCanceled: false })
                _clipboardThread = ClipboardThread.StartAsync(WndProc);

            return _clipboardThread;
        }
    }

    private Action StopListening(ClipboardThread clipboardThread) => () =>
    {
        _win32.RemoveClipboardFormatListener(clipboardThread.Handle);
        _captureScheduler?.Cancel();
    };

    private void DisposeCaptureScheduler()
    {
        _captureScheduler?.Dispose();
        _captureScheduler = null;
    }

    // Runs on the clipboard thread
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int wmClipboardUpdate = 0x031D;

        if (msg != wmClipboardUpdate)
            return IntPtr.Zero;

        handled = true;

        if (_captureSuspended)
        {
            _logger.LogDebug("Ignoring clipboard change notification - capture is suspended");

            return IntPtr.Zero;
        }

        _captureScheduler?.OnClipboardUpdate();

        return IntPtr.Zero;
    }

    private void TraceCapture(ClipboardCaptureTrace trace) =>
        _logger.LogDebug("Clipboard capture: {Outcome} after {Notifications} notification(s), {Start}, owner {Owner}",
            trace.Outcome, trace.Notifications, trace.TimerFired ? "delay timer fired" : "no delay", trace.Owner ?? "(none)");

    // Runs on the clipboard thread, started by the capture scheduler; the clipboard is held only for the raw read
    internal async Task<ClipboardCaptureResult> CaptureClipboardChangeAsync(HWND window)
    {
        string? owner = null;
        try
        {
            // Suspension may have started after the notification was scheduled - reading the clipboard here
            // (GetClipboardData) can itself satisfy a delayed-render registration (e.g. PowerPaste's)
            if (_captureSuspended)
                return new ClipboardCaptureResult(ClipboardCaptureOutcome.Suspended, owner);

            var snapshot = await _reader.ReadAsync(window, _configurationService.Configuration.Preferences.PayAttentionToClipboardIgnoreFlag);
            if (snapshot == null)
                return new ClipboardCaptureResult(ClipboardCaptureOutcome.Failed, owner);

            var source = GetSourceApplication(snapshot);
            owner = source.IsClipMate ? "ClipMate" : source.Name;

            // A program that asks clipboard viewers to ignore its change (for example Power Paste re-offering a clip
            // that was already read) is not captured
            if (snapshot.IgnoreFlag)
            {
                _logger.LogInformation("Not capturing a clipboard change marked to be ignored by clipboard viewers");

                return new ClipboardCaptureResult(ClipboardCaptureOutcome.IgnoreFlag, owner);
            }

            if (snapshot.FormatCount == 0)
            {
                await _soundService.PlaySoundAsync(SoundEvent.Erase);

                return new ClipboardCaptureResult(ClipboardCaptureOutcome.Empty, owner);
            }

            if (snapshot.Formats.Count == 0)
                return new ClipboardCaptureResult(ClipboardCaptureOutcome.Unsupported, owner);

            var pending = new PendingCapture(snapshot.Formats, source.Name, source.Title);

            // Decoding, hashing and saving run on the thread pool; the channel drops the oldest read when full
            await _pendingCaptures.Writer.WriteAsync(pending);

            return new ClipboardCaptureResult(ClipboardCaptureOutcome.Captured, owner);
        }
        catch (ChannelClosedException)
        {
            return new ClipboardCaptureResult(ClipboardCaptureOutcome.Failed, owner);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling clipboard change");

            return new ClipboardCaptureResult(ClipboardCaptureOutcome.Failed, owner);
        }
    }

    private async Task DecodeCapturesAsync()
    {
        await foreach (var pending in _pendingCaptures.Reader.ReadAllAsync())
        {
            try
            {
                await DecodeAndPublishAsync(pending);
            }
            catch (ChannelClosedException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decoding a clipboard change");
            }
        }
    }

    /// <summary>
    /// A clipboard read waiting to be decoded, with its Source Application.
    /// </summary>
    private sealed record PendingCapture(IReadOnlyList<RawClipboardFormat> Formats, string? ApplicationName, string? ApplicationTitle);

    private async Task DecodeAndPublishAsync(PendingCapture pending)
    {
        var clip = await DecodeAsync(pending);
        if (clip == null)
        {
            _logger.LogDebug("Clipboard change has nothing to keep");

            return;
        }

        await _clipsChannel.Writer.WriteAsync(clip);
    }

    private async Task<Clip?> DecodeAsync(PendingCapture pending, bool evenWhenProfileSwitchedOff = false, CancellationToken cancellationToken = default)
    {
        var clip = await _decoder.DecodeAsync(pending.Formats, pending.ApplicationName, evenWhenProfileSwitchedOff, cancellationToken);
        if (clip == null)
            return null;

        clip.SourceApplicationName = pending.ApplicationName;
        clip.SourceApplicationTitle = pending.ApplicationTitle;

        return clip;
    }

    /// <summary>
    /// Adds an image to a data object as a bitmap (for apps that read only CF_BITMAP/CF_DIB) and in its original PNG
    /// or JPEG encoding, so re-capturing it loses no quality.
    /// </summary>
    private void AddImageFormats(WpfDataObject dataObject, byte[] imageData)
    {
        // Convert byte array back to BitmapSource for DIB format compatibility
        using var memoryStream = new MemoryStream(imageData);
        var decoder = BitmapDecoder.Create(memoryStream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);

        if (decoder.Frames.Count <= 0)
            return;

        // Create a writable copy to ensure pixel data is fully accessible
        var writableBitmap = new WriteableBitmap(decoder.Frames[0]);
        writableBitmap.Freeze();
        dataObject.SetImage(writableBitmap);

        // IMPORTANT: Don't dispose these streams - the DataObject needs them to remain alive
        var format = ClipboardSnapshotDecoder.DetectImageFormat(imageData);
        if (format == "PNG")
        {
            dataObject.SetData("PNG", new MemoryStream(imageData));
            _logger.LogDebug("Preserved PNG format ({Size} bytes) on clipboard", imageData.Length);
        }
        else if (format == "JPEG")
        {
            dataObject.SetData("JFIF", new MemoryStream(imageData));
            _logger.LogDebug("Preserved JPEG format ({Size} bytes) on clipboard", imageData.Length);
        }
    }

    /// <summary>
    /// The clip's image as plain bytes ready for the clipboard: from the decrypted cache for an unlocked encrypted clip,
    /// null for a locked one (its stored image is ciphertext) or a clip without an image.
    /// </summary>
    private byte[]? GetPlainImageData(Clip clip)
    {
        if (!clip.Encrypted)
            return clip.ImageData is { Length: > 0 } ? clip.ImageData : null;

        if (!clip.IsDecrypted)
            return null;

        var cachedBlobs = _blobCacheService.GetDecryptedBlobs(clip.Id);
        return cachedBlobs?.PngBlobs.Select(p => p.Data).FirstOrDefault() ?? cachedBlobs?.JpgBlobs.Select(p => p.Data).FirstOrDefault();
    }

    /// <summary>
    /// Adds a clip's file list to a data object as Explorer offers it: CF_HDROP (with FileName/FileNameW,
    /// which WPF derives from it), "Shell IDList Array" and, if one was captured, "Preferred DropEffect".
    /// </summary>
    private void AddFileDropFormats(WpfDataObject dataObject, Clip clip)
    {
        if (string.IsNullOrEmpty(clip.FilePathsJson))
            return;

        var filePaths = JsonSerializer.Deserialize<List<string>>(clip.FilePathsJson);
        if (filePaths is not { Count: > 0 })
            return;

        var fileDropList = new StringCollection();
        fileDropList.AddRange(filePaths.ToArray());
        dataObject.SetFileDropList(fileDropList);

        var shellIdListArray = ShellFileDropFormats.CreateShellIdListArray(filePaths);
        if (shellIdListArray != null)
            dataObject.SetData(Formats.ShellIdListArray.Name, shellIdListArray);
        else
            _logger.LogDebug("Not offering {Format}: at least one file no longer exists", Formats.ShellIdListArray.Name);

        if (clip.FileDropEffect is { } dropEffect)
            dataObject.SetData(Formats.PreferredDropEffect.Name, ShellFileDropFormats.CreatePreferredDropEffect(dropEffect));
    }

    /// <summary>
    /// The data object a copy of the clip puts on the clipboard, with every Format the clip has; null for an unsupported
    /// clip type.
    /// </summary>
    private WpfDataObject? CreateDataObject(Clip clip)
    {
        var dataObject = clip.Type switch
        {
            ClipType.Text or ClipType.Html or ClipType.RichText or ClipType.Custom => CreateTextDataObject(clip),
            ClipType.Image => CreateImageDataObject(clip),
            ClipType.Files => CreateFilesDataObject(clip),
            var _ => null,
        };

        if (dataObject == null)
        {
            _logger.LogWarning("Unsupported clip type: {ClipType}", clip.Type);

            return null;
        }

        // An encrypted clip's raw formats are not decrypted for the clipboard
        if (!clip.Encrypted)
            AddRawFormats(dataObject, clip.RawFormats);

        return dataObject;
    }

    /// <summary>
    /// The data object a copy of the clip puts on the clipboard: every Format the clip has, marked as ClipMate's own copy.
    /// </summary>
    internal WpfDataObject? CreateCopy(Clip clip)
    {
        var dataObject = CreateDataObject(clip);
        if (dataObject != null)
            OwnClipboardCopy.Mark(dataObject);

        return dataObject;
    }

    /// <summary>
    /// Offers again the formats the source offered that no typed content holds, such as an application's private format.
    /// </summary>
    private static void AddRawFormats(WpfDataObject dataObject, IReadOnlyList<RawClipboardFormat> rawFormats)
    {
        foreach (var item in rawFormats)
        {
            // A standard format keeps its code; a registered one is found again by name in this Windows session
            var name = item.Code is > 0 and < 0xC000 && Formats.TryGetStandardFormatName(item.Code, out _)
                ? DataFormats.GetDataFormat((int)item.Code).Name
                : item.Name;

            if (!dataObject.GetDataPresent(name, false))
                dataObject.SetData(name, new MemoryStream(item.Data), false);
        }
    }

    private WpfDataObject CreateTextDataObject(Clip clip)
    {
        try
        {
            var textContent = clip.TextContent;
            var rtfContent = clip.RtfContent;
            var htmlContent = clip.HtmlContent;

            // Handle encrypted clips - load decrypted content from cache OR encrypted base64 from transient properties
            if (clip.Encrypted)
            {
                if (clip.IsDecrypted)
                {
                    // Load decrypted content from cache
                    var cachedBlobs = _blobCacheService.GetDecryptedBlobs(clip.Id);
                    if (cachedBlobs is { TextBlobs.Count: > 0 })
                    {
                        // Use first text BLOB as plain text (decrypted)
                        textContent = cachedBlobs.TextBlobs[0].Data;
                        _logger.LogDebug("Loaded decrypted text content from cache for clip {ClipId}", clip.Id);
                    }
                    else
                        _logger.LogWarning("Clip {ClipId} is marked as decrypted but cache is empty", clip.Id);
                }
                else
                {
                    // Use encrypted base64 from transient properties (loaded by caller via IClipService.LoadEncryptedContentAsync)
                    // If textContent is still null, caller forgot to load - log warning
                    if (textContent == null)
                        _logger.LogWarning("Clip {ClipId} is encrypted but TextContent not loaded. Call IClipService.LoadEncryptedContentAsync first.", clip.Id);
                }
            }

            var dataObject = new WpfDataObject();

            // Set plain text (always)
            if (!string.IsNullOrEmpty(textContent))
                dataObject.SetText(textContent);

            // Set RTF if available
            if (!string.IsNullOrEmpty(rtfContent))
                dataObject.SetData(DataFormats.Rtf, rtfContent);

            // Set HTML if available
            if (!string.IsNullOrEmpty(htmlContent))
                dataObject.SetData(DataFormats.Html, htmlContent);

            // Text clips that also carry an image or a file list restore those Formats too, so the Paste Target picks:
            // an image-capable app takes the image, Explorer takes the files, a text-only field takes the text.
            if (GetPlainImageData(clip) is { } imageData)
            {
                try
                {
                    AddImageFormats(dataObject, imageData);
                }
                catch (Exception ex)
                {
                    // The text is the clip's primary content; still paste it if the stored image cannot be decoded
                    _logger.LogWarning(ex, "Could not put the image of clip {ClipId} on the clipboard", clip.Id);
                }
            }

            AddFileDropFormats(dataObject, clip);

            return dataObject;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting text to clipboard");

            throw;
        }
    }

    private WpfDataObject CreateImageDataObject(Clip clip)
    {
        try
        {
            var imageData = clip.ImageData;

            // Handle encrypted clips - load decrypted content from cache OR encrypted bytes from transient properties
            if (clip.Encrypted)
            {
                if (clip.IsDecrypted)
                {
                    // Load decrypted content from cache
                    var cachedBlobs = _blobCacheService.GetDecryptedBlobs(clip.Id);
                    if (cachedBlobs != null)
                    {
                        // Try PNG first, then JPG
                        if (cachedBlobs.PngBlobs.Count > 0)
                        {
                            imageData = cachedBlobs.PngBlobs[0].Data;
                            _logger.LogDebug("Loaded decrypted PNG content from cache for clip {ClipId}", clip.Id);
                        }
                        else if (cachedBlobs.JpgBlobs.Count > 0)
                        {
                            imageData = cachedBlobs.JpgBlobs[0].Data;
                            _logger.LogDebug("Loaded decrypted JPG content from cache for clip {ClipId}", clip.Id);
                        }
                        else
                            _logger.LogWarning("Clip {ClipId} is marked as decrypted but no image BLOBs in cache", clip.Id);
                    }
                    else
                        _logger.LogWarning("Clip {ClipId} is marked as decrypted but cache is empty", clip.Id);
                }
                else
                {
                    // Use encrypted binary data from transient properties (loaded by caller via IClipService.LoadEncryptedContentAsync)
                    if (imageData == null)
                        _logger.LogWarning("Clip {ClipId} is encrypted but ImageData not loaded. Call IClipService.LoadEncryptedContentAsync first.", clip.Id);
                }
            }

            if (imageData == null || imageData.Length == 0)
                throw new InvalidOperationException("Image data is empty");

            _logger.LogDebug("Setting image to clipboard: Format={Format}, Size={Size} bytes", ClipboardSnapshotDecoder.DetectImageFormat(imageData), imageData.Length);

            var dataObject = new WpfDataObject();
            AddImageFormats(dataObject, imageData);
            AddFileDropFormats(dataObject, clip);

            return dataObject;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting image to clipboard");

            throw;
        }
    }

    private WpfDataObject CreateFilesDataObject(Clip clip)
    {
        try
        {
            if (string.IsNullOrEmpty(clip.FilePathsJson))
                throw new InvalidOperationException("File paths are empty");

            var filePaths = JsonSerializer.Deserialize<List<string>>(clip.FilePathsJson);

            if (filePaths == null || filePaths.Count == 0)
                throw new InvalidOperationException("No file paths found");

            var dataObject = new WpfDataObject();
            AddFileDropFormats(dataObject, clip);

            return dataObject;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting files to clipboard");

            throw;
        }
    }

    /// <summary>
    /// The Source Application of a clipboard change: the process of the clipboard owner, or of the foreground window
    /// when the owner's process can't be found. Without an owner it is the foreground window only when the clipboard
    /// has text, otherwise "WINDOWS", as ClipMate 7 records. The title is the owner's top-level window title, else
    /// the foreground window's. ClipMate itself gets no name, so it never has an Application Profile.
    /// </summary>
    private SourceApplication GetSourceApplication(ClipboardSnapshot snapshot)
    {
        var owner = snapshot.Owner;
        if (owner.IsNull && !snapshot.Formats.Any(p => p.Name is "CF_UNICODETEXT" or "CF_TEXT"))
            return new SourceApplication(NoOwnerApplication, null, false);

        var foreground = _win32.GetForegroundWindow();
        var window = owner;
        var process = FindProcess(owner);
        if (process == null)
        {
            window = foreground;
            process = FindProcess(foreground);
        }

        var title = GetTopLevelTitle(window) ?? (window != foreground ? GetTopLevelTitle(foreground) : null);

        return new SourceApplication(process?.Name, title, process?.IsClipMate == true);
    }

    private (string? Name, bool IsClipMate)? FindProcess(HWND window)
    {
        if (window.IsNull)
            return null;

        var processId = _win32.GetWindowProcessId(window);
        if (processId == 0)
            return null;

        if (OwnProcess.Is(processId))
            return (null, true);

        return _win32.GetProcessName(processId) is { } name ? (name, false) : null;
    }

    private string? GetTopLevelTitle(HWND window)
    {
        if (window.IsNull)
            return null;

        var topLevel = _win32.GetTopLevelWindow(window);

        return _win32.GetWindowTitle(topLevel.IsNull ? window : topLevel) is { Length: > 0 } title ? title : null;
    }

    private sealed record SourceApplication(string? Name, string? Title, bool IsClipMate);

    private static bool IsTextOrImage(string formatName) =>
        formatName is "CF_UNICODETEXT" or "CF_TEXT" or "CF_DIB" or "CF_DIBV5" or "PNG";
}
