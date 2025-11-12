using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.DataExchange;
using ClipMate.Core.Exceptions;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Platform.Helpers;
using Microsoft.Extensions.Logging;

namespace ClipMate.Platform.Services;

/// <summary>
/// Service for monitoring and capturing clipboard changes using Win32 APIs via CsWin32.
/// </summary>
public class ClipboardService : IClipboardService, IDisposable
{
    private readonly ILogger<ClipboardService> _logger;
    private HwndSource? _hwndSource;
    private bool _isMonitoring;
    private DateTime _lastClipboardChange = DateTime.MinValue;
    private string _lastContentHash = string.Empty;
    private const int DebounceMilliseconds = 50;

    /// <inheritdoc/>
    public event EventHandler<ClipCapturedEventArgs>? ClipCaptured;

    /// <inheritdoc/>
    public bool IsMonitoring => _isMonitoring;

    public ClipboardService(ILogger<ClipboardService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        if (_isMonitoring)
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
                WindowStyle = 0
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

            _isMonitoring = true;
            _logger.LogInformation("Clipboard monitoring started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start clipboard monitoring");
            throw new ClipboardException("Failed to start clipboard monitoring", ex);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopMonitoringAsync()
    {
        if (!_isMonitoring)
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

            _isMonitoring = false;
            _logger.LogInformation("Clipboard monitoring stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping clipboard monitoring");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<Clip?> GetCurrentClipboardContentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Must run on STA thread (UI thread) for WPF Clipboard API
            return await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // WPF Clipboard API handles OpenClipboard/CloseClipboard internally
                return ExtractClipboardData();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting clipboard content");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SetClipboardContentAsync(Clip clip, CancellationToken cancellationToken = default)
    {
        if (clip == null)
        {
            throw new ArgumentNullException(nameof(clip));
        }

        try
        {
            // Must run on STA thread (UI thread) for WPF Clipboard API
            // Must run on STA thread (UI thread) for WPF Clipboard API
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // WPF Clipboard API handles OpenClipboard/CloseClipboard internally
                switch (clip.Type)
                {
                    case ClipType.Text:
                        SetTextToClipboard(clip.TextContent ?? string.Empty);
                        break;
                    // TODO: Implement Image, Files, etc.
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

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_CLIPBOARDUPDATE = 0x031D;

        if (msg == WM_CLIPBOARDUPDATE)
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
            if ((now - _lastClipboardChange).TotalMilliseconds < DebounceMilliseconds)
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
            if (foregroundWindow.Value != IntPtr.Zero)
            {
                clip.SourceApplicationName = GetProcessName(foregroundWindow);
                clip.SourceApplicationTitle = GetWindowTitle(foregroundWindow);
            }

            // Raise event for subscribers
            var eventArgs = new ClipCapturedEventArgs { Clip = clip };
            ClipCaptured?.Invoke(this, eventArgs);

            if (!eventArgs.Cancel)
            {
                _logger.LogDebug("Captured clip: {ClipType}, Hash: {Hash}", clip.Type, clip.ContentHash);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling clipboard change");
        }
    }

    private Clip? ExtractClipboardData()
    {
        // Priority: Text > Image > Files
        if (PInvoke.IsClipboardFormatAvailable(13)) // CF_UNICODETEXT
        {
            return ExtractTextClip();
        }
        else if (PInvoke.IsClipboardFormatAvailable(2)) // CF_BITMAP
        {
            // TODO: Implement image extraction
            _logger.LogDebug("Image clipboard format detected (not yet implemented)");
            return null;
        }
        else if (PInvoke.IsClipboardFormatAvailable(15)) // CF_HDROP (files)
        {
            // TODO: Implement file list extraction
            _logger.LogDebug("File list clipboard format detected (not yet implemented)");
            return null;
        }

        return null;
    }

    private Clip? ExtractTextClip()
    {
        try
        {
            // Use WPF Clipboard API which is safer and simpler
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText();
                if (string.IsNullOrEmpty(text))
                {
                    return null;
                }

                var contentHash = ContentHasher.HashText(text);

                return new Clip
                {
                    Id = Guid.NewGuid(),
                    Type = ClipType.Text,
                    TextContent = text,
                    ContentHash = contentHash,
                    CapturedAt = DateTime.UtcNow
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from clipboard");
        }

        return null;
    }

    private void SetTextToClipboard(string text)
    {
        try
        {
            // Use WPF Clipboard API which handles all the complexity
            Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting text to clipboard");
            throw;
        }
    }

    private unsafe string? GetProcessName(HWND hwnd)
    {
        try
        {
            uint processId;
            PInvoke.GetWindowThreadProcessId(hwnd, &processId);
            
            if (processId != 0)
            {
                var process = System.Diagnostics.Process.GetProcessById((int)processId);
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

    public void Dispose()
    {
        StopMonitoringAsync().Wait();
    }
}
