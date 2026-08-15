using System.Diagnostics;
using System.Windows.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Memory;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace ClipMate.Platform.Services;

/// <summary>
/// Bridges PowerPaste's sequence state to real Windows paste detection.
/// The current clip's text is registered on the clipboard for delayed rendering
/// (SetClipboardData with no data), so Windows notifies us via WM_RENDERFORMAT at the
/// exact moment the target application performs its paste (GetClipboardData). That's
/// what triggers rendering the real data and advancing to the next clip in the sequence.
/// </summary>
public sealed class PowerPasteClipboardBridge : IHostedService, IDisposable
{
    private const int WmRenderFormat = 0x0305;
    private const int WmRenderAllFormats = 0x0306;
    private const int WmDestroyClipboard = 0x0307;

    // Some applications (and Windows itself - e.g. Smart Actions inspecting freshly pasted text)
    // re-read the clipboard shortly after a real paste for reasons unrelated to the user pressing
    // Ctrl+V again. A render request arriving faster than any human re-paste realistically would
    // is treated as one of those, not a second paste.
    private const int MinAdvanceIntervalMs = 500;

    private readonly IClipboardService _clipboardService;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<PowerPasteClipboardBridge> _logger;
    private readonly IPowerPasteService _powerPasteService;

    private CancellationTokenSource? _advanceDebounceCts;
    private Clip? _armedClip;
    private HwndSource? _hwndSource;
    private bool _isReregistering;
    private DateTime _lastAcceptedPasteAt = DateTime.MinValue;

    public PowerPasteClipboardBridge(IPowerPasteService powerPasteService,
        IClipboardService clipboardService,
        IConfigurationService configurationService,
        ILogger<PowerPasteClipboardBridge> logger)
    {
        _powerPasteService = powerPasteService ?? throw new ArgumentNullException(nameof(powerPasteService));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public void Dispose() => TearDown();

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _powerPasteService.StateChanged += OnStateChanged;
        _powerPasteService.PositionChanged += OnPositionChanged;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _powerPasteService.StateChanged -= OnStateChanged;
        _powerPasteService.PositionChanged -= OnPositionChanged;

        RunOnDispatcher(TearDown);

        return Task.CompletedTask;
    }

    private void OnStateChanged(object? sender, PowerPasteStateChangedEventArgs e)
    {
        if (e.NewState == PowerPasteState.Active)
            RunOnDispatcher(CreateMessageWindow);
        else
            RunOnDispatcher(TearDown);
    }

    private void OnPositionChanged(object? sender, PowerPastePositionChangedEventArgs e) =>
        RunOnDispatcher(() => Arm(e.CurrentClip));

    private static void RunOnDispatcher(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;

        if (dispatcher == null || dispatcher.CheckAccess())
            action();
        else
            dispatcher.BeginInvoke(action);
    }

    private void CreateMessageWindow()
    {
        if (_hwndSource != null)
            return;

        // Don't let timing from a previous PowerPaste session affect this one.
        _lastAcceptedPasteAt = DateTime.MinValue;

        // Our own delayed-render registrations broadcast WM_CLIPBOARDUPDATE like any other
        // clipboard write. Without suspending, ClipMate's own capture pipeline reads the
        // clipboard in response - which satisfies our WM_RENDERFORMAT request itself and looks
        // exactly like a real paste, advancing the sequence before the user ever does.
        _clipboardService.SuspendCapture();

        var parameters = new HwndSourceParameters("ClipMate.PowerPaste")
        {
            Width = 0,
            Height = 0,
            PositionX = 0,
            PositionY = 0,
            WindowStyle = 0,
            ExtendedWindowStyle = 0,
        };

        _hwndSource = new HwndSource(parameters);
        _hwndSource.AddHook(WndProc);
    }

    private void TearDown()
    {
        _advanceDebounceCts?.Cancel();
        _advanceDebounceCts = null;
        _armedClip = null;

        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource.Dispose();
            _hwndSource = null;

            // Release our delayed-render clipboard ownership. Passing a null window associates
            // the open with the current process (the owning window is already gone).
            if (TryOpenClipboard(HWND.Null))
            {
                try
                {
                    PInvoke.EmptyClipboard();
                }
                finally
                {
                    PInvoke.CloseClipboard();
                }
            }
        }

