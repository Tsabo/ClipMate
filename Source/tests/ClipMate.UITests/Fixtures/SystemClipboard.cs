using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace ClipMate.UITests.Fixtures;

/// <summary>
/// The real Windows clipboard, shared with every other program, accessed from a dedicated STA thread (OLE clipboard
/// calls require one; test threads are MTA).
/// </summary>
public static partial class SystemClipboard
{
    /// <summary>
    /// <c>[NotInParallel]</c> key for every test that uses the clipboard: there is only one, and each test's ClipMate
    /// captures whatever any other test puts on it.
    /// </summary>
    public const string ParallelKey = "SystemClipboard";

    /// <summary>
    /// Runs <paramref name="action" /> on a new STA thread and returns its result.
    /// </summary>
    public static T OnSta<T>(Func<T> action)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        return error == null
            ? result
            : throw new InvalidOperationException("Clipboard operation failed", error);
    }

    public static void OnSta(Action action) => OnSta(() =>
    {
        action();
        return 0;
    });

    /// <summary>
    /// Copies every format currently on the clipboard that can be read, so a test can put it back afterwards.
    /// </summary>
    public static DataObject Backup() => OnSta(() =>
    {
        var copy = new DataObject();
        var current = Clipboard.GetDataObject();
        if (current == null)
            return copy;

        foreach (var item in current.GetFormats(false))
        {
            try
            {
                var data = current.GetData(item, false);
                if (data != null)
                    copy.SetData(item, data, false);
            }
            catch (COMException)
            {
                // Some owners refuse to render a format on demand; the rest is still worth restoring
            }
        }

        return copy;
    });

    public static void Restore(DataObject backup) => OnSta(() => Clipboard.SetDataObject(backup, true));

    /// <summary>
    /// Names of the formats on the clipboard as Windows reports them (EnumClipboardFormats), so formats WPF would
    /// synthesize on read do not count. Standard formats are reported as "#code". Null while another program holds
    /// the clipboard open; poll again.
    /// </summary>
    public static IReadOnlyList<string>? TryGetRawFormatNames()
    {
        if (!OpenClipboard(IntPtr.Zero))
            return null;

        try
        {
            var names = new List<string>();
            var buffer = new StringBuilder(256);
            for (var format = EnumClipboardFormats(0); format != 0; format = EnumClipboardFormats(format))
            {
                buffer.Clear();
                names.Add(GetClipboardFormatName(format, buffer, buffer.Capacity) > 0
                    ? buffer.ToString()
                    : $"#{format}");
            }

            return names;
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>
    /// True when the clipboard's current content was put there by <paramref name="process" />.
    /// </summary>
    public static bool IsOwnedBy(System.Diagnostics.Process process)
    {
        var owner = GetClipboardOwner();
        if (owner == IntPtr.Zero)
            return false;

        GetWindowThreadProcessId(owner, out var processId);
        return processId == process.Id;
    }

    /// <summary>
    /// Reads the clipboard's text the way an application pastes it: OpenClipboard with <paramref name="window" />
    /// (IntPtr.Zero opens it without a window, as a background reader may), then GetClipboardData(CF_UNICODETEXT),
    /// which makes a delayed-rendering owner render it. Null while another program holds the clipboard open or when
    /// there is no text.
    /// </summary>
    public static string? TryReadText(IntPtr window)
    {
        const uint cfUnicodeText = 13;

        if (!OpenClipboard(window))
            return null;

        try
        {
            var handle = GetClipboardData(cfUnicodeText);
            if (handle == IntPtr.Zero)
                return null;

            var pointer = GlobalLock(handle);
            try
            {
                return pointer == IntPtr.Zero ? null : Marshal.PtrToStringUni(pointer);
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>
    /// The title of the window that owns the clipboard's current content, empty if it has none.
    /// </summary>
    public static string GetOwnerWindowTitle()
    {
        var owner = GetClipboardOwner();
        if (owner == IntPtr.Zero)
            return string.Empty;

        var buffer = new StringBuilder(256);
        return GetWindowText(owner, buffer, buffer.Capacity) > 0 ? buffer.ToString() : string.Empty;
    }

    /// <summary>
    /// Windows' count of clipboard changes; it moves on every change, including a re-registration by the owner.
    /// </summary>
    public static uint SequenceNumber => GetClipboardSequenceNumber();

    [LibraryImport("user32.dll")]
    private static partial uint GetClipboardSequenceNumber();

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetClipboardOwner();

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetClipboardData(uint format);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GlobalLock(IntPtr handle);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(IntPtr handle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenClipboard(IntPtr hWndNewOwner);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseClipboard();

    [LibraryImport("user32.dll")]
    private static partial uint EnumClipboardFormats(uint format);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClipboardFormatName(uint format, StringBuilder lpszFormatName, int cchMaxCount);
}
