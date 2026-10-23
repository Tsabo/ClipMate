using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ClipMate.Platform.Interop;

/// <summary>
/// Default implementation of <see cref="IWin32InputInterop" /> that delegates to CsWin32 PInvoke methods.
/// </summary>
public class Win32InputInterop : IWin32InputInterop
{
    /// <inheritdoc />
    public HWND GetForegroundWindow() => PInvoke.GetForegroundWindow();

    /// <inheritdoc />
    public unsafe int GetWindowText(HWND hwnd, char* lpString, int nMaxCount) =>
        PInvoke.GetWindowText(hwnd, lpString, nMaxCount);

    /// <inheritdoc />
    public uint GetWindowThreadProcessId(HWND hWnd, out uint lpdwProcessId) =>
        PInvoke.GetWindowThreadProcessId(hWnd, out lpdwProcessId);

    /// <inheritdoc />
    public unsafe uint SendInput(uint cInputs, INPUT* pInputs, int cbSize) =>
        PInvoke.SendInput(cInputs, pInputs, cbSize);

    /// <inheritdoc />
    public unsafe int GetClassName(HWND hWnd, char* lpClassName, int nMaxCount) =>
        PInvoke.GetClassName(hWnd, lpClassName, nMaxCount);

    /// <inheritdoc />
    public bool SetForegroundWindow(HWND hWnd)
    {
        unsafe
        {
            return SetForegroundWindow((IntPtr)hWnd.Value);
        }
    }

    /// <inheritdoc />
    public HWND GetWindow(HWND hWnd, GET_WINDOW_CMD uCmd) => PInvoke.GetWindow(hWnd, uCmd);

    /// <inheritdoc />
    public bool IsWindow(HWND hWnd) => PInvoke.IsWindow(hWnd);

    /// <inheritdoc />
    public bool IsWindowVisible(HWND hWnd) => PInvoke.IsWindowVisible(hWnd);

    /// <inheritdoc />
    public bool IsWindowCloaked(HWND hWnd)
    {
        unsafe
        {
            uint cloaked = 0;
            var result = PInvoke.DwmGetWindowAttribute(hWnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint));
            return result.Succeeded && cloaked != 0;
        }
    }

    /// <inheritdoc />
    public string? GetProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            // The process has exited
            return null;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
