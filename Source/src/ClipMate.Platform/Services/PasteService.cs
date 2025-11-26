using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using ClipMate.Platform.Interop;

namespace ClipMate.Platform.Services;

/// <summary>
/// Service for pasting clipboard content to the active window using Win32 SendInput.
/// </summary>
public class PasteService : IPasteService
{
    private readonly IWin32InputInterop _win32;

    /// <summary>
    /// Initializes a new instance of the <see cref="PasteService" /> class.
    /// </summary>
    public PasteService(IWin32InputInterop win32Interop)
    {
        _win32 = win32Interop ?? throw new ArgumentNullException(nameof(win32Interop));
    }

    /// <inheritdoc />
    public async Task<bool> PasteToActiveWindowAsync(Clip? clip, CancellationToken cancellationToken = default)
    {
        if (clip == null)
            return false;

        try
        {
            // Set clipboard content based on clip type
            if (clip.Type == ClipType.Text && !string.IsNullOrEmpty(clip.TextContent))
                return await PasteTextAsync(clip.TextContent, cancellationToken);

            // TODO: Handle other clip types (RTF, HTML, Image) in future iterations
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> PasteTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        try
        {
            // Set clipboard content
            await Task.Run(() => Clipboard.SetText(text), cancellationToken);

            // Small delay to ensure clipboard is set
            await Task.Delay(50, cancellationToken);

            // Send Ctrl+V to active window
            SendCtrlV();

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public string GetActiveWindowTitle()
    {
        try
        {
            var foregroundWindow = _win32.GetForegroundWindow();
            if (foregroundWindow.IsNull)
                return string.Empty;

            const int maxLength = 256;
            unsafe
            {
                var buffer = stackalloc char[maxLength];
                var length = _win32.GetWindowText(foregroundWindow, buffer, maxLength);
                if (length > 0)
                    return new string(buffer, 0, length);
            }

            return string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <inheritdoc />
    public string GetActiveWindowProcessName()
    {
        try
        {
            var foregroundWindow = _win32.GetForegroundWindow();
            if (foregroundWindow.IsNull)
                return string.Empty;

            _win32.GetWindowThreadProcessId(foregroundWindow, out var processId);

            if (processId == 0)
                return string.Empty;

            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Sends Ctrl+V key combination to the active window using SendInput.
    /// </summary>
    private void SendCtrlV()
    {
        const int inputCount = 4; // Ctrl down, V down, V up, Ctrl up
        unsafe
        {
            var inputs = stackalloc INPUT[inputCount];

            // Ctrl key down
            inputs[0] = new INPUT
            {
                type = INPUT_TYPE.INPUT_KEYBOARD,
                Anonymous = new INPUT._Anonymous_e__Union
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = VIRTUAL_KEY.VK_CONTROL,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = 0,
                    },
                },
            };

            // V key down
            inputs[1] = new INPUT
            {
                type = INPUT_TYPE.INPUT_KEYBOARD,
                Anonymous = new INPUT._Anonymous_e__Union
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = VIRTUAL_KEY.VK_V,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = 0,
                    },
                },
            };

            // V key up
            inputs[2] = new INPUT
            {
                type = INPUT_TYPE.INPUT_KEYBOARD,
                Anonymous = new INPUT._Anonymous_e__Union
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = VIRTUAL_KEY.VK_V,
                        wScan = 0,
                        dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = 0,
                    },
                },
            };

            // Ctrl key up
            inputs[3] = new INPUT
            {
                type = INPUT_TYPE.INPUT_KEYBOARD,
                Anonymous = new INPUT._Anonymous_e__Union
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = VIRTUAL_KEY.VK_CONTROL,
                        wScan = 0,
                        dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = 0,
                    },
                },
            };

            // Send the inputs
            _win32.SendInput(inputCount, inputs, Marshal.SizeOf<INPUT>());
        }
    }
}
