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

    public const ushort VkReturn = 0x0D;

    private const int _swRestore = 9;
    private const uint _keyeventfKeyUp = 0x0002;

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
