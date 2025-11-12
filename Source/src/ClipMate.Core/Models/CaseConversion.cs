namespace ClipMate.Core.Models;

/// <summary>
/// Defines the type of case conversion to apply to text.
/// </summary>
public enum CaseConversion
{
    /// <summary>
    /// Convert all characters to uppercase.
    /// </summary>
    Uppercase,

    /// <summary>
    /// Convert all characters to lowercase.
    /// </summary>
    Lowercase,

    /// <summary>
    /// Convert the first character of each word to uppercase.
    /// </summary>
    TitleCase,

    /// <summary>
    /// Convert the first character of each sentence to uppercase.
    /// </summary>
    SentenceCase
}
