using Windows.Win32.UI.Input.KeyboardAndMouse;
using ClipMate.Core.Services;
using ClipMate.Platform.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClipMate.Platform.Services;

/// <summary>
/// Service for executing macro clips by sending keystrokes to the target application.
/// Uses CsWin32-generated Windows SendInput API for reliable keystroke simulation.
/// </summary>
public class MacroExecutionService : IMacroExecutionService
{
    private readonly IWin32InputInterop _inputInterop;
    private readonly ILogger<MacroExecutionService> _logger;

    public MacroExecutionService(IWin32InputInterop inputInterop,
        ILogger<MacroExecutionService>? logger = null)
    {
        _inputInterop = inputInterop ?? throw new ArgumentNullException(nameof(inputInterop));
        _logger = logger ?? NullLogger<MacroExecutionService>.Instance;
    }

    /// <inheritdoc />
    public async Task<bool> ExecuteMacroAsync(string macroText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(macroText))
            return true;

        try
        {
            _logger.LogInformation("Executing macro: {MacroLength} characters", macroText.Length);

            // Parse and execute macro
            var tokens = ParseMacroTokens(macroText);

            foreach (var item in tokens)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Macro execution cancelled");
                    return false;
                }

                await ExecuteTokenAsync(item, cancellationToken);
            }

            _logger.LogInformation("Macro execution completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing macro");
            return false;
        }
    }

    /// <inheritdoc />
    public bool IsMacroSafe(string macroText)
    {
        if (string.IsNullOrEmpty(macroText))
            return true;

        // Check for multiple Alt+F4 (window close command)
        var altF4Count = CountOccurrences(macroText, "@{F4}");
        if (altF4Count <= 2)
            return true;

        _logger.LogWarning("Macro contains multiple Alt+F4 commands: {Count}", altF4Count);
        return false;

        // Add more dangerous pattern checks as needed
    }

    #region Token Execution

    private async Task ExecuteTokenAsync(MacroToken token, CancellationToken cancellationToken)
    {
        switch (token.Type)
        {
            case TokenType.Character:
                SendUnicodeChar(token.Character);
                break;

            case TokenType.SpecialKey:
                if (token.SpecialKey!.IsPause)
                    await Task.Delay(500, cancellationToken); // Pause for half second
                else if (token.SpecialKey.KeyName == "LITERAL")
                    SendUnicodeChar(token.SpecialKey.Character);
                else
                {
                    for (var i = 0; i < token.SpecialKey.RepeatCount; i++)
                    {
                        SendVirtualKey(token.SpecialKey.VirtualKeyCode);
                        await Task.Delay(50, cancellationToken); // Small delay between repeats
                    }
                }

                break;

            case TokenType.ModifierChar:
                SendModifiedChar(token.Modifier, token.Character);
                break;

            case TokenType.ModifierSpecialKey:
                SendModifiedVirtualKey(token.Modifier, token.SpecialKey!.VirtualKeyCode);
                break;
        }

        // Delay between keystrokes for reliability - 15ms ensures target app can process each keystroke
        await Task.Delay(15, cancellationToken);
    }

    #endregion

    #region Helper Methods

    private int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(pattern, index, StringComparison.OrdinalIgnoreCase)) != -1)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }

    #endregion

    #region Macro Parsing

    private List<MacroToken> ParseMacroTokens(string macroText)
    {
        var tokens = new List<MacroToken>();
        var i = 0;

        // Remove natural line breaks (they should be ignored)
        macroText = macroText.Replace("\r\n", "").Replace("\r", "").Replace("\n", "");

        while (i < macroText.Length)
        {
            // Check for modifier keys
            if (IsModifier(macroText[i]))
            {
                var modifier = macroText[i];
                i++;

                if (i >= macroText.Length)
                    continue;

                if (macroText[i] == '{')
                {
                    // Modifier + special key: ^{TAB}
                    var (specialKey, length) = ParseSpecialKey(macroText, i);
                    if (specialKey == null)
                        continue;

                    tokens.Add(new MacroToken { Type = TokenType.ModifierSpecialKey, Modifier = modifier, SpecialKey = specialKey });
                    i += length;
                }
                else
                {
                    // Modifier + character: ^c
                    tokens.Add(new MacroToken { Type = TokenType.ModifierChar, Modifier = modifier, Character = macroText[i] });
                    i++;
                }
            }
            // Check for special keys
            else if (macroText[i] == '{')
            {
                var (specialKey, length) = ParseSpecialKey(macroText, i);
                if (specialKey != null)
                {
                    tokens.Add(new MacroToken { Type = TokenType.SpecialKey, SpecialKey = specialKey });
                    i += length;
                }
                else
                {
                    // Not a valid special key, treat as literal
                    tokens.Add(new MacroToken { Type = TokenType.Character, Character = macroText[i] });
                    i++;
                }
            }
            // Regular character
            else
            {
                tokens.Add(new MacroToken { Type = TokenType.Character, Character = macroText[i] });
                i++;
            }
        }

        return tokens;
    }

    private static bool IsModifier(char c) => c is '~' or '^' or '@';

    private (SpecialKeyInfo? key, int length) ParseSpecialKey(string text, int startIndex)
    {
        var endIndex = text.IndexOf('}', startIndex);
        if (endIndex == -1)
            return (null, 0);

        var content = text.Substring(startIndex + 1, endIndex - startIndex - 1);
        var parts = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var keyName = parts[0].ToUpperInvariant();
        var repeatCount = parts.Length > 1 && int.TryParse(parts[1], out var count)
            ? count
            : 1;

        var keyInfo = GetSpecialKeyInfo(keyName);
        if (keyInfo != null)
        {
            keyInfo.RepeatCount = repeatCount;
            return (keyInfo, endIndex - startIndex + 1);
        }

        // Check for escaped modifiers: {~}, {^}, {@}
        if (content.Length == 1 && IsModifier(content[0]))
            return (new SpecialKeyInfo { KeyName = "LITERAL", Character = content[0] }, endIndex - startIndex + 1);

        return (null, 0);
    }

    private SpecialKeyInfo? GetSpecialKeyInfo(string keyName)
    {
        return keyName switch
        {
            "TAB" => new SpecialKeyInfo { KeyName = "TAB", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_TAB },
            "ENTER" => new SpecialKeyInfo { KeyName = "ENTER", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_RETURN },
            "ESC" or "ESCAPE" => new SpecialKeyInfo { KeyName = "ESC", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_ESCAPE },
            "BACKSPACE" or "BKSP" or "BS" => new SpecialKeyInfo { KeyName = "BACKSPACE", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_BACK },
            "DELETE" or "DEL" => new SpecialKeyInfo { KeyName = "DELETE", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_DELETE },
            "INSERT" or "INS" => new SpecialKeyInfo { KeyName = "INSERT", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_INSERT },
            "HOME" => new SpecialKeyInfo { KeyName = "HOME", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_HOME },
            "END" => new SpecialKeyInfo { KeyName = "END", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_END },
            "PGUP" => new SpecialKeyInfo { KeyName = "PGUP", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_PRIOR },
            "PGDN" => new SpecialKeyInfo { KeyName = "PGDN", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_NEXT },
            "UP" => new SpecialKeyInfo { KeyName = "UP", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_UP },
            "DOWN" => new SpecialKeyInfo { KeyName = "DOWN", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_DOWN },
            "LEFT" => new SpecialKeyInfo { KeyName = "LEFT", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_LEFT },
            "RIGHT" => new SpecialKeyInfo { KeyName = "RIGHT", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_RIGHT },
            "F1" => new SpecialKeyInfo { KeyName = "F1", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_F1 },
            "F2" => new SpecialKeyInfo { KeyName = "F2", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_F2 },
            "F3" => new SpecialKeyInfo { KeyName = "F3", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_F3 },
            "F4" => new SpecialKeyInfo { KeyName = "F4", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_F4 },
            "F5" => new SpecialKeyInfo { KeyName = "F5", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_F5 },
            "F6" => new SpecialKeyInfo { KeyName = "F6", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_F6 },
            "F7" => new SpecialKeyInfo { KeyName = "F7", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_F7 },
            "F8" => new SpecialKeyInfo { KeyName = "F8", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_F8 },
            "F9" => new SpecialKeyInfo { KeyName = "F9", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_F9 },
            "F10" => new SpecialKeyInfo { KeyName = "F10", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_F10 },
            "F11" => new SpecialKeyInfo { KeyName = "F11", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_F11 },
            "F12" => new SpecialKeyInfo { KeyName = "F12", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_F12 },
            "F13" => new SpecialKeyInfo { KeyName = "F13", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_F13 },
            "F14" => new SpecialKeyInfo { KeyName = "F14", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_F14 },
            "F15" => new SpecialKeyInfo { KeyName = "F15", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_F15 },
            "F16" => new SpecialKeyInfo { KeyName = "F16", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_F16 },
            "PAUSE" => new SpecialKeyInfo { KeyName = "PAUSE", IsPause = true },
            "CAPSLOCK" => new SpecialKeyInfo { KeyName = "CAPSLOCK", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_CAPITAL },
            "NUMLOCK" => new SpecialKeyInfo { KeyName = "NUMLOCK", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_NUMLOCK },
            "SCROLLLOCK" => new SpecialKeyInfo { KeyName = "SCROLLLOCK", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_SCROLL },
            "BREAK" => new SpecialKeyInfo { KeyName = "BREAK", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_CANCEL },
            "CLEAR" => new SpecialKeyInfo { KeyName = "CLEAR", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_CLEAR },
            "HELP" => new SpecialKeyInfo { KeyName = "HELP", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_HELP },
            "PRTSC" => new SpecialKeyInfo { KeyName = "PRTSC", VirtualKeyCode = (ushort)VIRTUAL_KEY.VK_SNAPSHOT },
            var _ => null,
        };
    }

    #endregion

    #region Windows API via CsWin32

    private void SendUnicodeChar(char character)
    {
        unsafe
        {
            var inputs = stackalloc INPUT[2];

            // Key down
            inputs[0] = new INPUT
            {
                type = INPUT_TYPE.INPUT_KEYBOARD,
                Anonymous = new INPUT._Anonymous_e__Union
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = default,
                        wScan = character,
                        dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE,
                        time = 0,
                        dwExtraInfo = 0,
                    },
                },
            };

            // Key up
            inputs[1] = new INPUT
            {
                type = INPUT_TYPE.INPUT_KEYBOARD,
                Anonymous = new INPUT._Anonymous_e__Union
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = default,
                        wScan = character,
                        dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = 0,
                    },
                },
            };

            var result = _inputInterop.SendInput(2, inputs, sizeof(INPUT));
            if (result == 0)
                _logger.LogWarning("SendInput failed for character '{Char}'", character);
        }
    }

    private void SendVirtualKey(ushort virtualKeyCode)
    {
        unsafe
        {
            var inputs = stackalloc INPUT[2];

            // Key down
            inputs[0] = new INPUT
            {
                type = INPUT_TYPE.INPUT_KEYBOARD,
                Anonymous = new INPUT._Anonymous_e__Union
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (VIRTUAL_KEY)virtualKeyCode,
                        wScan = 0,
                        dwFlags = default,
                        time = 0,
                        dwExtraInfo = 0,
                    },
                },
            };

            // Key up
            inputs[1] = new INPUT
            {
                type = INPUT_TYPE.INPUT_KEYBOARD,
                Anonymous = new INPUT._Anonymous_e__Union
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (VIRTUAL_KEY)virtualKeyCode,
                        wScan = 0,
                        dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = 0,
                    },
                },
            };

            var result = _inputInterop.SendInput(2, inputs, sizeof(INPUT));
            if (result == 0)
                _logger.LogWarning("SendInput failed for virtual key {Key}", virtualKeyCode);
        }
    }

    private void SendModifiedChar(char modifier, char character)
    {
        var modifierKey = GetModifierVirtualKey(modifier);
        var charKey = char.ToUpper(character);

        unsafe
        {
            var inputs = stackalloc INPUT[4];

            // Modifier down
            inputs[0] = CreateKeyInput(modifierKey, false);
            // Char down
            inputs[1] = CreateUnicodeInput(charKey, false);
            // Char up
            inputs[2] = CreateUnicodeInput(charKey, true);
            // Modifier up
            inputs[3] = CreateKeyInput(modifierKey, true);

            _inputInterop.SendInput(4, inputs, sizeof(INPUT));
        }
    }

    private void SendModifiedVirtualKey(char modifier, ushort virtualKeyCode)
    {
        var modifierKey = GetModifierVirtualKey(modifier);

        unsafe
        {
            var inputs = stackalloc INPUT[4];

            // Modifier down
            inputs[0] = CreateKeyInput(modifierKey, false);
            // Key down
            inputs[1] = CreateKeyInput(virtualKeyCode, false);
            // Key up
            inputs[2] = CreateKeyInput(virtualKeyCode, true);
            // Modifier up
            inputs[3] = CreateKeyInput(modifierKey, true);

            _inputInterop.SendInput(4, inputs, sizeof(INPUT));
        }
    }

    private ushort GetModifierVirtualKey(char modifier) => modifier switch
    {
        '~' => (ushort)VIRTUAL_KEY.VK_SHIFT,
        '^' => (ushort)VIRTUAL_KEY.VK_CONTROL,
        '@' => (ushort)VIRTUAL_KEY.VK_MENU, // Alt
        var _ => throw new ArgumentException($"Unknown modifier: {modifier}"),
    };

    private INPUT CreateKeyInput(ushort virtualKey, bool isKeyUp)
    {
        return new INPUT
        {
            type = INPUT_TYPE.INPUT_KEYBOARD,
            Anonymous = new INPUT._Anonymous_e__Union
            {
                ki = new KEYBDINPUT
                {
                    wVk = (VIRTUAL_KEY)virtualKey,
                    wScan = 0,
                    dwFlags = isKeyUp
                        ? KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP
                        : default,
                    time = 0,
                    dwExtraInfo = 0,
                },
            },
        };
    }

    private INPUT CreateUnicodeInput(char character, bool isKeyUp)
    {
        return new INPUT
        {
            type = INPUT_TYPE.INPUT_KEYBOARD,
            Anonymous = new INPUT._Anonymous_e__Union
            {
                ki = new KEYBDINPUT
                {
                    wVk = default,
                    wScan = character,
                    dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE | (isKeyUp
                        ? KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP
                        : default),
                    time = 0,
                    dwExtraInfo = 0,
                },
            },
        };
    }

    #endregion

    #region Internal Classes

    private enum TokenType
    {
        Character,
        SpecialKey,
        ModifierChar,
        ModifierSpecialKey,
    }

    private class MacroToken
    {
        public TokenType Type { get; set; }
        public char Character { get; set; }
        public char Modifier { get; set; }
        public SpecialKeyInfo? SpecialKey { get; set; }
    }

    private class SpecialKeyInfo
    {
        public string KeyName { get; set; } = string.Empty;
        public ushort VirtualKeyCode { get; set; }
        public int RepeatCount { get; set; } = 1;
        public bool IsPause { get; set; }
        public char Character { get; set; }
    }

    #endregion
}
