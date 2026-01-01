namespace ClipMate.Core.Events;

/// <summary>
/// Text case conversion types.
/// </summary>
public enum CaseConversionType
{
    /// <summary>Convert to UPPERCASE.</summary>
    Upper,
    /// <summary>Convert to lowercase.</summary>
    Lower,
    /// <summary>Convert To Title Case.</summary>
    Title,
    /// <summary>Convert to Sentence case.</summary>
    Sentence,
    /// <summary>tOGGLE cASE.</summary>
    Toggle
}

/// <summary>
/// Request to convert the case of text in the selected clip.
/// </summary>
/// <param name="ConversionType">The type of case conversion to apply.</param>
public record CaseConversionRequestedEvent(CaseConversionType ConversionType);