        _clipboardService.ResumeCapture();
    }

    /// <summary>
    /// Registers the given clip's text for delayed clipboard rendering, or - for clips with
    /// no text content (e.g. images) - falls back to setting the clipboard immediately.
    /// Auto-advance cannot be detected for that fallback case.
    /// </summary>
    internal void Arm(Clip? clip)
    {
        _advanceDebounceCts?.Cancel();
        _armedClip = clip;

        if (clip == null)
            return;

        if (string.IsNullOrEmpty(clip.TextContent))
        {
            _logger.LogDebug(
                "PowerPaste clip {ClipId} has no text content - setting clipboard immediately (auto-advance unavailable for this item)",
                clip.Id);

            _ = _clipboardService.SetClipboardContentAsync(clip);

            return;
        }

        RegisterDelayedRendering();
    }

    private void RegisterDelayedRendering()
    {
        if (_hwndSource == null)
        {
            _logger.LogWarning("PowerPaste has no active message window to arm clip {ClipId} for delayed rendering", _armedClip?.Id);

            return;
        }

        var hwnd = new HWND(_hwndSource.Handle);

        _isReregistering = true;
        try
        {
            if (!TryOpenClipboard(hwnd))
            {
                _logger.LogWarning("PowerPaste could not open the clipboard to arm the next clip");

                return;
            }

            try
            {
                PInvoke.EmptyClipboard();
                PInvoke.SetClipboardData((uint)Formats.UnicodeText.Code, new HANDLE(IntPtr.Zero));
                _logger.LogInformation("PowerPaste armed clip {ClipId} for delayed rendering", _armedClip?.Id);
            }
            finally
            {
                PInvoke.CloseClipboard();
            }
        }
        finally
        {
            _isReregistering = false;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WmRenderFormat:
                HandleRenderFormat((uint)wParam.ToInt32());
                handled = true;

                break;

            case WmRenderAllFormats:
                HandleRenderAllFormats();
                handled = true;

                break;

            case WmDestroyClipboard:
                OnClipboardOwnershipLost();

                break;
        }

        return IntPtr.Zero;
    }

    private void HandleRenderFormat(uint formatId)
    {
        var requester = GetForegroundWindowInfo();

        _logger.LogInformation(
            "PowerPaste WM_RENDERFORMAT received for format {FormatId}, requested by {RequestingProcess} (window: {WindowTitle}, isOwnProcess={IsOwnProcess})",
            formatId, requester.ProcessName, requester.WindowTitle, requester.IsOwnProcess);

        RenderFormatData(formatId);

        if (requester.IsOwnProcess)
        {
            // ClipMate's own UI (command requery, focus/property-change handling, etc.) can read
            // the clipboard for reasons that have nothing to do with the user pasting into a
            // target application. Provide real data (above) so nothing breaks, but don't treat it
            // as a paste - only an external application's read should advance the sequence.
            //
            // Windows only sends WM_RENDERFORMAT once per NULL-data registration - once real data
            // has been provided, further reads (including the user's actual paste) succeed
            // silently with no further notification. Re-arm immediately, while the clipboard is
            // still open to us from this callback, so a subsequent real paste can still be seen.
            PInvoke.SetClipboardData(formatId, new HANDLE(IntPtr.Zero));
            _logger.LogDebug("PowerPaste ignoring clipboard read from ClipMate's own process - re-armed for the next read");

            return;
        }

        var msSinceLastAcceptedPaste = (DateTime.UtcNow - _lastAcceptedPasteAt).TotalMilliseconds;
        if (msSinceLastAcceptedPaste < MinAdvanceIntervalMs)
        {
            // Too soon after the last accepted paste to plausibly be a new, separate Ctrl+V -
            // most likely the target application (or Windows) re-reading what it just received.
            // Re-arm so a genuinely later paste still gets detected.
            PInvoke.SetClipboardData(formatId, new HANDLE(IntPtr.Zero));
            _logger.LogDebug(
                "PowerPaste ignoring clipboard read {ElapsedMs}ms after the last accepted paste - too soon to be a new paste, re-armed",
                msSinceLastAcceptedPaste);

            return;
        }

        _lastAcceptedPasteAt = DateTime.UtcNow;
        OnPasteDetected();
    }

    private static (string ProcessName, string WindowTitle, bool IsOwnProcess) GetForegroundWindowInfo()
    {
        try
        {
            var foregroundWindow = PInvoke.GetForegroundWindow();
            if (foregroundWindow.IsNull)
                return ("(none)", "(none)", false);

            uint processId;
            unsafe
            {
                PInvoke.GetWindowThreadProcessId(foregroundWindow, &processId);
            }

            var isOwnProcess = processId == (uint)Environment.ProcessId;

            string processName;
            try
            {
                using var process = Process.GetProcessById((int)processId);
                processName = process.ProcessName;
            }
            catch
            {
                processName = "(unknown)";
            }

            string windowTitle;
            const int maxLength = 256;
            unsafe
            {
                var buffer = stackalloc char[maxLength];
                var length = PInvoke.GetWindowText(foregroundWindow, buffer, maxLength);
                windowTitle = length > 0
                    ? new string(buffer, 0, length)
                    : "(empty)";
            }

            return (processName, windowTitle, isOwnProcess);
        }
        catch
        {
            return ("(unknown)", "(unknown)", false);
        }
    }

    private void HandleRenderAllFormats()
    {
        // Our window is being destroyed while still owning delayed-render data - must provide
        // real data now or it's lost. Clipboard must be (re)opened explicitly here, unlike
        // WM_RENDERFORMAT where it's already open for us.
        var clip = _armedClip;
        if (string.IsNullOrEmpty(clip?.TextContent))
            return;

        if (!TryOpenClipboard(HWND.Null))
            return;

        try
        {
            RenderFormatData((uint)Formats.UnicodeText.Code);
        }
        finally
        {
            PInvoke.CloseClipboard();
        }
    }

    private void RenderFormatData(uint formatId)
    {
        var text = _armedClip?.TextContent;
        if (string.IsNullOrEmpty(text))
        {
            _logger.LogWarning(
                "PowerPaste has no armed clip text to render for format {FormatId} (ArmedClipId={ArmedClipId})",
                formatId, _armedClip?.Id);

            return;
        }

        var hGlobal = AllocGlobalUnicodeText(text);
        if (hGlobal == IntPtr.Zero)
        {
            _logger.LogWarning("Failed to allocate memory to render PowerPaste clipboard format {FormatId}", formatId);

            return;
        }

        var result = PInvoke.SetClipboardData(formatId, new HANDLE(hGlobal));
        _logger.LogInformation(
            "PowerPaste rendered {Length} characters for format {FormatId} (ArmedClipId={ArmedClipId}, SetClipboardData succeeded={Succeeded})",
            text.Length, formatId, _armedClip?.Id, !result.IsNull);
    }

    /// <summary>
    /// Debounces WM_RENDERFORMAT notifications and advances the PowerPaste sequence once they
    /// settle. Some applications probe several formats for a single paste, so a single Ctrl+V
    /// must not advance the sequence more than once.
    /// </summary>
    internal void OnPasteDetected()
    {
        _advanceDebounceCts?.Cancel();
        _advanceDebounceCts?.Dispose();

        var cts = new CancellationTokenSource();
        _advanceDebounceCts = cts;

        var delayMs = Math.Max(0, _configurationService.Configuration.Preferences.PowerPasteDelay);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs, cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await _powerPasteService.AdvanceToNextAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error advancing PowerPaste sequence after paste was detected");
            }
        }, cts.Token);
    }

    /// <summary>
    /// Called when clipboard ownership changes. If it wasn't us re-arming the next clip, some
    /// other application (or the user, via Ctrl+C) took the clipboard - PowerPaste is
    /// interrupted, matching the existing behavior of stopping on unrelated clip selection.
    /// </summary>
    internal void OnClipboardOwnershipLost()
    {
        if (_isReregistering)
            return;

        _logger.LogInformation("PowerPaste clipboard ownership lost externally - stopping PowerPaste");
        _powerPasteService.Stop();
    }

    private static unsafe IntPtr AllocGlobalUnicodeText(string text)
    {
        var byteCount = (text.Length + 1) * sizeof(char);
        var hMem = PInvoke.GlobalAlloc(GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE, (nuint)byteCount);

        if (hMem.IsNull)
            return IntPtr.Zero;

        var ptr = PInvoke.GlobalLock(hMem);
        if (ptr == null)
        {
            PInvoke.GlobalFree(hMem);

            return IntPtr.Zero;
        }

        try
        {
            var span = new Span<char>(ptr, text.Length + 1);
            text.AsSpan().CopyTo(span);
            span[text.Length] = '\0';
        }
        finally
        {
            PInvoke.GlobalUnlock(hMem);
        }

        return (IntPtr)hMem.Value;
    }

    private static bool TryOpenClipboard(HWND hwnd, int maxAttempts = 10, int delayMs = 50)
    {
        for (var i = 0; i < maxAttempts; i++)
        {
            if (PInvoke.OpenClipboard(hwnd))
                return true;

            Thread.Sleep(delayMs);
        }

        return false;
    }
}
