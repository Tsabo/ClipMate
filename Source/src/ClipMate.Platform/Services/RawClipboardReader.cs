using Windows.Win32.Foundation;
using ClipMate.Core.Models;
using ClipMate.Platform.Interop;
using Microsoft.Extensions.Logging;

namespace ClipMate.Platform.Services;

/// <summary>
/// What one read of the clipboard found.
/// </summary>
/// <param name="FormatCount">How many formats the clipboard offered, stored or not; 0 means it was empty.</param>
/// <param name="Formats">The formats read, in the source's order.</param>
/// <param name="IgnoreFlag">The change asked clipboard viewers to ignore it, so nothing was read.</param>
/// <param name="Owner">The window that owned the clipboard while it was read, or null.</param>
internal sealed record ClipboardSnapshot(int FormatCount, IReadOnlyList<RawClipboardFormat> Formats, bool IgnoreFlag, HWND Owner);

/// <summary>
/// Reads the clipboard the way ClipMate 7 does: opened once, every format read raw in the order the source offered
/// them, then closed. Opening and each read are tried up to four times, 100 ms apart, with the clipboard closed while
/// waiting. Decoding is left to the caller, after the clipboard is closed.
/// </summary>
internal sealed class RawClipboardReader
{
    public const int MaxAttempts = 4;
    public static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);

    // ClipMate 7's never-capture list, plus formats whose data is a GDI handle rather than memory (the DIB covers
    // CF_BITMAP) and OLE's own formats, which point into the source process
    private static readonly HashSet<uint> _skippedCodes =
    [
        (uint)Formats.OemText.Code, 0x0081 /* CF_DSPTEXT */, (uint)Formats.Palette.Code,
        (uint)Formats.Bitmap.Code, (uint)Formats.Metafilepict.Code, (uint)Formats.EnhMetafile.Code,
    ];

    private static readonly HashSet<string> _skippedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ClipMate Ping", "Borland IDE Block Type",
        "DataObject", "Ole Private Data", "OleClipboardPersistOnFlush",
        ClipboardIgnoreFormats.ClipboardViewerIgnore, ClipboardIgnoreFormats.ExcludeFromMonitorProcessing,
    };

    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IWin32ClipboardInterop _win32;

    public RawClipboardReader(IWin32ClipboardInterop win32, TimeProvider timeProvider, ILogger logger)
    {
        _win32 = win32 ?? throw new ArgumentNullException(nameof(win32));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Reads every format on the clipboard; null if the clipboard could not be opened.
    /// </summary>
    /// <param name="window">The window that opens the clipboard.</param>
    /// <param name="honourIgnoreFlag">Read nothing when the change carries a clipboard-viewer ignore format.</param>
    public async Task<ClipboardSnapshot?> ReadAsync(HWND window, bool honourIgnoreFlag)
    {
        if (!await OpenAsync(window))
        {
            _logger.LogWarning("Could not open the clipboard after {Attempts} tries", MaxAttempts);

            return null;
        }

        var open = true;
        try
        {
            var owner = _win32.GetClipboardOwner();
            var offered = ListFormats();
            if (honourIgnoreFlag && offered.Any(p => p.Name != null && ClipboardIgnoreFormats.All.Contains(p.Name)))
                return new ClipboardSnapshot(offered.Count, [], true, owner);

            var formats = new List<RawClipboardFormat>();
            foreach (var (code, name) in offered)
            {
                if (name == null || _skippedCodes.Contains(code) || _skippedNames.Contains(name))
                    continue;

                for (var attempt = 1; ; attempt++)
                {
                    if (_win32.GetClipboardData(code) is { } data)
                    {
                        formats.Add(new RawClipboardFormat(name, code, data));

                        break;
                    }

                    if (attempt == MaxAttempts)
                    {
                        _logger.LogDebug("No data for clipboard format {Format} after {Attempts} tries", name, MaxAttempts);

                        break;
                    }

                    _win32.CloseClipboard();
                    open = false;
                    await Task.Delay(RetryDelay, _timeProvider);
                    if (!await OpenAsync(window))
                    {
                        _logger.LogWarning("Could not reopen the clipboard to read {Format}", name);

                        return new ClipboardSnapshot(offered.Count, formats, false, owner);
                    }

                    open = true;
                }
            }

            return new ClipboardSnapshot(offered.Count, formats, false, owner);
        }
        finally
        {
            if (open)
                _win32.CloseClipboard();
        }
    }

    private async Task<bool> OpenAsync(HWND window)
    {
        for (var attempt = 1; ; attempt++)
        {
            if (_win32.OpenClipboard(window))
                return true;

            if (attempt == MaxAttempts)
                return false;

            await Task.Delay(RetryDelay, _timeProvider);
        }
    }

    private List<(uint Code, string? Name)> ListFormats()
    {
        var formats = new List<(uint, string?)>();
        uint code = 0;
        while ((code = _win32.EnumClipboardFormats(code)) != 0)
        {
            var name = Formats.TryGetStandardFormatName(code, out var standardName) ? standardName : _win32.GetClipboardFormatName(code);
            formats.Add((code, name));
        }

        return formats;
    }
}
