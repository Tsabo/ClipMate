using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using ClipMate.Core.Models.Configuration;

namespace ClipMate.Tests.Unit.Configuration;

/// <summary>
/// A global hotkey takes its keys before any window sees them, so a default hotkey that is also a shortcut in ClipMate's
/// windows silently replaces that shortcut. Ctrl+Alt+A was both Append and Toggle Auto-Capture: trying to Append
/// turned capture off.
/// </summary>
public partial class HotkeyShortcutClashTests
{
    // Default hotkeys whose window shortcut does the same thing, so the hotkey taking it changes nothing
    private static readonly Dictionary<string, string> _sameAction = new()
    {
        [nameof(HotkeyConfiguration.SelectNext)] = "Scroll to Next Item: also Ctrl+Alt+N in the windows",
        [nameof(HotkeyConfiguration.SelectPrevious)] = "Scroll to Previous Item: also Ctrl+Alt+P in the windows",
        [nameof(HotkeyConfiguration.ScreenCapture)] = "the tray menu's Screen Capture - Area item shows its hotkey",
    };

    [Test]
    public async Task DefaultHotkeys_AreNotShortcutsInClipMatesWindows()
    {
        // Arrange: every KeyBinding, KeyGesture and InputGestureText in ClipMate.App's XAML
        var appDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(SourcePath())!, "..", "..", "..", "src", "ClipMate.App"));
        var separator = Path.DirectorySeparatorChar;
        var shortcuts = Directory.EnumerateFiles(appDirectory, "*.xaml", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{separator}obj{separator}") && !p.Contains($"{separator}bin{separator}"))
            .SelectMany(p => Shortcuts(File.ReadAllText(p)).Select(s => (Shortcut: s, File: Path.GetFileName(p))))
            .ToList();

        var defaults = typeof(HotkeyConfiguration).GetProperties()
            .Where(p => p.PropertyType == typeof(string) && !_sameAction.ContainsKey(p.Name))
            .Select(p => (Name: p.Name, Hotkey: (string?)p.GetValue(new HotkeyConfiguration())))
            .Where(p => !string.IsNullOrEmpty(p.Hotkey))
            .ToList();

        // Act
        var clashes = defaults
            .SelectMany(d => shortcuts.Where(s => s.Shortcut == Normalize(d.Hotkey!)).Select(s => $"{d.Name} = {d.Hotkey} is a shortcut in {s.File}"))
            .Distinct()
            .ToList();

        // Assert
        await Assert.That(shortcuts).IsNotEmpty();
        await Assert.That(clashes).IsEmpty();
    }

    private static IEnumerable<(ModifierKeys, Key)> Shortcuts(string xaml)
    {
        foreach (Match item in KeyBindingElement().Matches(xaml))
        {
            var modifiers = item.Groups["modifiers"].Success ? item.Groups["modifiers"].Value + "+" : "";
            yield return Normalize(modifiers + item.Groups["key"].Value);
        }

        foreach (Match item in KeyGestureAttribute().Matches(xaml))
            yield return Normalize(item.Groups["gesture"].Value);

        // A menu item's InputGestureText is free display text, e.g. "(+)"; only text that reads as a shortcut counts
        foreach (Match item in InputGestureTextAttribute().Matches(xaml))
        {
            (ModifierKeys, Key)? shortcut;
            try
            {
                shortcut = Normalize(item.Groups["gesture"].Value);
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                shortcut = null;
            }

            if (shortcut is { } found)
                yield return found;
        }
    }

    // "Shift+Ctrl+Q" → (Control | Shift, Q); the order of the modifiers doesn't matter
    private static (ModifierKeys, Key) Normalize(string shortcut)
    {
        var parts = shortcut.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var modifiers = parts[..^1].Aggregate(ModifierKeys.None, (all, part) => all | part.ToUpperInvariant() switch
        {
            "CTRL" or "CONTROL" => ModifierKeys.Control,
            "ALT" => ModifierKeys.Alt,
            "SHIFT" => ModifierKeys.Shift,
            "WIN" or "WINDOWS" => ModifierKeys.Windows,
            var _ => throw new FormatException($"Unknown modifier '{part}' in '{shortcut}'"),
        });

        // InputGestureText is display text, which abbreviates some keys
        var key = parts[^1].ToUpperInvariant() switch
        {
            "DEL" => nameof(Key.Delete),
            "INS" => nameof(Key.Insert),
            "ESC" => nameof(Key.Escape),
            "PGUP" => nameof(Key.PageUp),
            "PGDN" => nameof(Key.PageDown),
            var _ => parts[^1],
        };

        return (modifiers, Enum.Parse<Key>(key, ignoreCase: true));
    }

    [GeneratedRegex("""<KeyBinding\b(?=[^>]*\bKey="(?<key>[^"]+)")(?:(?=[^>]*\bModifiers="(?<modifiers>[^"]+)"))?[^>]*>""")]
    private static partial Regex KeyBindingElement();

    [GeneratedRegex(@"\bKeyGesture=""(?<gesture>[^""{]+)""")]
    private static partial Regex KeyGestureAttribute();

    [GeneratedRegex(@"\bInputGestureText=""(?<gesture>[^""{]+)""")]
    private static partial Regex InputGestureTextAttribute();

    private static string SourcePath([CallerFilePath] string path = "") => path;
}
