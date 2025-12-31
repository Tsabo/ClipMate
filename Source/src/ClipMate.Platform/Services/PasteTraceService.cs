using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Platform.Services;

/// <summary>
/// Service for tracing paste operations using Windows delayed rendering.
/// Registers formats with null data and monitors WM_RENDERFORMAT requests.
/// </summary>
public sealed class PasteTraceService : IPasteTraceService, IDisposable
{
    private const int _wmRenderFormat = 0x0305;
    private const int _wmRenderAllFormats = 0x0306;
    private const int _wmDestroyClipboard = 0x0307;
    private readonly IClipboardDiagnosticsService _diagnosticsService;

    private readonly IClipboardFormatEnumerator _formatEnumerator;
    private readonly ILogger<PasteTraceService> _logger;

    private HwndSource? _hwndSource;
    private Dictionary<uint, string> _registeredFormats = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PasteTraceService" /> class.
    /// </summary>
    /// <param name="formatEnumerator">The clipboard format enumerator.</param>
    /// <param name="diagnosticsService">The clipboard diagnostics service.</param>
    /// <param name="logger">The logger instance.</param>
    public PasteTraceService(IClipboardFormatEnumerator formatEnumerator,
        IClipboardDiagnosticsService diagnosticsService,
        ILogger<PasteTraceService> logger)
    {
        _formatEnumerator = formatEnumerator ?? throw new ArgumentNullException(nameof(formatEnumerator));
        _diagnosticsService = diagnosticsService ?? throw new ArgumentNullException(nameof(diagnosticsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public void Dispose() => StopTrace();

    /// <inheritdoc />
    public ObservableCollection<PasteTraceEntry> TraceEntries { get; } = [];

    /// <inheritdoc />
    public bool IsTracing { get; private set; }

    /// <inheritdoc />
    public string? TargetApplication { get; private set; }

    /// <inheritdoc />
    public event EventHandler<bool>? TracingStateChanged;

    /// <inheritdoc />
    public event EventHandler<PasteTraceEntry>? FormatRequested;

    /// <inheritdoc />
    public bool StartTrace(Guid clipId)
    {
        if (IsTracing)
        {
            _logger.LogWarning("Trace already in progress, stopping previous trace");
            StopTrace();
        }

        try
        {
            // Get current clipboard formats to trace
            var currentFormats = _formatEnumerator.GetAllAvailableFormats();
            if (currentFormats.Count == 0)
            {
                _logger.LogWarning("No formats currently on clipboard to trace");
                return false;
            }

            _registeredFormats = currentFormats.ToDictionary(p => p.FormatCode, f => f.FormatName);

            // Create a hidden window to receive WM_RENDERFORMAT messages
            CreateMessageWindow();

            // Take clipboard ownership and register delayed rendering
            if (!TryOpenClipboard())
            {
                _logger.LogError("Failed to open clipboard for trace");
                return false;
            }

            try
            {
                PInvoke.EmptyClipboard();

                // Register each format with delayed rendering (null data)
                // Note: We're just tracking which formats are requested, not actually providing data
                foreach (var (formatId, formatName) in _registeredFormats)
                {
                    PInvoke.SetClipboardData(formatId, new HANDLE(IntPtr.Zero));
                    _logger.LogDebug("Registered delayed rendering for format {FormatId} ({FormatName})", formatId, formatName);
                }
            }
            finally
            {
                PInvoke.CloseClipboard();
            }

            IsTracing = true;
            TraceEntries.Clear();
            TracingStateChanged?.Invoke(this, true);

            _logger.LogInformation(
                "Paste trace started for clip {ClipId} with {FormatCount} formats",
                clipId,
                _registeredFormats.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start paste trace");
            StopTrace();
            return false;
        }
    }

    /// <inheritdoc />
    public void StopTrace()
    {
        if (!IsTracing)
            return;

        IsTracing = false;
        TargetApplication = null;

        // Destroy the message window
        DestroyMessageWindow();

        // Clear clipboard to remove our delayed rendering
        if (TryOpenClipboard())
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

        _registeredFormats.Clear();
        TracingStateChanged?.Invoke(this, false);

        _logger.LogInformation("Paste trace stopped");
    }

    /// <inheritdoc />
    public void ClearEntries() => TraceEntries.Clear();

    private void CreateMessageWindow()
    {
        if (_hwndSource != null)
            return;

        var parameters = new HwndSourceParameters("ClipMate.PasteTrace")
        {
            Width = 0,
            Height = 0,
            PositionX = 0,
            PositionY = 0,
            WindowStyle = 0, // Hidden window
            ExtendedWindowStyle = 0,
        };

        _hwndSource = new HwndSource(parameters);
        _hwndSource.AddHook(WndProc);
    }

    private void DestroyMessageWindow()
    {
        if (_hwndSource == null)
            return;

        _hwndSource.RemoveHook(WndProc);
        _hwndSource.Dispose();
        _hwndSource = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case _wmRenderFormat:
                HandleRenderFormat((uint)wParam.ToInt32());
                handled = true;
                break;

            case _wmRenderAllFormats:
                HandleRenderAllFormats();
                handled = true;
                break;

            case _wmDestroyClipboard:
                // Another app has taken clipboard ownership
                _logger.LogDebug("Clipboard ownership lost");
                handled = false;
                break;
        }

        return IntPtr.Zero;
    }

    private void HandleRenderFormat(uint formatId)
    {
        // Get format name from our registered formats or lookup
        var formatName = _registeredFormats.TryGetValue(formatId, out var name)
            ? name
            : _diagnosticsService.GetFormatName(formatId);

        // Get the requesting application
        var foregroundWindow = PInvoke.GetForegroundWindow();
        if (foregroundWindow != HWND.Null)
        {
            uint processId;
            unsafe
            {
                PInvoke.GetWindowThreadProcessId(foregroundWindow, &processId);
            }

            try
            {
                using var process = Process.GetProcessById((int)processId);
                TargetApplication = process.ProcessName;
            }
            catch
            {
                TargetApplication = null;
            }
        }

        // Note: In this trace-only implementation, we don't actually provide data.
        // The target app will fail to paste, but we capture which formats it requested.
        // For full delayed rendering with data, we'd need to store clipboard data first.

        // Record the trace entry
        var entry = new PasteTraceEntry(
            DateTime.Now,
            formatId,
            formatName,
            null, // No data provided in trace-only mode
            TargetApplication);

        // Marshal to UI thread
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            TraceEntries.Add(entry);
            FormatRequested?.Invoke(this, entry);
        });

        _logger.LogDebug("Format {FormatId} ({FormatName}) requested", formatId, formatName);
    }

    private void HandleRenderAllFormats()
    {
        // Called when our window is being destroyed while still owning clipboard
        // In trace-only mode, we don't provide any actual data
        _logger.LogDebug("WM_RENDERALLFORMATS received - trace mode, not providing data");
    }

    private bool TryOpenClipboard(int maxAttempts = 10, int delayMs = 50)
    {
        var hwnd = _hwndSource?.Handle ?? IntPtr.Zero;

        for (var i = 0; i < maxAttempts; i++)
        {
            if (PInvoke.OpenClipboard(new HWND(hwnd)))
                return true;

            Thread.Sleep(delayMs);
        }

        return false;
    }
}
