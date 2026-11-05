using System.Text;

namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// The characters ticked in one group of Step 1 of the Text Cleanup Options dialog.
/// </summary>
public class TextCleanupCharacters
{
    public bool GreaterThan { get; set; }

    public bool LessThan { get; set; }

    public bool PoundSign { get; set; }

    public bool EqualsSign { get; set; }

    public bool Pipe { get; set; }

    public bool Spaces { get; set; }

    public bool Tabs { get; set; }

    public bool Other { get; set; }

    public string OtherText { get; set; } = string.Empty;

    /// <summary>
    /// Gets the ticked characters as one string.
    /// </summary>
    public string ToCharacters()
    {
        var characters = new StringBuilder();
        if (GreaterThan)
            characters.Append('>');

        if (LessThan)
            characters.Append('<');

        if (PoundSign)
            characters.Append('#');

        if (EqualsSign)
            characters.Append('=');

        if (Pipe)
            characters.Append('|');

        if (Spaces)
            characters.Append(' ');

        if (Tabs)
            characters.Append('\t');

        if (Other)
            characters.Append(OtherText);

        return characters.ToString();
    }

    public TextCleanupCharacters Clone() => (TextCleanupCharacters)MemberwiseClone();
}
