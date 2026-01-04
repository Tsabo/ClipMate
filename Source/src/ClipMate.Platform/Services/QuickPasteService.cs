using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services;
using ClipMate.Platform.Interop;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace ClipMate.Platform.Services;

/// <summary>
/// Service implementing QuickPaste functionality including auto-targeting,
/// formatting string execution, and keystroke sending to target applications.
/// </summary>
public class QuickPasteService : IQuickPasteService
{
    private readonly IClipboardService _clipboardService;
    private readonly IConfigurationService _configurationService;
    private readonly IDialogService _dialogService;
    private readonly ILogger<QuickPasteService> _logger;
    private readonly IMacroExecutionService _macroExecutionService;
    private readonly IMessenger _messenger;
    private readonly ITemplateService _templateService;
    private readonly IWin32InputInterop _win32;
    private nint _clipMateWindowHandle; // Store ClipMate's window handle for GoBack functionality
    private (string ProcessName, string ClassName, string WindowTitle)? _currentTarget;
    private nint _currentTargetWindowHandle; // Store the window handle for focus switching

    private bool _goBackEnabled;
    private QuickPasteFormattingString? _selectedFormattingString;
    private int _sequenceCounter = 1;
    private bool _targetLocked;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuickPasteService" /> class.
    /// </summary>
    public QuickPasteService(IWin32InputInterop win32Interop,
        IClipboardService clipboardService,
        IConfigurationService configurationService,
        IMessenger messenger,
        IMacroExecutionService macroExecutionService,
        ITemplateService templateService,
        IDialogService dialogService,
        ILogger<QuickPasteService> logger)
    {
        _win32 = win32Interop ?? throw new ArgumentNullException(nameof(win32Interop));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _macroExecutionService = macroExecutionService ?? throw new ArgumentNullException(nameof(macroExecutionService));
        _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Subscribe to configuration changes for immediate reload
        _messenger.Register<QuickPasteConfigurationChangedEvent>(this, (_, _) => OnConfigurationChanged());

        // Subscribe to QuickPaste action events from menu commands
        _messenger.Register<QuickPasteSendTabEvent>(this, (_, _) => SendTabKeystroke());
        _messenger.Register<QuickPasteSendEnterEvent>(this, (_, _) => SendEnterKeystroke());

        // Select default formatting string (one with TitleTrigger = "*")
        SelectDefaultFormattingString();
    }

    /// <inheritdoc />
    public (string ProcessName, string ClassName, string WindowTitle)? GetCurrentTarget() => _currentTarget;

    /// <inheritdoc />
    public void SetTargetLock(bool locked)
    {
        _targetLocked = locked;
        _logger.LogDebug("Target lock set to {Locked}", locked);
        _messenger.Send(new StateRefreshRequestedEvent());
    }

    /// <inheritdoc />
    public bool IsTargetLocked() => _targetLocked;

    /// <inheritdoc />
    public bool GetGoBackState() => _goBackEnabled;

    /// <inheritdoc />
    public void SetGoBackState(bool goBack)
    {
        _goBackEnabled = goBack;
        _logger.LogDebug("GoBack state set to {GoBack}", goBack);
        _messenger.Send(new StateRefreshRequestedEvent());
    }

    /// <inheritdoc />
    public QuickPasteFormattingString? GetSelectedFormattingString() => _selectedFormattingString;

    /// <inheritdoc />
    public void SelectFormattingString(QuickPasteFormattingString? format)
    {
        _selectedFormattingString = format;
        _logger.LogDebug("Selected formatting string: {Title}", format?.Title ?? "None");
    }

