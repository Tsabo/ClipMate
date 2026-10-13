namespace ClipMate.Core.Models.Import;

/// <summary>
/// Summary of a ClipMate 7.x (DBISAM) database, produced before importing.
/// </summary>
public sealed class ClipMate7ImportPreview
{
    public int ClipCount { get; init; }

    public int CollectionCount { get; init; }

    public int FormatCount { get; init; }

    public int ShortcutCount { get; init; }

    /// <summary>
    /// Clips encrypted by ClipMate 7. These cannot be decrypted and are skipped.
    /// </summary>
    public int EncryptedClipCount { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Options for importing a ClipMate 7.x database.
/// </summary>
public sealed class ClipMate7ImportOptions
{
    /// <summary>
    /// Folder containing the ClipMate 7 database files (clip.dat, coll.dat, ...).
    /// </summary>
    public required string SourceFolder { get; init; }

    /// <summary>
    /// Full path of the target ClipMate SQLite database.
    /// </summary>
    public required string TargetDatabasePath { get; init; }

    /// <summary>
    /// When true, the target database is created (with default collections) before importing.
    /// The file must not already exist. When false, clips are merged into the existing database.
    /// </summary>
    public bool CreateNewDatabase { get; init; }
}

/// <summary>
/// Outcome of a ClipMate 7.x database import.
/// </summary>
public sealed class ClipMate7ImportResult
{
    public int ClipsImported { get; set; }

    public int CollectionsImported { get; set; }

    public int ShortcutsImported { get; set; }

    /// <summary>
    /// Clips skipped because a clip with the same GUID already exists in the target database.
    /// </summary>
    public int DuplicateClipsSkipped { get; set; }

    /// <summary>
    /// Clips skipped because ClipMate 7 encrypted them.
    /// </summary>
    public int EncryptedClipsSkipped { get; set; }

    /// <summary>
    /// Clips skipped because none of their formats could be converted, or their data was unreadable.
    /// </summary>
    public int UnreadableClipsSkipped { get; set; }

    /// <summary>
    /// Virtual (SQL-based) collections that were not imported because their queries use DBISAM SQL.
    /// </summary>
    public int VirtualCollectionsSkipped { get; set; }

    /// <summary>
    /// Individual clip formats that were dropped because ClipMate cannot store them.
    /// </summary>
    public int UnsupportedFormatsSkipped { get; set; }

    public List<string> Warnings { get; } = [];
}
