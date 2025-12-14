namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Specifies the location for temporary database files.
/// </summary>
public enum TempFileLocation
{
    /// <summary>
    /// Store temp files in the database directory.
    /// </summary>
    DatabaseDirectory = 0,

    /// <summary>
    /// Store temp files in the system TMP directory.
    /// </summary>
    SystemTmp = 1,

    /// <summary>
    /// Store temp files in the program directory.
    /// </summary>
    ProgramDirectory = 2,

    /// <summary>
    /// Store temp files in ClipMate's temp directory.
    /// </summary>
    ClipMateTemp = 3,
}
