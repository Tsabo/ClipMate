namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// The choices in the Text Cleanup Options dialog, kept from one use to the next. The defaults are ClipMate 7's
/// <c>[CleanUp]</c> settings.
/// </summary>
public class TextCleanupConfiguration
{
    public TextCleanupCharacters Leading { get; set; } = new() { GreaterThan = true, Spaces = true, Tabs = true };

    public TextCleanupCharacters Trailing { get; set; } = new();

    public TextCleanupCharacters Anywhere { get; set; } = new();

    public TextCleanupLineBreaks LineBreaks { get; set; } = TextCleanupLineBreaks.NoChange;

    public bool ReBreak { get; set; }

    public int ReBreakColumn { get; set; } = 72;

    public bool ReQuote { get; set; }

    public string ReQuoteText { get; set; } = ">";

    public TextCleanupCase Case { get; set; } = TextCleanupCase.NoChange;

    public string FindText { get; set; } = string.Empty;

    public string ReplaceText { get; set; } = string.Empty;

    public bool MatchCase { get; set; }

    public bool RegExFind { get; set; }

    public bool RegExReplace { get; set; }

    /// <summary>
    /// Runs a regular expression on each line. Only a regular expression run on the whole text can match a line break.
    /// </summary>
    public bool RegExLineByLine { get; set; } = true;

    public TextCleanupConfiguration Clone()
    {
        var clone = (TextCleanupConfiguration)MemberwiseClone();
        clone.Leading = Leading.Clone();
        clone.Trailing = Trailing.Clone();
        clone.Anywhere = Anywhere.Clone();
        return clone;
    }
}
