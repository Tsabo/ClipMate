using CoreModifierKeys = ClipMate.Core.Models.ModifierKeys;

namespace ClipMate.App.Helpers;

/// <summary>
/// Utility class for parsing and validating hotkey strings.
/// </summary>
public static class HotkeyParser
{
    private static readonly HashSet<string> ForbiddenCombinations =
    [
        "Ctrl+C", "Ctrl+V", "Ctrl+X", "Shift+Ins", "Shift+Insert",
        "Ctrl+A", "Ctrl+Z", "Ctrl+Y", // Common editing shortcuts
    ];

    /// <summary>
    /// Tries to parse a hotkey string into modifier keys and virtual key code.
    /// </summary>
    /// <param name="hotkeyString">The hotkey string (e.g., "Ctrl+Alt+C").</param>
    /// <param name="modifiers">The parsed modifier keys.</param>
    /// <param name="virtualKey">The parsed virtual key code.</param>
    /// <param name="errorMessage">Error message if parsing fails.</param>
    /// <returns>True if parsing succeeds, false otherwise.</returns>
    public static bool TryParse(string hotkeyString, out CoreModifierKeys modifiers, out int virtualKey, out string? errorMessage)
    {
        modifiers = CoreModifierKeys.None;
        virtualKey = 0;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(hotkeyString))
        {
            errorMessage = "Hotkey cannot be empty";
            return false;
        }

        var parts = hotkeyString.Split('+').Select(p => p.Trim()).ToArray();

        if (parts.Length == 0)
        {
            errorMessage = "Invalid hotkey format";
            return false;
        }

        // Last part should be the key, everything else should be modifiers
        var keyPart = parts[^1];
        var modifierParts = parts[..^1];

        // Parse modifiers
        foreach (var modifier in modifierParts)
        {
            switch (modifier.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= CoreModifierKeys.Control;
                    break;
                case "alt":
                    modifiers |= CoreModifierKeys.Alt;
                    break;
                case "shift":
                    modifiers |= CoreModifierKeys.Shift;
                    break;
                case "win":
                case "windows":
                    modifiers |= CoreModifierKeys.Windows;
                    break;
                default:
                    errorMessage = $"Invalid modifier: {modifier}";
                    return false;
            }
        }

        // Must have at least one modifier
        if (modifiers == CoreModifierKeys.None)
        {
            errorMessage = "Must include at least one modifier key (Ctrl, Alt, Shift, or Win)";
            return false;
        }

        // Parse the key
        if (!TryParseKey(keyPart, out virtualKey))
        {
            errorMessage = $"Invalid key: {keyPart}";
            return false;
        }

        // Check for forbidden combinations
        if (IsForbiddenCombination(hotkeyString))
        {
            errorMessage = $"Reserved system combination: {hotkeyString}";
            return false;
        }

        // Check for Shift+Letter combinations (these make typing impossible)
        if (modifiers == CoreModifierKeys.Shift && IsLetter(keyPart))
        {
            errorMessage = "Shift+Letter combinations interfere with normal typing";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Converts modifier keys and virtual key code into a hotkey string.
    /// </summary>
    /// <param name="modifiers">The modifier keys.</param>
    /// <param name="virtualKey">The virtual key code.</param>
    /// <returns>The hotkey string (e.g., "Ctrl+Alt+C").</returns>
    public static string ToString(CoreModifierKeys modifiers, int virtualKey)
    {
        var parts = new List<string>();

        if ((modifiers & CoreModifierKeys.Control) != 0)
            parts.Add("Ctrl");

        if ((modifiers & CoreModifierKeys.Alt) != 0)
            parts.Add("Alt");

        if ((modifiers & CoreModifierKeys.Shift) != 0)
            parts.Add("Shift");

        if ((modifiers & CoreModifierKeys.Windows) != 0)
            parts.Add("Win");

        parts.Add(GetKeyName(virtualKey));

        return string.Join("+", parts);
    }

    /// <summary>
    /// Checks if a hotkey string represents a valid combination.
    /// </summary>
    /// <param name="hotkeyString">The hotkey string to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool IsValidCombination(string hotkeyString) => TryParse(hotkeyString, out var _, out var _, out var _);

    private static bool TryParseKey(string keyString, out int virtualKey)
    {
        virtualKey = 0;

        // Handle single letters (A-Z, a-z)
        if (keyString.Length == 1)
        {
            var ch = char.ToUpperInvariant(keyString[0]);
            if (ch is >= 'A' and <= 'Z')
            {
                virtualKey = ch;
                return true;
            }

            if (ch is >= '0' and <= '9')
            {
                virtualKey = ch;
                return true;
            }
        }

        // Handle function keys (F1-F12)
        if (keyString.StartsWith("F", StringComparison.OrdinalIgnoreCase) && keyString.Length > 1)
        {
            if (int.TryParse(keyString[1..], out var fNum) && fNum is >= 1 and <= 12)
            {
                virtualKey = 0x70 + (fNum - 1); // VK_F1 = 0x70
                return true;
            }
        }

        // Handle special keys
        virtualKey = keyString.ToLowerInvariant() switch
        {
            "insert" or "ins" => 0x2D, // VK_INSERT
            "delete" or "del" => 0x2E, // VK_DELETE
            "home" => 0x24, // VK_HOME
            "end" => 0x23, // VK_END
            "pgup" or "pageup" => 0x21, // VK_PRIOR
            "pgdn" or "pagedown" => 0x22, // VK_NEXT
            "up" => 0x26, // VK_UP
            "down" => 0x28, // VK_DOWN
            "left" => 0x25, // VK_LEFT
            "right" => 0x27, // VK_RIGHT
            var _ => 0,
        };

        return virtualKey != 0;
    }

    private static string GetKeyName(int virtualKey)
    {
        // Letters and numbers
        if (virtualKey is >= 'A' and <= 'Z')
            return ((char)virtualKey).ToString();

        if (virtualKey is >= '0' and <= '9')
            return ((char)virtualKey).ToString();

        // Function keys
        if (virtualKey is >= 0x70 and <= 0x7B)
            return $"F{virtualKey - 0x70 + 1}";

        // Special keys
        return virtualKey switch
        {
            0x2D => "Insert",
            0x2E => "Delete",
            0x24 => "Home",
            0x23 => "End",
            0x21 => "PgUp",
            0x22 => "PgDn",
            0x26 => "Up",
            0x28 => "Down",
            0x25 => "Left",
            0x27 => "Right",
            var _ => $"Key{virtualKey}",
        };
    }

    private static bool IsForbiddenCombination(string hotkeyString) => ForbiddenCombinations.Contains(hotkeyString, StringComparer.OrdinalIgnoreCase);

    private static bool IsLetter(string keyString) => keyString.Length == 1 && char.IsLetter(keyString[0]);
}
