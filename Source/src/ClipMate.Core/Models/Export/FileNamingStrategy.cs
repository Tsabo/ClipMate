namespace ClipMate.Core.Models.Export;

/// <summary>
/// File naming strategy for flat-file export.
/// </summary>
public enum FileNamingStrategy
{
    /// <summary>
    /// Sequential numbering: 00001, 00002, 00003...
    /// </summary>
    Sequential,

    /// <summary>
    /// Uses clip title as filename.
    /// </summary>
    TitleBased,

    /// <summary>
    /// Uses GUID (no dashes).
    /// </summary>
    Serial,

    /// <summary>
    /// Prompts user for each file.
    /// </summary>
    PromptPerFile,
}
