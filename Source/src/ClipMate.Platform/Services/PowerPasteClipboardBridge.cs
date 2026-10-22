using System.Diagnostics;
using System.Windows.Interop;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Platform.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace ClipMate.Platform.Services;

/// <summary>
/// Bridges PowerPaste's sequence state to real Windows paste detection.
/// Every Format of the waiting clip is registered on the clipboard for delayed rendering
/// (SetClipboardData with no data), so Windows notifies us via WM_RENDERFORMAT at the
/// exact moment an application reads it (GetClipboardData). All Formats are rendered on that
/// first request; a read by the application the user is working in is a paste and advances
/// the sequence, any other read just gets the data.
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

    // Re-arming after a background read changes the clipboard, and a clipboard reader that ignores the markers below
    // reads every change; stop answering it after a few rounds rather than loop with it forever.
    private const int MaxReArmsPerClip = 3;

    // Formats that tell clipboard history and other clipboard managers to ignore a change (ClipMate 7's
    // "Clipboard Viewer Ignore" and the Windows clipboard history's own). A re-arm adds them: the waiting clip is
    // unchanged, so the managers that already read it have nothing new to read.
    private static readonly string[] _ignoredByMonitorsFormatNames =
        ["Clipboard Viewer Ignore", "ExcludeClipboardContentFromMonitorProcessing"];

    private readonly IClipboardService _clipboardService;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<PowerPasteClipboardBridge> _logger;
    private readonly IPowerPasteService _powerPasteService;
    private readonly ITemplateService _templateService;

    private CancellationTokenSource? _advanceDebounceCts;
    private int _armCount;
    private Clip? _armedClip;
    private IReadOnlyList<ClipboardFormatData> _armedFormats = [];
    private bool _clipboardTaken;
    private HwndSource? _hwndSource;
    private bool _isReregistering;
    private DateTime _lastAcceptedPasteAt = DateTime.MinValue;
    private int _reArmCount;
    private bool _reArmPending;

    public PowerPasteClipboardBridge(IPowerPasteService powerPasteService,
        IClipboardService clipboardService,
        IConfigurationService configurationService,
        ITemplateService templateService,
        ILogger<PowerPasteClipboardBridge> logger)
    {
        _powerPasteService = powerPasteService ?? throw new ArgumentNullException(nameof(powerPasteService));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// The Formats registered for the waiting clip.
    /// </summary>
    internal IReadOnlyList<ClipboardFormatData> ArmedFormats => _armedFormats;

    /// <inheritdoc />
    public void Dispose() => TearDown(false);

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

        // Leaving: the waiting clip stays on the clipboard through WM_RENDERALLFORMATS
        RunOnDispatcher(() => TearDown(false));

        return Task.CompletedTask;
    }

    private void OnStateChanged(object? sender, PowerPasteStateChangedEventArgs e)
    {
        if (e.NewState == PowerPasteState.Active)
            RunOnDispatcher(CreateMessageWindow);
        else
            RunOnDispatcher(() => TearDown(!_clipboardTaken));
    }

    private void OnPositionChanged(object? sender, PowerPastePositionChangedEventArgs e) =>
        RunOnDispatcher(() => Arm(e.CurrentClip, e.IsExplodePiece));

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
        _clipboardTaken = false;

        // Our own delayed-render registrations broadcast WM_CLIPBOARDUPDATE like any other
        // clipboard write. Without suspending, ClipMate's own capture pipeline would read the
        // clipboard in response and capture every clip of the sequence again.
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

    /// <summary>
    /// Ends Power Paste's hold on the clipboard. With <paramref name="restore" /> the waiting clip is put back as a
    /// normal copy with all its Formats; without it the clipboard is left alone (another application took it, or
    /// ClipMate is closing and WM_RENDERALLFORMATS has already kept the clip).
    /// </summary>
    private void TearDown(bool restore)
    {
        _advanceDebounceCts?.Cancel();
        _advanceDebounceCts = null;

        var clip = _armedClip;
        _armedClip = null;

        if (_hwndSource != null)
        {
            // Destroying the window while it still owns delayed-render Formats sends WM_RENDERALLFORMATS, so keep
            // the hook until it is gone: the clipboard keeps the waiting clip instead of being left empty.
            var source = _hwndSource;
            _hwndSource = null;
            source.Dispose();

            if (restore && clip != null)
                RestoreAsNormalCopy(clip);
        }

        _armedFormats = [];
        _clipboardService.ResumeCapture();
    }

    private void RestoreAsNormalCopy(Clip clip)
    {
        // A normal copy: the capture pipeline recognizes the clip by this hash and does not capture it again
        clip.ContentHash = ContentHasher.HashClip(clip);

        _ = Task.Run(async () =>
        {
            // Power Paste was started again meanwhile and holds the clipboard
            if (_powerPasteService.State == PowerPasteState.Active)
                return;

            try
            {
                await _clipboardService.SetClipboardContentAsync(clip);
                _logger.LogInformation("PowerPaste left the waiting clip {ClipId} on the clipboard", clip.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PowerPaste could not leave the waiting clip {ClipId} on the clipboard", clip.Id);
            }
        });
    }

    /// <summary>
    /// Applies the active template to the clip and registers every Format of the result for delayed rendering.
    /// An Explode piece is plain text only.
    /// </summary>
    internal void Arm(Clip? clip, bool isExplodePiece = false)
    {
        _advanceDebounceCts?.Cancel();
        _armCount++;
        _reArmCount = 0;
        _reArmPending = false;

        if (clip == null)
        {
            _armedClip = null;
            _armedFormats = [];

            return;
        }

        // Applied once per waiting clip, so #SEQUENCE# advances once per paste
        var pastedClip = _templateService.TryApplyTemplate(clip) ?? clip;
        _armedClip = pastedClip;
        _armedFormats = isExplodePiece
            ? [new ClipboardFormatData((uint)Formats.UnicodeText.Code, DataObjectFormats.EncodeUnicodeText(pastedClip.TextContent ?? string.Empty))]
            : _clipboardService.GetClipboardFormats(pastedClip);

        if (_armedFormats.Count == 0)
        {
            _logger.LogWarning("PowerPaste clip {ClipId} has nothing to put on the clipboard", clip.Id);

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
                foreach (var item in _armedFormats)
                    PInvoke.SetClipboardData(item.FormatId, new HANDLE(IntPtr.Zero));

                _logger.LogInformation("PowerPaste armed clip {ClipId} for delayed rendering in {FormatCount} format(s)",
                    _armedClip?.Id, _armedFormats.Count);
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

    /// <summary>
    /// After a read that was not a paste the clipboard holds the clip's real data, so the user's next paste would go
    /// unnoticed. Registers the Formats for delayed rendering again, without emptying the clipboard, and marks the
    /// change as one clipboard managers should ignore.
    /// </summary>
    private void ReArm(int armCount, bool afterBackgroundRead)
    {
        // The next clip was armed meanwhile
        if (armCount != _armCount)
            return;

        _reArmPending = false;
        if (_hwndSource == null || _armedFormats.Count == 0)
            return;

        if (afterBackgroundRead && _reArmCount >= MaxReArmsPerClip)
        {
            _logger.LogWarning(
                "PowerPaste stopped re-arming clip {ClipId}: another program keeps reading the clipboard, so the next paste may not be detected",
                _armedClip?.Id);

            return;
        }

        if (afterBackgroundRead)
            _reArmCount++;

        if (!TryOpenClipboard(new HWND(_hwndSource.Handle)))
        {
            _logger.LogWarning("PowerPaste could not open the clipboard to re-arm clip {ClipId}", _armedClip?.Id);

            return;
        }

        try
        {
            foreach (var item in _armedFormats)
                PInvoke.SetClipboardData(item.FormatId, new HANDLE(IntPtr.Zero));

            foreach (var item in _ignoredByMonitorsFormatNames)
            {
                var hGlobal = DataObjectFormats.AllocGlobal(new byte[sizeof(int)]);
                if (hGlobal != IntPtr.Zero)
                    PInvoke.SetClipboardData(PInvoke.RegisterClipboardFormat(item), new HANDLE(hGlobal));
            }

            _logger.LogDebug("PowerPaste re-armed clip {ClipId} after a read that was not a paste", _armedClip?.Id);
        }
        finally
        {
            PInvoke.CloseClipboard();
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
                HandleRenderAllFormats(new HWND(hwnd));
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
        var reader = GetClipboardReader();

        _logger.LogInformation(
            "PowerPaste WM_RENDERFORMAT received for format {FormatId}, read by {ReaderProcess} while {ForegroundProcess} is in the foreground",
            formatId, reader.ReaderProcessName, reader.ForegroundProcessName);

        // Windows sends WM_RENDERFORMAT once per format; every Format is supplied now, whoever asked
        RenderFormatData();

        if (!IsPaste(reader.ReaderProcessId, reader.ForegroundProcessId, (uint)Environment.ProcessId))
        {
            // Clipboard history, another clipboard manager, ClipMate itself: it gets the data, but the user has not
            // pasted. Arm the clip again once the reader has closed the clipboard.
            _logger.LogDebug("PowerPaste clipboard read by {ReaderProcess} is not a paste", reader.ReaderProcessName);
            ScheduleReArm(true);

            return;
        }

        var msSinceLastAcceptedPaste = (DateTime.UtcNow - _lastAcceptedPasteAt).TotalMilliseconds;
        if (msSinceLastAcceptedPaste < MinAdvanceIntervalMs)
        {
            // Too soon after the last accepted paste to plausibly be a new, separate Ctrl+V -
            // most likely the target application (or Windows) re-reading what it just received.
            _logger.LogDebug(
                "PowerPaste ignoring clipboard read {ElapsedMs}ms after the last accepted paste - too soon to be a new paste",
                msSinceLastAcceptedPaste);
            ScheduleReArm(false);

            return;
        }

        _lastAcceptedPasteAt = DateTime.UtcNow;
        OnPasteDetected();
    }

    /// <summary>
    /// A clipboard read is a paste when the window holding the clipboard open belongs to the application in the
    /// foreground, and that application is not ClipMate. A read by a window-less caller (process ID 0) never is.
    /// </summary>
    internal static bool IsPaste(uint readerProcessId, uint foregroundProcessId, uint ownProcessId) =>
        readerProcessId != 0 && readerProcessId == foregroundProcessId && readerProcessId != ownProcessId;

    /// <param name="afterBackgroundRead">True when another program read the clipboard; only those re-arms are limited.</param>
    private void ScheduleReArm(bool afterBackgroundRead)
    {
        if (_reArmPending || _hwndSource == null)
            return;

        _reArmPending = true;
        var armCount = _armCount;
        _hwndSource.Dispatcher.BeginInvoke(DispatcherPriority.Background, () => ReArm(armCount, afterBackgroundRead));
    }

    private (uint ReaderProcessId, string ReaderProcessName, uint ForegroundProcessId, string ForegroundProcessName) GetClipboardReader()
    {
        var readerProcessId = GetProcessId(PInvoke.GetOpenClipboardWindow());
        var foregroundProcessId = GetProcessId(PInvoke.GetForegroundWindow());

        return (readerProcessId, GetProcessName(readerProcessId), foregroundProcessId, GetProcessName(foregroundProcessId));
    }

    private static unsafe uint GetProcessId(HWND window)
    {
        if (window.IsNull)
            return 0;

        uint processId;
        PInvoke.GetWindowThreadProcessId(window, &processId);

        return processId;
    }

    private static string GetProcessName(uint processId)
    {
        if (processId == 0)
            return "(no window)";

        try
        {
            using var process = Process.GetProcessById((int)processId);

            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return "(unknown)";
        }
    }

    private void HandleRenderAllFormats(HWND hwnd)
    {
        // Our window is being destroyed while still owning delayed-render data - must provide
        // real data now or it's lost. Clipboard must be (re)opened explicitly here, unlike
        // WM_RENDERFORMAT where it's already open for us.
        if (_armedFormats.Count == 0 || !TryOpenClipboard(hwnd))
            return;

        try
        {
            // Another program may have taken the clipboard while it was being opened
            if (PInvoke.GetClipboardOwner() == hwnd)
                RenderFormatData();
        }
        finally
        {
            PInvoke.CloseClipboard();
        }
    }

    private void RenderFormatData()
    {
        foreach (var item in _armedFormats)
        {
            var hGlobal = DataObjectFormats.AllocGlobal(item.Data);
            if (hGlobal == IntPtr.Zero)
            {
                _logger.LogWarning("Failed to allocate memory to render PowerPaste clipboard format {FormatId}", item.FormatId);

                continue;
            }

            PInvoke.SetClipboardData(item.FormatId, new HANDLE(hGlobal));
        }

        _logger.LogInformation("PowerPaste rendered {FormatCount} format(s) of clip {ArmedClipId}", _armedFormats.Count, _armedClip?.Id);
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
    /// other application (or the user, via Ctrl+C) took the clipboard - PowerPaste stops and
    /// leaves the new content alone.
    /// </summary>
    internal void OnClipboardOwnershipLost()
    {
        if (_isReregistering)
            return;

        _logger.LogInformation("PowerPaste clipboard ownership lost externally - stopping PowerPaste");
        _clipboardTaken = true;
        _powerPasteService.Stop();
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
