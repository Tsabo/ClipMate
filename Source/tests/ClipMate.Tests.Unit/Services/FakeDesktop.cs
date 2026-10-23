using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using ClipMate.Platform.Interop;

namespace ClipMate.Tests.Unit.Services;

/// <summary>
/// A simulated desktop for auto-targeting tests: top-level windows in a window stack (Z-order), a foreground window
/// and the foreground-change event. Windows of the test process count as ClipMate's own.
/// </summary>
internal sealed class FakeDesktop : IWin32InputInterop, IForegroundWindowMonitor
{
    private const string _clipMateProcessName = "ClipMate.App";

    private readonly Dictionary<string, uint> _processIds = new(StringComparer.OrdinalIgnoreCase)
    {
        [_clipMateProcessName] = (uint)Environment.ProcessId,
    };

    // Top of the window stack first
    private readonly List<FakeWindow> _stack = [];
    private nint _nextHandle = 0x1000;
    private uint _nextProcessId = 50_000;

    public event Action<HWND>? ForegroundChanged;

    public FakeWindow? Foreground { get; private set; }

    /// <summary>
    /// Windows that ClipMate asked to bring to the front, in order.
    /// </summary>
    public List<nint> ForegroundRequests { get; } = [];

    public bool IsStarted { get; private set; }

    /// <summary>
    /// Opens a window at the top of the stack without activating it.
    /// </summary>
    public FakeWindow Open(string processName, string className, string title)
    {
        if (!_processIds.TryGetValue(processName, out var processId))
        {
            processId = _nextProcessId++;
            _processIds[processName] = processId;
        }

        var window = new FakeWindow(_nextHandle++, processId, processName, className, title);
        _stack.Insert(0, window);

        return window;
    }

    public FakeWindow OpenClipMate(string title = "ClipMate Explorer") => Open(_clipMateProcessName, "HwndWrapper[ClipMate.App;;1]", title);

    /// <summary>
    /// Brings the window to the top of the stack and the foreground, raising the foreground-change event.
    /// </summary>
    public void Activate(FakeWindow window)
    {
        _stack.Remove(window);
        _stack.Insert(0, window);
        Foreground = window;
        ForegroundChanged?.Invoke(new HWND(window.Handle));
    }

    public void Close(FakeWindow window)
    {
        _stack.Remove(window);
        if (Foreground == window)
            Foreground = null;
    }

    public void Start() => IsStarted = true;

    public void Dispose()
    {
    }

    public HWND GetForegroundWindow() => new(Foreground?.Handle ?? 0);

    public unsafe int GetWindowText(HWND hwnd, char* lpString, int nMaxCount) =>
        Find(hwnd) is { } window
            ? Copy(window.Title, lpString, nMaxCount)
            : 0;

    public uint GetWindowThreadProcessId(HWND hWnd, out uint lpdwProcessId)
    {
        lpdwProcessId = Find(hWnd)?.ProcessId ?? 0;
        return lpdwProcessId == 0
            ? 0u
            : 1u;
    }

    public unsafe uint SendInput(uint cInputs, INPUT* pInputs, int cbSize) => cInputs;

    public unsafe int GetClassName(HWND hWnd, char* lpClassName, int nMaxCount) =>
        Find(hWnd) is { } window
            ? Copy(window.ClassName, lpClassName, nMaxCount)
            : 0;

    public bool SetForegroundWindow(HWND hWnd)
    {
        ForegroundRequests.Add((nint)hWnd);
        return true;
    }

    public HWND GetWindow(HWND hWnd, GET_WINDOW_CMD uCmd)
    {
        if (uCmd != GET_WINDOW_CMD.GW_HWNDNEXT)
            throw new NotSupportedException($"{uCmd} is not simulated");

        var index = _stack.FindIndex(p => p.Handle == (nint)hWnd);
        return index >= 0 && index + 1 < _stack.Count
            ? new HWND(_stack[index + 1].Handle)
            : HWND.Null;
    }

    public bool IsWindow(HWND hWnd) => Find(hWnd) != null;

    public bool IsWindowVisible(HWND hWnd) => Find(hWnd)?.IsVisible ?? false;

    public bool IsWindowCloaked(HWND hWnd) => Find(hWnd)?.IsCloaked ?? false;

    public string? GetProcessName(uint processId) =>
        _processIds.FirstOrDefault(p => p.Value == processId).Key;

    private FakeWindow? Find(HWND hwnd) => _stack.FirstOrDefault(p => p.Handle == (nint)hwnd);

    private static unsafe int Copy(string text, char* buffer, int maxCount)
    {
        var length = Math.Min(text.Length, maxCount - 1);
        for (var i = 0; i < length; i++)
            buffer[i] = text[i];

        buffer[length] = '\0';
        return length;
    }
}

/// <summary>
/// A top-level window on a <see cref="FakeDesktop" />.
/// </summary>
internal sealed class FakeWindow(nint handle, uint processId, string processName, string className, string title)
{
    public nint Handle { get; } = handle;

    public uint ProcessId { get; } = processId;

    public string ProcessName { get; } = processName;

    public string ClassName { get; } = className;

    public string Title { get; set; } = title;

    public bool IsVisible { get; set; } = true;

    public bool IsCloaked { get; set; }
}
