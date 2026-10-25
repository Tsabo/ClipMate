using ClipMate.Core.Models.Export;
using ClipMate.Core.Models.Import;

namespace ClipMate.Core.Services;

/// <summary>
/// Imports clips, collections and shortcuts from an original ClipMate 7.x database (Elevatesoft DBISAM format).
/// </summary>
public interface IClipMate7ImportService
{
    /// <summary>
    /// Returns true if the folder looks like a ClipMate 7 database (contains clip.dat and coll.dat).
    /// </summary>
    bool IsClipMate7Database(string folder);

    /// <summary>
    /// Reads the source database and reports what would be imported.
    /// </summary>
    /// <exception cref="InvalidOperationException">The folder is not a readable ClipMate 7 database.</exception>
    Task<ClipMate7ImportPreview> AnalyzeAsync(string sourceFolder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the encrypted clips that one of the ClipMate 7 keys can decrypt, so the user can tell when every key
    /// has been entered.
    /// </summary>
    /// <exception cref="InvalidOperationException">The folder is not a readable ClipMate 7 database.</exception>
    Task<int> CountDecryptableClipsAsync(string sourceFolder, IReadOnlyList<string> encryptionKeys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports the source database into a new or existing ClipMate database.
    /// The import runs in a single transaction: on failure or cancellation nothing is written,
    /// and a newly created target database file is removed.
    /// Clips and collections keep their ClipMate 7 GUIDs; clips already present in the target are skipped.
    /// </summary>
    Task<ClipMate7ImportResult> ImportAsync(ClipMate7ImportOptions options,
        Action<ExportProgressMessage>? progressCallback = null,
        CancellationToken cancellationToken = default);
}
