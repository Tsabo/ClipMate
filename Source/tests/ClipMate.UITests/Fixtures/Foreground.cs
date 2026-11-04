using System.Runtime.InteropServices;

namespace ClipMate.UITests.Fixtures;

/// <summary>
/// Switches the foreground window as a user clicking another window would. Windows only lets the process that owns
/// the foreground (or shares its input state) change it, so the test thread attaches to the foreground thread's input
/// for the switch.
/// </summary>
public static partial class Foreground
{
    /// <summary>
    /// Brings <paramref name="handle" /> to the foreground and waits until Windows reports it there.
    /// </summary>
    public static Task ActivateAsync(nint handle, string what) => Poll.UntilAsync(() =>
    {
        if (GetForegroundWindow() == handle)
            return true;

        var foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        var currentThread = GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != currentThread &&
                       AttachThreadInput(currentThread, foregroundThread, true);
        try
        {
            if (IsIconic(handle))
                ShowWindow(handle, _swRestore);

            BringWindowToTop(handle);
            SetForegroundWindow(handle);
        }
        finally
        {
            if (attached)
                AttachThreadInput(currentThread, foregroundThread, false);
        }

        return GetForegroundWindow() == handle;
    }, $"{what} to come to the foreground");

    /// <summary>
    /// Presses and releases a key; the input goes to the foreground window.
    /// </summary>
    public static void PressKey(ushort virtualKey)
    {
        keybd_event((byte)virtualKey, 0, 0, 0);
        keybd_event((byte)virtualKey, 0, _keyeventfKeyUp, 0);
    }

    /// <summary>
    /// Presses a key combination, such as Ctrl+Alt+Shift+F9, and releases it in reverse order.
    /// </summary>
    public static void PressKeys(params ushort[] virtualKeys)
    {
        foreach (var item in virtualKeys)
            keybd_event((byte)item, 0, 0, 0);

        foreach (var item in virtualKeys.Reverse())
            keybd_event((byte)item, 0, _keyeventfKeyUp, 0);
    }

    /// <summary>
    /// Clicks the middle of <paramref name="bounds" /> (screen coordinates) with the mouse.
    /// </summary>
    public static void LeftClick(System.Windows.Rect bounds)
    {
        SetCursorPos((int)(bounds.Left + bounds.Width / 2), (int)(bounds.Top + bounds.Height / 2));
        mouse_event(_mouseeventfLeftDown, 0, 0, 0, 0);
        mouse_event(_mouseeventfLeftUp, 0, 0, 0, 0);
    }

    /// <summary>
    /// Clicks the middle of <paramref name="bounds" /> only if the window under that point belongs to
    /// <paramref name="processId" />, so a window of another program that has come on top is never clicked.
    /// </summary>
    /// <returns>False if another program's window is there; nothing was clicked.</returns>
    public static bool TryLeftClickIn(int processId, System.Windows.Rect bounds)
    {
        // WindowFromPoint takes a POINT by value, which on x64 is the two coordinates packed into one register
        var x = (int)(bounds.Left + bounds.Width / 2);
        var y = (int)(bounds.Top + bounds.Height / 2);
        GetWindowThreadProcessId(WindowFromPoint(((long)y << 32) | (uint)x), out var owner);
        if (owner != processId)
            return false;

        LeftClick(bounds);
        return true;
    }

    /// <summary>
    /// Right-clicks the middle of <paramref name="bounds" /> (screen coordinates) with the mouse.
    /// </summary>
    public static void RightClick(System.Windows.Rect bounds)
    {
        SetCursorPos((int)(bounds.Left + bounds.Width / 2), (int)(bounds.Top + bounds.Height / 2));
        mouse_event(_mouseeventfRightDown, 0, 0, 0, 0);
        mouse_event(_mouseeventfRightUp, 0, 0, 0, 0);
    }

    public const ushort VkReturn = 0x0D;
    public const ushort VkShift = 0x10;
    public const ushort VkControl = 0x11;
    public const ushort VkAlt = 0x12;
    public const ushort VkF9 = 0x78;

    private const int _swRestore = 9;
    private const uint _keyeventfKeyUp = 0x0002;
    private const uint _mouseeventfLeftDown = 0x0002;
    private const uint _mouseeventfLeftUp = 0x0004;
    private const uint _mouseeventfRightDown = 0x0008;
    private const uint _mouseeventfRightUp = 0x0010;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetCursorPos(int x, int y);

    [LibraryImport("user32.dll")]
    private static partial void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, nuint dwExtraInfo);

    [LibraryImport("user32.dll")]
    private static partial nint WindowFromPoint(long point);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool BringWindowToTop(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(nint hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [LibraryImport("user32.dll")]
    private static partial void keybd_event(byte bVk, byte bScan, uint dwFlags, nuint dwExtraInfo);
}
