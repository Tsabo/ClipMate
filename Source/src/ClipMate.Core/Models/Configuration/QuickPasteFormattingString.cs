namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Represents a QuickPaste formatting string configuration that defines how keystrokes
/// are sent to the target application during paste operations.
/// </summary>
public class QuickPasteFormattingString
{
    /// <summary>
    /// Gets or sets the title/name of this formatting string.
    /// Displayed in the formatting string selector dropdown.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the preamble text sent before pasting the clip.
    /// Can contain literal text, meta characters (^, ~, @), special keys ({TAB}, {ENTER}),
    /// and macros (#DATE#, #TITLE#, etc.).
    /// </summary>
    public string Preamble { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the keystrokes used to invoke the paste operation.
    /// Examples: ^v (Ctrl+V), ~{INSERT} (Shift+Insert), @e#PAUSE#p (Alt+E, pause, P).
    /// </summary>
    public string PasteKeystrokes { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the postamble text sent after pasting the clip.
    /// Can contain literal text, meta characters, special keys, and macros.
    /// </summary>
    public string Postamble { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title trigger for automatic format selection.
    /// When the target window title contains this string, this format is automatically selected.
    /// Use "*" to match all applications (default format).
    /// Leave empty for manual selection only.
    /// </summary>
    public string TitleTrigger { get; set; } = string.Empty;

    /// <summary>
    /// Gets the full formatting string in registry format.
    /// Format: [Title],[Preamble],[PasteKeystrokes],[Postamble],[TitleTrigger]
    /// </summary>
    public string ToRegistryFormat() => $"[{Title}],[{Preamble}],[{PasteKeystrokes}],[{Postamble}],[{TitleTrigger}]";

    /// <summary>
    /// Parses a formatting string from registry format.
    /// Format: [Title],[Preamble],[PasteKeystrokes],[Postamble],[TitleTrigger]
    /// </summary>
    /// <param name="registryFormat">The registry format string.</param>
    /// <returns>A new QuickPasteFormattingString instance.</returns>
    public static QuickPasteFormattingString FromRegistryFormat(string registryFormat)
    {
        var parts = registryFormat.Split(["],["], StringSplitOptions.None);
        if (parts.Length != 5)
            return new QuickPasteFormattingString();

        return new QuickPasteFormattingString
        {
            Title = parts[0].TrimStart('['),
            Preamble = parts[1],
            PasteKeystrokes = parts[2],
            Postamble = parts[3],
            TitleTrigger = parts[4].TrimEnd(']'),
        };
    }
}
