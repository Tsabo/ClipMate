using System.Windows.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using ClipMate.Core.Constants;
using ClipMate.Core.Models;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Platform.Helpers;
using ClipMate.Platform.Interop;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClipMate.Platform.Services;

/// <summary>
/// Power Paste's clipboard on Windows. The offered Formats are registered for delayed rendering (SetClipboardData with
/// no data) from a hidden window, so Windows sends WM_RENDERFORMAT at the moment an application reads one
/// (GetClipboardData). Every Format is rendered on that first request, and the read is reported with what tells a paste
/// from another read: whether the reader is the application in the foreground, and how recent the user's input is.
/// </summary>
public sealed class PowerPasteClipboard : IPowerPasteClipboard, IHostedService, IDisposable
{
    private const int WmRenderFormat = 0x0305;
    private const int WmRenderAllFormats = 0x0306;
    private const int WmDestroyClipboard = 0x0307;

    private readonly IWin32IdleDetector _idleDetector;
    private readonly ILogger<PowerPasteClipboard> _logger;
    private readonly IWin32InputInterop _win32;
    private IReadOnlyList<ClipboardFormatData> _formats = [];
    private HwndSource? _hwndSource;
    private bool _isOffering;

    public PowerPasteClipboard(IWin32InputInterop win32, IWin32IdleDetector idleDetector, ILogger<PowerPasteClipboard> logger)
    {
        _win32 = win32 ?? throw new ArgumentNullException(nameof(win32));
        _idleDetector = idleDetector ?? throw new ArgumentNullException(nameof(idleDetector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public event EventHandler<PowerPasteClipboardReadEventArgs>? Read;

    /// <inheritdoc />
    public event EventHandler? Lost;

    /// <inheritdoc />
    public void Dispose() => Release();

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Leaving: the waiting clip stays on the clipboard through WM_RENDERALLFORMATS
        UiThread.Run(Release);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public bool Offer(IReadOnlyList<ClipboardFormatData> formats)
    {
        ArgumentNullException.ThrowIfNull(formats);

        _formats = formats;
        var hwnd = new HWND(EnsureWindow().Handle);

        // Emptying the clipboard sends our own window WM_DESTROYCLIPBOARD, which is not another program taking it
        _isOffering = true;
        try
        {
            if (!TryOpenClipboard(hwnd))
                return false;

            try
            {
                PInvoke.EmptyClipboard();
                SetFormats(false);
            }
            finally
            {
                PInvoke.CloseClipboard();
            }

            return true;
        }
        finally
        {
            _isOffering = false;
        }
    }

    /// <inheritdoc />
    public bool OfferAgain()
    {
        if (_hwndSource == null || _formats.Count == 0)
            return false;

        if (!TryOpenClipboard(new HWND(_hwndSource.Handle)))
            return false;

        try
        {
            SetFormats(false);

            // The waiting clip is unchanged, so clipboard managers that already read it are asked not to read it again
            foreach (var item in ClipboardIgnoreFormats.All)
            {
                var hGlobal = DataObjectFormats.AllocGlobal(new byte[sizeof(int)]);
                if (hGlobal != IntPtr.Zero)
                    PInvoke.SetClipboardData(PInvoke.RegisterClipboardFormat(item), new HANDLE(hGlobal));
            }
        }
        finally
        {
            PInvoke.CloseClipboard();
        }

        return true;
    }

    /// <inheritdoc />
    public void Release()
    {
        if (_hwndSource != null)
        {
            // Destroying the window while it still owns delayed-render Formats sends WM_RENDERALLFORMATS, so keep the
            // hook until it is gone: the clipboard keeps the waiting clip instead of being left empty.
            var source = _hwndSource;
            _hwndSource = null;
            source.Dispose();
        }

        _formats = [];
    }

    private HwndSource EnsureWindow()
    {
        if (_hwndSource != null)
            return _hwndSource;

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

        return _hwndSource;
    }

    /// <summary>
    /// Sets every offered Format on the open clipboard: its data when <paramref name="render" />, or no data to
    /// register it for delayed rendering.
    /// </summary>
    private void SetFormats(bool render)
    {
        foreach (var item in _formats)
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
                OnRenderFormat((uint)wParam.ToInt32());
                handled = true;

                break;

            case WmRenderAllFormats:
                OnRenderAllFormats(new HWND(hwnd));
                handled = true;

                break;

            case WmDestroyClipboard:
                if (!_isOffering)
                    Lost?.Invoke(this, EventArgs.Empty);

                break;
        }

        return IntPtr.Zero;
    }

    private void OnRenderFormat(uint formatId)
    {
        var reader = ClipboardReader.Current(_win32, PInvoke.GetOpenClipboardWindow());

        _logger.LogInformation(
            "PowerPaste WM_RENDERFORMAT received for format {FormatId}, read by {ReaderProcess} while {ForegroundProcess} is in the foreground",
            formatId, reader.ProcessName, reader.ForegroundProcessName);

        // Windows sends WM_RENDERFORMAT once per format; every Format is supplied now, whoever asked
        SetFormats(true);

        Read?.Invoke(this, new PowerPasteClipboardReadEventArgs
        {
            IsByForegroundApplication = reader.IsForegroundApplication(OwnProcess.Id),
            MayBeForForegroundApplication = reader.MayBeForForegroundApplication,
            IsByClipMate = reader.ProcessId == OwnProcess.Id,
            IsClipMateInForeground = reader.ForegroundProcessId == OwnProcess.Id,
            SinceUserInput = TimeSpan.FromMilliseconds(_idleDetector.GetIdleTimeMilliseconds()),
            ReaderName = reader.ProcessName,
        });
    }

    private void OnRenderAllFormats(HWND hwnd)
    {
        // Our window is being destroyed while still owning delayed-render data: real data must be provided now or it
        // is lost. The clipboard must be opened here, unlike for WM_RENDERFORMAT where it is already open for us.
        if (_formats.Count == 0 || !TryOpenClipboard(hwnd))
            return;

        try
        {
            // Another program may have taken the clipboard while it was being opened
            if (PInvoke.GetClipboardOwner() == hwnd)
                SetFormats(true);
        }
        finally
        {
            PInvoke.CloseClipboard();
        }
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