    /// <inheritdoc />
    public async Task<bool> PasteClipAsync(Clip clip, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (_currentTarget == null)
        {
            _logger.LogWarning("No target application selected for QuickPaste");

            return false;
        }

        try
        {
            _logger.LogDebug("QuickPaste: Pasting clip {ClipId} to target {Target}",
                clip.Id, GetCurrentTargetString());

            // Update target if not locked - ensures we have the most recent foreground window
            // This handles cases where user switches windows before double-clicking
            if (!_targetLocked)
            {
                UpdateTarget();
                _logger.LogDebug("Updated target before paste: {Target}", GetCurrentTargetString());
            }

            // Capture ClipMate's window handle for GoBack functionality (before switching away)
            if (_goBackEnabled)
            {
                var clipMateWindow = _win32.GetForegroundWindow();
                if (!clipMateWindow.IsNull)
                {
                    unsafe
                    {
                        _clipMateWindowHandle = (nint)clipMateWindow.Value;
                        _logger.LogDebug("Captured ClipMate window handle for GoBack: {Handle:X}", _clipMateWindowHandle);
                    }
                }
            }

            // Switch focus to target application and wait for it to activate
            if (_currentTargetWindowHandle != IntPtr.Zero)
            {
                _logger.LogDebug("Switching focus to target window handle: {Handle:X}", _currentTargetWindowHandle);
                _win32.SetForegroundWindow(new HWND(_currentTargetWindowHandle));

                // Wait longer for focus switch to complete - give the window time to activate
                // This is especially important for macro execution which requires the window to be active
                await Task.Delay(300, cancellationToken);
            }
            else
                _logger.LogWarning("No valid window handle for target application");

            // Check if clip is a macro - if so, execute as macro instead of clipboard paste
            _logger.LogDebug("Checking macro execution: Macro={Macro}, HasTextContent={HasText}",
                clip.Macro, !string.IsNullOrEmpty(clip.TextContent));

            if (clip.Macro && !string.IsNullOrEmpty(clip.TextContent))
            {
                _logger.LogInformation("Executing clip {ClipId} as macro", clip.Id);

                // Apply template tag replacement (#DATE#, #TIME#, #CREATOR#, etc.)
                var macroText = _templateService.ReplaceTagsInText(clip.TextContent, clip, _sequenceCounter);
                _sequenceCounter++; // Increment for next macro

                _logger.LogDebug("Macro text after tag replacement: {Text}", macroText);

                // Security check before executing macro
                if (!_macroExecutionService.IsMacroSafe(macroText))
                {
                    _logger.LogWarning("Macro contains potentially dangerous commands - showing confirmation dialog");

                    var result = _dialogService.ShowMessage(
                        "This macro contains potentially dangerous commands (e.g., multiple Alt+F4 keystrokes).\n\n" +
                        $"Macro text:\n{macroText}\n\n" +
                        "Do you want to execute it anyway?",
                        "Macro Security Warning",
                        DialogButton.YesNo,
                        DialogIcon.Warning);

                    if (result != DialogResult.Yes)
                    {
                        _logger.LogInformation("User declined to execute potentially dangerous macro");
                        return false;
                    }

                    _logger.LogInformation("User confirmed execution of potentially dangerous macro");
                }

                var success = await _macroExecutionService.ExecuteMacroAsync(macroText, cancellationToken);
                if (!success)
                {
                    _logger.LogWarning("Macro execution failed or was cancelled");
                    return false;
                }
            }
            else
            {
                // Normal clipboard paste
                // Set clipboard content
                await _clipboardService.SetClipboardContentAsync(clip, cancellationToken);
                await Task.Delay(50, cancellationToken);

                // Select formatting string based on title trigger if no manual selection
                var format = _selectedFormattingString ?? SelectFormattingStringByTarget();

                // Execute the formatting string
                await ExecuteFormattingStringAsync(clip, format, cancellationToken);
            }

            _logger.LogInformation("Successfully pasted clip {ClipId} via QuickPaste", clip.Id);

            // Handle GoBack functionality - return focus to ClipMate
            if (_goBackEnabled && _clipMateWindowHandle != IntPtr.Zero)
            {
                _logger.LogDebug("GoBack enabled - returning focus to ClipMate window: {Handle:X}", _clipMateWindowHandle);

                // Small delay to ensure paste operation completes
                await Task.Delay(50, cancellationToken);

                // Switch focus back to ClipMate
                _win32.SetForegroundWindow(new HWND(_clipMateWindowHandle));
                _logger.LogDebug("Focus returned to ClipMate");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pasting clip {ClipId} via QuickPaste", clip.Id);

            return false;
        }
    }

    /// <inheritdoc />
    public void SendTabKeystroke() => SendSpecialKey(VIRTUAL_KEY.VK_TAB);

    /// <inheritdoc />
    public void SendEnterKeystroke() => SendSpecialKey(VIRTUAL_KEY.VK_RETURN);

    /// <inheritdoc />
    public void ResetSequence()
    {
        _sequenceCounter = 1;
        _logger.LogDebug("Sequence counter reset to 1");
    }

    /// <inheritdoc />
    public void UpdateTarget()
    {
        // Check if auto-targeting is enabled in configuration
        var config = _configurationService.Configuration.Preferences;
        if (!config.QuickPasteAutoTargetingEnabled)
        {
            _logger.LogDebug("Auto-targeting is disabled in configuration");

            return;
        }

        if (_targetLocked)
        {
            _logger.LogDebug("Target is locked, skipping update");

            return;
        }

        var result = DetectTargetWindow();
        if (result.target == null)
            return;

        _currentTarget = result.target;
        _currentTargetWindowHandle = result.windowHandle;
        _logger.LogInformation("Target updated to: {Target} (Handle: {Handle:X})", GetCurrentTargetString(), _currentTargetWindowHandle);
        _messenger.Send(new QuickPasteTargetChangedEvent());
    }

    /// <inheritdoc />
    public string GetCurrentTargetString() => _currentTarget == null
        ? string.Empty
        : $"{_currentTarget.Value.ProcessName}:{_currentTarget.Value.ClassName}";

    private void OnConfigurationChanged()
    {
        _logger.LogInformation("QuickPaste configuration changed, reloading settings");
        SelectDefaultFormattingString();
    }

    private void SelectDefaultFormattingString()
    {
        var config = _configurationService.Configuration.Preferences;

        // First try to find the default formatting string (with TitleTrigger = "*")
        _selectedFormattingString = config.QuickPasteFormattingStrings
            .FirstOrDefault(p => p.TitleTrigger == "*");

        // If no default found, use the first formatting string
        if (_selectedFormattingString == null && config.QuickPasteFormattingStrings.Count > 0)
            _selectedFormattingString = config.QuickPasteFormattingStrings[0];

        _logger.LogDebug("Default formatting string selected: {Title}",
            _selectedFormattingString?.Title ?? "None");
    }

    private QuickPasteFormattingString? SelectFormattingStringByTarget()
    {
        if (_currentTarget == null)
            return _selectedFormattingString;

        var config = _configurationService.Configuration.Preferences;
        var targetTitle = _currentTarget.Value.WindowTitle;

        // Find format with matching title trigger (case-insensitive substring match)
        var matchedFormat = config.QuickPasteFormattingStrings
            .FirstOrDefault(p => !string.IsNullOrEmpty(p.TitleTrigger) &&
                                 p.TitleTrigger != "*" &&
                                 targetTitle.Contains(p.TitleTrigger, StringComparison.OrdinalIgnoreCase));

        return matchedFormat ?? _selectedFormattingString;
    }

    private ((string ProcessName, string ClassName, string WindowTitle)? target, nint windowHandle) DetectTargetWindow()
    {
        try
        {
            var foregroundWindow = _win32.GetForegroundWindow();
            if (foregroundWindow.IsNull)
                return (null, IntPtr.Zero);

            // Get process name
            _win32.GetWindowThreadProcessId(foregroundWindow, out var processId);

            if (processId == 0)
                return (null, IntPtr.Zero);

            string processName;
            try
            {
                using var process = Process.GetProcessById((int)processId);
                processName = process.ProcessName.ToUpperInvariant();
            }
            catch
            {
                return (null, IntPtr.Zero);
            }

            // Get window class name
            const int maxLength = 256;
            string className;
            unsafe
            {
                var buffer = stackalloc char[maxLength];
                var length = _win32.GetClassName(foregroundWindow, buffer, maxLength);
                className = length > 0
                    ? new string(buffer, 0, length).ToUpperInvariant()
                    : string.Empty;
            }

            // Get window title
            string windowTitle;
            unsafe
            {
                var buffer = stackalloc char[maxLength];
                var length = _win32.GetWindowText(foregroundWindow, buffer, maxLength);
                windowTitle = length > 0
                    ? new string(buffer, 0, length)
                    : string.Empty;
            }

            // Check against good/bad target lists
            var targetString = $"{processName}:{className}";

            if (!IsValidTarget(targetString))
                return (null, IntPtr.Zero);

            // Store window handle for focus switching
            nint windowHandle;
            unsafe
            {
                windowHandle = (nint)foregroundWindow.Value;
            }

            return ((processName, className, windowTitle), windowHandle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting target window");

            return (null, IntPtr.Zero);
        }
    }

    private bool IsValidTarget(string targetString)
    {
        var config = _configurationService.Configuration.Preferences;

        // Check bad targets first (exclusion list)
        foreach (var item in config.QuickPasteBadTargets)
        {
            if (!MatchesTarget(targetString, item))
                continue;

            _logger.LogDebug("Target {Target} matches bad target {BadTarget}, rejecting",
                targetString, item);

            return false;
        }

        // Good targets are training hints for future window stack enumeration
        // For now, accept any target not in the bad list
        _logger.LogDebug("Target {Target} is valid (not in bad targets list)", targetString);

        return true;
    }

    private static bool MatchesTarget(string actual, string pattern)
    {
        // Pattern format: PROCESSNAME:CLASSNAME or PROCESSNAME: (match all classes for process)
        // Empty process name means match any process with that class
        var patternParts = pattern.Split(':', 2);
        var actualParts = actual.Split(':', 2);

        if (patternParts.Length != 2 || actualParts.Length != 2)
            return false;

        var patternProcess = patternParts[0];
        var patternClass = patternParts[1];
        var actualProcess = actualParts[0];
        var actualClass = actualParts[1];

        // Match process (empty pattern matches any, non-empty uses prefix match)
        if (!string.IsNullOrEmpty(patternProcess) &&
            !actualProcess.StartsWith(patternProcess, StringComparison.OrdinalIgnoreCase))
            return false;

        // Match class (empty pattern matches any, non-empty uses prefix match)
        return string.IsNullOrEmpty(patternClass) ||
               actualClass.StartsWith(patternClass, StringComparison.OrdinalIgnoreCase);
    }

    private async Task ExecuteFormattingStringAsync(Clip clip, QuickPasteFormattingString? format,
        CancellationToken cancellationToken)
    {
        if (format == null)
        {
            // Default: just send Ctrl+V
            SendCtrlV();

            return;
        }

        // Execute preamble
        if (!string.IsNullOrEmpty(format.Preamble))
            await ExecuteKeystrokesAsync(format.Preamble, clip, cancellationToken);

        // Execute paste keystrokes
        if (!string.IsNullOrEmpty(format.PasteKeystrokes))
            await ExecuteKeystrokesAsync(format.PasteKeystrokes, clip, cancellationToken);

        // Execute postamble
        if (!string.IsNullOrEmpty(format.Postamble))
            await ExecuteKeystrokesAsync(format.Postamble, clip, cancellationToken);
    }

    private async Task ExecuteKeystrokesAsync(string keystrokes, Clip clip, CancellationToken cancellationToken)
    {
        var i = 0;
        while (i < keystrokes.Length)
        {
            // Check for macros (#MACRO#)
            if (keystrokes[i] == '#')
            {
                var endIndex = keystrokes.IndexOf('#', i + 1);
                if (endIndex > i)
                {
                    var macro = keystrokes.Substring(i + 1, endIndex - i - 1);
                    await ExecuteMacroAsync(macro, clip, cancellationToken);
                    i = endIndex + 1;

                    continue;
                }
            }

            // Check for special keys ({KEY}) or parametric commands ({PAUSE:ms})
            if (keystrokes[i] == '{')
            {
                var endIndex = keystrokes.IndexOf('}', i + 1);
                if (endIndex > i)
                {
                    var command = keystrokes.Substring(i + 1, endIndex - i - 1);

                    // Check for parametric PAUSE: {PAUSE:milliseconds}
                    if (command.StartsWith("PAUSE:", StringComparison.OrdinalIgnoreCase))
                    {
                        var delayPart = command.Substring(6); // Skip "PAUSE:"
                        if (int.TryParse(delayPart, out var delayMs))
                        {
                            // Clamp to 1-10000ms range (max 10 seconds)
                            delayMs = Math.Clamp(delayMs, 1, 10000);
                            await Task.Delay(delayMs, cancellationToken);
                        }
                        else
                            _logger.LogWarning("Invalid PAUSE parameter: {Command}", command);
                    }
                    else
                    {
                        // Regular special key
                        SendSpecialKeyByName(command);
                    }

                    i = endIndex + 1;
                    continue;
                }
            }

            // Check for modifier keys
            switch (keystrokes[i])
            {
                case '^': // Ctrl
                    if (i + 1 < keystrokes.Length)
                    {
                        SendCtrlKey(keystrokes[i + 1]);
                        i += 2;

                        continue;
                    }

                    break;

                case '~': // Shift
                    if (i + 1 < keystrokes.Length)
                    {
                        SendShiftKey(keystrokes[i + 1]);
                        i += 2;

                        continue;
                    }

                    break;

                case '@': // Alt
                    if (i + 1 < keystrokes.Length)
                    {
                        SendAltKey(keystrokes[i + 1]);
                        i += 2;

                        continue;
                    }

                    break;
            }

            // Send as literal character
            SendLiteralChar(keystrokes[i]);
            i++;
        }
    }

    private async Task ExecuteMacroAsync(string macro, Clip clip, CancellationToken cancellationToken)
    {
        switch (macro.ToUpperInvariant())
        {
            case "DATE":
                SendLiteralString(clip.CapturedAt.ToString("d", CultureInfo.CurrentCulture));

                break;

            case "TIME":
                SendLiteralString(clip.CapturedAt.ToString("t", CultureInfo.CurrentCulture));

                break;

            case "CURRENTDATE":
                SendLiteralString(DateTime.Now.ToString("d", CultureInfo.CurrentCulture));

                break;

            case "CURRENTTIME":
                SendLiteralString(DateTime.Now.ToString("t", CultureInfo.CurrentCulture));

                break;

            case "URL":
                SendLiteralString(clip.SourceUrl ?? string.Empty);

                break;

            case "CREATOR":
                SendLiteralString(clip.SourceApplicationName ?? string.Empty);

                break;

            case "TITLE":
                SendLiteralString(clip.Title ?? string.Empty);

                break;

            case "PAUSE":
                await Task.Delay(10, cancellationToken);

                break;

            case "SEQUENCE":
                SendLiteralString(_sequenceCounter.ToString(CultureInfo.InvariantCulture));
                _sequenceCounter++;

                break;

            case "FILENAME":
                SendLiteralString(GetFileNameFromClip(clip));

                break;

            case "FILEPATH":
                SendLiteralString(GetFilePathFromClip(clip));

                break;

            case "FILEEXT":
                SendLiteralString(GetFileExtensionFromClip(clip));

                break;

            default:
                // Unknown macro, send as literal
                SendLiteralString($"#{macro}#");
                _logger.LogWarning("Unknown macro: {Macro}", macro);

                break;
        }
    }

    private void SendCtrlV()
    {
        const int inputCount = 4; // Ctrl down, V down, V up, Ctrl up
        unsafe
        {
            var inputs = stackalloc INPUT[inputCount];

            inputs[0] = CreateKeyInput(VIRTUAL_KEY.VK_CONTROL, false);
            inputs[1] = CreateKeyInput(VIRTUAL_KEY.VK_V, false);
            inputs[2] = CreateKeyInput(VIRTUAL_KEY.VK_V, true);
            inputs[3] = CreateKeyInput(VIRTUAL_KEY.VK_CONTROL, true);

            _win32.SendInput(inputCount, inputs, Marshal.SizeOf<INPUT>());
        }
    }

    private void SendSpecialKey(VIRTUAL_KEY key)
    {
        unsafe
        {
            var inputs = stackalloc INPUT[2];
            inputs[0] = CreateKeyInput(key, false);
            inputs[1] = CreateKeyInput(key, true);

            _win32.SendInput(2, inputs, Marshal.SizeOf<INPUT>());
        }
    }

    private void SendSpecialKeyByName(string keyName)
    {
        var key = keyName.ToUpperInvariant() switch
        {
            "TAB" => VIRTUAL_KEY.VK_TAB,
            "ENTER" => VIRTUAL_KEY.VK_RETURN,
            "INSERT" => VIRTUAL_KEY.VK_INSERT,
            "DELETE" => VIRTUAL_KEY.VK_DELETE,
            "HOME" => VIRTUAL_KEY.VK_HOME,
            "END" => VIRTUAL_KEY.VK_END,
            "PAGEUP" => VIRTUAL_KEY.VK_PRIOR,
            "PAGEDOWN" => VIRTUAL_KEY.VK_NEXT,
            "UP" => VIRTUAL_KEY.VK_UP,
            "DOWN" => VIRTUAL_KEY.VK_DOWN,
            "LEFT" => VIRTUAL_KEY.VK_LEFT,
            "RIGHT" => VIRTUAL_KEY.VK_RIGHT,
            "ESCAPE" => VIRTUAL_KEY.VK_ESCAPE,
            "ESC" => VIRTUAL_KEY.VK_ESCAPE,
            var _ => VIRTUAL_KEY.VK_NONAME,
        };

        if (key != VIRTUAL_KEY.VK_NONAME)
            SendSpecialKey(key);
        else
        {
            // Unknown key, send as literal
            SendLiteralString($"{{{keyName}}}");
            _logger.LogWarning("Unknown special key: {KeyName}", keyName);
        }
    }

    private void SendCtrlKey(char key)
    {
        var vk = CharToVirtualKey(key);
        if (vk != VIRTUAL_KEY.VK_NONAME)
        {
            unsafe
            {
                var inputs = stackalloc INPUT[4];
                inputs[0] = CreateKeyInput(VIRTUAL_KEY.VK_CONTROL, false);
                inputs[1] = CreateKeyInput(vk, false);
                inputs[2] = CreateKeyInput(vk, true);
                inputs[3] = CreateKeyInput(VIRTUAL_KEY.VK_CONTROL, true);

                _win32.SendInput(4, inputs, Marshal.SizeOf<INPUT>());
            }
        }
    }

    private void SendShiftKey(char key)
    {
        var vk = CharToVirtualKey(key);
        if (vk != VIRTUAL_KEY.VK_NONAME)
        {
            unsafe
            {
                var inputs = stackalloc INPUT[4];
                inputs[0] = CreateKeyInput(VIRTUAL_KEY.VK_SHIFT, false);
                inputs[1] = CreateKeyInput(vk, false);
                inputs[2] = CreateKeyInput(vk, true);
                inputs[3] = CreateKeyInput(VIRTUAL_KEY.VK_SHIFT, true);

                _win32.SendInput(4, inputs, Marshal.SizeOf<INPUT>());
            }
        }
    }

    private void SendAltKey(char key)
    {
        var vk = CharToVirtualKey(key);
        if (vk != VIRTUAL_KEY.VK_NONAME)
        {
            unsafe
            {
                var inputs = stackalloc INPUT[4];
                inputs[0] = CreateKeyInput(VIRTUAL_KEY.VK_MENU, false);
                inputs[1] = CreateKeyInput(vk, false);
                inputs[2] = CreateKeyInput(vk, true);
                inputs[3] = CreateKeyInput(VIRTUAL_KEY.VK_MENU, true);

                _win32.SendInput(4, inputs, Marshal.SizeOf<INPUT>());
            }
        }
    }

    private void SendLiteralChar(char c)
    {
        // For simplicity, send as Unicode input
        unsafe
        {
            var inputs = stackalloc INPUT[2];

            inputs[0] = new INPUT
            {
                type = INPUT_TYPE.INPUT_KEYBOARD,
                Anonymous = new INPUT._Anonymous_e__Union
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = c,
                        dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE,
                        time = 0,
                        dwExtraInfo = 0,
                    },
                },
            };

            inputs[1] = new INPUT
            {
                type = INPUT_TYPE.INPUT_KEYBOARD,
                Anonymous = new INPUT._Anonymous_e__Union
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = c,
                        dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = 0,
                    },
                },
            };

