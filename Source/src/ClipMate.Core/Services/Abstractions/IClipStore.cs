using ClipMate.Core.Models;

namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// The one place a clip is created in a Database: it gives the clip its content hash and checksum, and applies the
/// Duplicate rule when asked (ADR-0005).
/// </summary>
public interface IClipStore
{
    /// <summary>
    /// Adds <paramref name="clip" /> to a collection and folder of a Database.
    /// </summary>
    /// <param name="databaseKey">The Database.</param>
    /// <param name="clip">The clip, with its content loaded; a copy without loaded content keeps the hash it was given.</param>
    /// <param name="collectionId">The collection it goes into.</param>
    /// <param name="folderId">The folder it goes into, if any.</param>
    /// <param name="rejectDuplicate">
    /// Applies the Duplicate rule: when the collection doesn't accept duplicates, a clip whose plain text is the same as
    /// the newest clip's there isn't added. Capture asks for it; other ways of making a clip don't.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see cref="ClipAdded" />, or <see cref="DuplicateFound" /> with the clip already there.</returns>
    Task<AddResult> AddAsync(string databaseKey, Clip clip, Guid? collectionId, Guid? folderId, bool rejectDuplicate,
        CancellationToken cancellationToken = default);
}
