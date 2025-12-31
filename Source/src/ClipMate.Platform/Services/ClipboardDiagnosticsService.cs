using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using Microsoft.Extensions.Logging;

namespace ClipMate.Platform.Services;

/// <summary>
/// Service for retrieving diagnostic information about the system clipboard.
/// Uses CsWin32 for Win32 API interop.
/// </summary>
public sealed class ClipboardDiagnosticsService : IClipboardDiagnosticsService
{
    private readonly ILogger<ClipboardDiagnosticsService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClipboardDiagnosticsService" /> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public ClipboardDiagnosticsService(ILogger<ClipboardDiagnosticsService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public ClipboardDiagnosticInfo GetDiagnostics()
    {
        var ownerName = GetOwnerProcessName();
        var sequenceNumber = GetSequenceNumber();
        var formats = new List<ClipboardFormatDiagnostic>();

        if (!TryOpenClipboard())
        {
            _logger.LogWarning("Failed to open clipboard for diagnostics");
            return new ClipboardDiagnosticInfo(ownerName, sequenceNumber, formats);
        }

        try
        {
            var formatId = PInvoke.EnumClipboardFormats(0);
            while (formatId != 0)
            {
                var formatName = GetFormatName(formatId);
                var dataSize = GetClipboardDataSize(formatId);

                formats.Add(new ClipboardFormatDiagnostic(formatId, formatName, dataSize));

                formatId = PInvoke.EnumClipboardFormats(formatId);
            }

            _logger.LogDebug("Retrieved {Count} clipboard formats for diagnostics", formats.Count);
        }
        finally
        {
            PInvoke.CloseClipboard();
        }

        return new ClipboardDiagnosticInfo(ownerName, sequenceNumber, formats);
    }

    /// <inheritdoc />
    public string GetOwnerProcessName()
    {
        var ownerHandle = PInvoke.GetClipboardOwner();
        if (ownerHandle == HWND.Null)
            return "(No owner)";

        uint processId;
        unsafe
        {
            PInvoke.GetWindowThreadProcessId(ownerHandle, &processId);
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return $"(PID {processId})";
        }
        catch (InvalidOperationException)
        {
            return $"(PID {processId})";
        }
    }

    /// <inheritdoc />
    public uint GetSequenceNumber() => PInvoke.GetClipboardSequenceNumber();

    /// <inheritdoc />
    public string GetFormatName(uint formatCode)
    {
        // Check standard format names first (O(1) lookup)
        if (Formats.TryGetStandardFormatName(formatCode, out var standardName) && standardName != null)
            return standardName;

        // Custom registered format - get name from Windows
        try
        {
            const int maxNameLength = 256;
            unsafe
            {
                var buffer = stackalloc char[maxNameLength];
                var length = PInvoke.GetClipboardFormatName(formatCode, buffer, maxNameLength);

                if (length > 0)
                    return new string(buffer, 0, length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get format name for code {FormatCode}", formatCode);
        }

        return $"Format_{formatCode}";
    }

    private bool TryOpenClipboard(int maxAttempts = 5, int delayMs = 10)
    {
        for (var i = 0; i < maxAttempts; i++)
        {
            if (PInvoke.OpenClipboard(HWND.Null))
                return true;

            if (i >= maxAttempts - 1)
                continue;

            _logger.LogDebug("Clipboard locked, retry {Retry}/{MaxRetries}", i + 1, maxAttempts);
            Thread.Sleep(delayMs * (i + 1)); // Exponential backoff
        }

        return false;
    }

    private static unsafe long? GetClipboardDataSize(uint format)
    {
        var hData = PInvoke.GetClipboardData(format);
        if (hData == HANDLE.Null)
            return null;

        try
        {
            // GetClipboardData returns HANDLE, GlobalSize expects HGLOBAL
            // They are both pointers to global memory, safe to convert via IntPtr
            var hGlobal = new HGLOBAL(hData.Value);
            var size = PInvoke.GlobalSize(hGlobal);
            return (long)size;
        }
        catch
        {
            return null;
        }
    }
}