            _win32.SendInput(2, inputs, Marshal.SizeOf<INPUT>());
        }
    }

    private void SendLiteralString(string text)
    {
        foreach (var c in text)
            SendLiteralChar(c);
    }

    private static VIRTUAL_KEY CharToVirtualKey(char c)
    {
        return char.ToUpperInvariant(c) switch
        {
            'A' => VIRTUAL_KEY.VK_A,
            'B' => VIRTUAL_KEY.VK_B,
            'C' => VIRTUAL_KEY.VK_C,
            'D' => VIRTUAL_KEY.VK_D,
            'E' => VIRTUAL_KEY.VK_E,
            'F' => VIRTUAL_KEY.VK_F,
            'G' => VIRTUAL_KEY.VK_G,
            'H' => VIRTUAL_KEY.VK_H,
            'I' => VIRTUAL_KEY.VK_I,
            'J' => VIRTUAL_KEY.VK_J,
            'K' => VIRTUAL_KEY.VK_K,
            'L' => VIRTUAL_KEY.VK_L,
            'M' => VIRTUAL_KEY.VK_M,
            'N' => VIRTUAL_KEY.VK_N,
            'O' => VIRTUAL_KEY.VK_O,
            'P' => VIRTUAL_KEY.VK_P,
            'Q' => VIRTUAL_KEY.VK_Q,
            'R' => VIRTUAL_KEY.VK_R,
            'S' => VIRTUAL_KEY.VK_S,
            'T' => VIRTUAL_KEY.VK_T,
            'U' => VIRTUAL_KEY.VK_U,
            'V' => VIRTUAL_KEY.VK_V,
            'W' => VIRTUAL_KEY.VK_W,
            'X' => VIRTUAL_KEY.VK_X,
            'Y' => VIRTUAL_KEY.VK_Y,
            'Z' => VIRTUAL_KEY.VK_Z,
            '0' => VIRTUAL_KEY.VK_0,
            '1' => VIRTUAL_KEY.VK_1,
            '2' => VIRTUAL_KEY.VK_2,
            '3' => VIRTUAL_KEY.VK_3,
            '4' => VIRTUAL_KEY.VK_4,
            '5' => VIRTUAL_KEY.VK_5,
            '6' => VIRTUAL_KEY.VK_6,
            '7' => VIRTUAL_KEY.VK_7,
            '8' => VIRTUAL_KEY.VK_8,
            '9' => VIRTUAL_KEY.VK_9,
            var _ => VIRTUAL_KEY.VK_NONAME,
        };
    }

    private static INPUT CreateKeyInput(VIRTUAL_KEY key, bool keyUp)
    {
        return new INPUT
        {
            type = INPUT_TYPE.INPUT_KEYBOARD,
            Anonymous = new INPUT._Anonymous_e__Union
            {
                ki = new KEYBDINPUT
                {
                    wVk = key,
                    wScan = 0,
                    dwFlags = keyUp
                        ? KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP
                        : 0,
                    time = 0,
                    dwExtraInfo = 0,
                },
            },
        };
    }

    /// <summary>
    /// Extracts the filename from a CF_HDROP clip (file list).
    /// Returns the first filename if multiple files are present.
    /// </summary>
    private static string GetFileNameFromClip(Clip clip)
    {
        var filePath = GetFilePathFromClip(clip);
        return string.IsNullOrEmpty(filePath)
            ? string.Empty
            : Path.GetFileName(filePath);
    }

    /// <summary>
    /// Extracts the full file path from a CF_HDROP clip (file list).
    /// Returns the first file path if multiple files are present.
    /// </summary>
    private static string GetFilePathFromClip(Clip clip)
    {
        // Check if clip has FilePathsJson (from CF_HDROP format)
        if (string.IsNullOrWhiteSpace(clip.FilePathsJson))
            return string.Empty;

        try
        {
            // FilePathsJson is a JSON array of file paths
            var filePaths = JsonSerializer.Deserialize<string[]>(clip.FilePathsJson);
            return filePaths?.Length > 0
                ? filePaths[0]
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Extracts the file extension from a CF_HDROP clip (file list).
    /// Returns the extension of the first file if multiple files are present.
    /// </summary>
    private static string GetFileExtensionFromClip(Clip clip)
    {
        var filePath = GetFilePathFromClip(clip);
        return string.IsNullOrEmpty(filePath)
            ? string.Empty
            : Path.GetExtension(filePath);
    }
}
