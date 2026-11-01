using System.Windows.Interop;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using ClipMate.Core.Models;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Platform.Helpers;
using ClipMate.Platform.Interop;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

    // Re-arming after a background read changes the clipboard, and a clipboard reader that ignores the "ignore me"
    // markers reads every change. A few re-arms per interval follow the settle time, so a paste soon after a background
    // read is still seen; more wait for the interval, which keeps such a reader from spinning with us while the clip
    // stays detectable, and once readers stop the next re-arm is quick again.
    private const int QuickReArmsPerInterval = 3;

    // A reader can open the clipboard several times for one change (Remote Desktop's clipboard redirection does). Re-arming
    // only after this settle time lets those opens read the data already rendered, instead of each one rendering it again
    // and counting as another read.
    private static readonly TimeSpan _reArmSettleTime = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan _reArmInterval = TimeSpan.FromSeconds(1);

    private readonly IClipboardService _clipboardService;
    private readonly IConfigurationService _configurationService;
    private readonly ICurrentClipWriter _currentClipWriter;
    private readonly ILogger<PowerPasteClipboardBridge> _logger;
    private readonly IPowerPasteService _powerPasteService;
    private readonly Queue<DateTimeOffset> _recentReArms = new();
    private readonly ITemplateService _templateService;
    private readonly TimeProvider _timeProvider;
    private readonly IWin32InputInterop _win32;

    private CancellationTokenSource? _advanceDebounceCts;
    private int _armGeneration;

    // The waiting clip: as it is pasted (template applied), its Formats, and whether a paste uses up a #SEQUENCE#
    // number. The generation moves on with every clip armed, so work queued for an earlier one is dropped.
    private Clip? _armedClip;
    private bool _armedClipIsTemplated;
    private HwndSource? _hwndSource;
    private bool _isReregistering;
    private DateTime _lastAcceptedPasteAt = DateTime.MinValue;

    // Re-arming after a read that was not a paste
    private bool _reArmPending;

    public PowerPasteClipboardBridge(IPowerPasteService powerPasteService,
        IClipboardService clipboardService,
        IConfigurationService configurationService,
        ICurrentClipWriter currentClipWriter,
        ITemplateService templateService,
        IWin32InputInterop win32,
        ILogger<PowerPasteClipboardBridge> logger,
        TimeProvider? timeProvider = null)
    {
        _powerPasteService = powerPasteService ?? throw new ArgumentNullException(nameof(powerPasteService));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _currentClipWriter = currentClipWriter ?? throw new ArgumentNullException(nameof(currentClipWriter));
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _win32 = win32 ?? throw new ArgumentNullException(nameof(win32));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// The Formats registered for the waiting clip.
    /// </summary>
    internal IReadOnlyList<ClipboardFormatData> ArmedFormats { get; private set; } = [];

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
        UiThread.Run(() => TearDown(false));

        return Task.CompletedTask;
    }

    private void OnStateChanged(object? sender, PowerPasteStateChangedEventArgs e)
    {
        if (e.NewState == PowerPasteState.Active)
            UiThread.Run(CreateMessageWindow);
        else
            UiThread.Run(() => TearDown(e.KeepWaitingClip));
    }

    private void OnPositionChanged(object? sender, PowerPastePositionChangedEventArgs e) =>
        UiThread.Run(() => Arm(e.CurrentClip, e.IsExplodePiece));

    private void CreateMessageWindow()
    {
        if (_hwndSource != null)
            return;

        // Don't let timing from a previous PowerPaste session affect this one.
        _lastAcceptedPasteAt = DateTime.MinValue;

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
    /// normal copy with all its Formats; without it nothing more is written (another application took the clipboard,
    /// a picked clip is about to be copied, or ClipMate is closing and WM_RENDERALLFORMATS has already kept the clip).
    /// </summary>
    private void TearDown(bool restore)
    {
        _advanceDebounceCts?.Cancel();
        _advanceDebounceCts = null;
        _armGeneration++;

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

        ArmedFormats = [];
        _clipboardService.ResumeCapture();
    }

    private void RestoreAsNormalCopy(Clip clip)
    {
        // A normal copy, marked as ClipMate's own so it is not captured again; the clip stays in the list untouched
        var copy = clip.WithText(clip.TextContent);

        _ = Task.Run(async () =>
        {
            // Power Paste was started again meanwhile and holds the clipboard
            if (_powerPasteService.State == PowerPasteState.Active)
                return;

            try
            {
                await _currentClipWriter.PutOnClipboardAsync(copy);
                _logger.LogInformation("PowerPaste left the waiting clip {ClipId} on the clipboard", copy.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PowerPaste could not leave the waiting clip {ClipId} on the clipboard", copy.Id);
            }
        });
    }

    /// <summary>
    /// Registers every Format of the clip for delayed rendering, with the active template applied to its text. The
    /// template's #SEQUENCE# number is used up only when the clip is pasted. An Explode piece is plain text only.
    /// </summary>
    internal void Arm(Clip? clip, bool isExplodePiece = false)
    {
        _advanceDebounceCts?.Cancel();
        _armGeneration++;
        _reArmPending = false;
        _recentReArms.Clear();

        if (clip == null)
        {
            _armedClip = null;
            ArmedFormats = [];

            return;
        }

        // Only the text changes; every other Format of the clip is offered as it is
        var templatedText = _templateService.PreviewTemplateText(clip);
        _armedClipIsTemplated = templatedText != null;
        var pastedClip = templatedText == null
            ? clip
            : clip.WithText(templatedText);

        _armedClip = pastedClip;
        if (isExplodePiece)
        {
            ArmedFormats = [new ClipboardFormatData((uint)Formats.UnicodeText.Code,
                DataObjectFormats.EncodeUnicodeText(pastedClip.TextContent ?? string.Empty))];
            _currentClipWriter.ReportCurrent(pastedClip, null);
        }
        else
            ArmedFormats = _currentClipWriter.Prepare(pastedClip);

        if (ArmedFormats.Count == 0)
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
                SetArmedFormats(false);

                _logger.LogInformation("PowerPaste armed clip {ClipId} for delayed rendering in {FormatCount} format(s)",
                    _armedClip?.Id, ArmedFormats.Count);
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
    private void ReArm(int armGeneration)
    {
        // The next clip was armed, or Power Paste stopped, meanwhile
        if (armGeneration != _armGeneration)
            return;

        _reArmPending = false;
        if (_hwndSource == null || ArmedFormats.Count == 0)
            return;

        _recentReArms.Enqueue(_timeProvider.GetUtcNow());
        if (_recentReArms.Count > QuickReArmsPerInterval)
            _recentReArms.Dequeue();

        if (!TryOpenClipboard(new HWND(_hwndSource.Handle)))
        {
            _logger.LogWarning("PowerPaste could not open the clipboard to re-arm clip {ClipId}", _armedClip?.Id);

            return;
        }

        try
        {
            SetArmedFormats(false);

            // The waiting clip is unchanged, so clipboard managers that already read it are asked not to read it again
            foreach (var item in ClipboardIgnoreFormats.All)
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

    /// <summary>
    /// Sets every Format of the waiting clip on the open clipboard: its data when <paramref name="render" />, or no
    /// data to register it for delayed rendering.
    /// </summary>
    private void SetArmedFormats(bool render)
    {
        foreach (var item in ArmedFormats)
        {
            var handle = IntPtr.Zero;
            if (render)
            {
                handle = DataObjectFormats.AllocGlobal(item.Data);
                if (handle == IntPtr.Zero)
                {
                    _logger.LogWarning("Failed to allocate memory to render PowerPaste clipboard format {FormatId}", item.FormatId);

                    continue;
                }
            }

            PInvoke.SetClipboardData(item.FormatId, new HANDLE(handle));
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
        var reader = ClipboardReader.Current(_win32, PInvoke.GetOpenClipboardWindow());

        _logger.LogInformation(
            "PowerPaste WM_RENDERFORMAT received for format {FormatId}, read by {ReaderProcess} while {ForegroundProcess} is in the foreground",
            formatId, reader.ProcessName, reader.ForegroundProcessName);

        // Windows sends WM_RENDERFORMAT once per format; every Format is supplied now, whoever asked
        SetArmedFormats(true);
        _logger.LogInformation("PowerPaste rendered {FormatCount} format(s) of clip {ArmedClipId}", ArmedFormats.Count, _armedClip?.Id);

        if (!reader.IsPaste(OwnProcess.Id))
        {
            // Clipboard history, another clipboard manager, ClipMate itself: it gets the data, but the user has not
            // pasted. Arm the clip again once the reader has closed the clipboard.
            _logger.LogDebug("PowerPaste clipboard read by {ReaderProcess} is not a paste", reader.ProcessName);
            ScheduleReArm();

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

            ScheduleReArm();

            return;
        }

        AcceptPaste();
    }

    /// <summary>
    /// The waiting clip was pasted: its #SEQUENCE# number is used up and the sequence moves on.
    /// </summary>
    internal void AcceptPaste()
    {
        _lastAcceptedPasteAt = DateTime.UtcNow;

        // The next clip gets the next number
        if (_armedClipIsTemplated)
            _templateService.AdvanceSequence();

        OnPasteDetected();
    }

    /// <summary>
    /// Arms the waiting clip again once the reader is done with the clipboard: after <see cref="_reArmSettleTime" />, or
    /// later when the waiting clip was already re-armed <see cref="QuickReArmsPerInterval" /> times within
    /// <see cref="_reArmInterval" />.
    /// </summary>
    private void ScheduleReArm()
    {
        if (_reArmPending || _hwndSource == null)
            return;

        _reArmPending = true;
        var armGeneration = _armGeneration;
        var dispatcher = _hwndSource.Dispatcher;
        var wait = _reArmSettleTime;
        if (_recentReArms.Count == QuickReArmsPerInterval)
        {
            var throttled = _recentReArms.Peek() + _reArmInterval - _timeProvider.GetUtcNow();
            if (throttled > wait)
                wait = throttled;
        }

        _ = Task.Delay(wait, _timeProvider)
            .ContinueWith(_ => dispatcher.BeginInvoke(DispatcherPriority.Background, () => ReArm(armGeneration)),
                TaskScheduler.Default);
    }

    private void HandleRenderAllFormats(HWND hwnd)
    {
        // Our window is being destroyed while still owning delayed-render data - must provide
        // real data now or it's lost. Clipboard must be (re)opened explicitly here, unlike
        // WM_RENDERFORMAT where it's already open for us.
        if (ArmedFormats.Count == 0 || !TryOpenClipboard(hwnd))
            return;

        try
        {
            // Another program may have taken the clipboard while it was being opened
            if (PInvoke.GetClipboardOwner() == hwnd)
                SetArmedFormats(true);
        }
        finally
        {
            PInvoke.CloseClipboard();
        }
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
        _powerPasteService.Stop(false);
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

/// <summary>
/// Who is reading the clipboard during a WM_RENDERFORMAT: the process of the window holding it open, and the process
/// in the foreground. Process ID 0 means no window.
/// </summary>
internal sealed record ClipboardReader(uint ProcessId, string ProcessName, uint ForegroundProcessId, string ForegroundProcessName)
{
    /// <summary>
    /// The reader as seen now, while the clipboard is open to <paramref name="openClipboardWindow" />.
    /// </summary>
    public static ClipboardReader Current(IWin32InputInterop win32, HWND openClipboardWindow)
    {
        var processId = GetProcessId(win32, openClipboardWindow);
        var foregroundProcessId = GetProcessId(win32, win32.GetForegroundWindow());

        return new ClipboardReader(processId, GetProcessName(win32, processId), foregroundProcessId, GetProcessName(win32, foregroundProcessId));
    }

    /// <summary>
    /// A read is a paste when the window holding the clipboard open belongs to the application in the foreground, and
    /// that application is not ClipMate. A read by a window-less caller never is.
    /// </summary>
    public bool IsPaste(uint ownProcessId) =>
        ProcessId != 0 && ProcessId == ForegroundProcessId && ProcessId != ownProcessId;

    private static uint GetProcessId(IWin32InputInterop win32, HWND window)
    {
        if (window.IsNull)
            return 0;

        win32.GetWindowThreadProcessId(window, out var processId);

        return processId;
    }

    private static string GetProcessName(IWin32InputInterop win32, uint processId) =>
        processId == 0
            ? "(no window)"
            : win32.GetProcessName(processId) ?? "(unknown)";
}
