using ClipMate.Core.Models;

namespace ClipMate.Core.Services.Abstractions;

/// <summary>
/// The read path for a clip's formats and content, for any Database: a local Database is read from its file and a
/// Remote Database (a <see cref="Models.Networking.RemoteDatabaseKey" /> routing key) from its Peer. The viewer, the
/// clipboard and decryption use this instead of reading repositories, so they work the same for remote clips.
/// </summary>
public interface IClipContentService
{
    /// <summary>
    /// Reads a clip with all its formats and stored content. Encrypted content is returned as stored (ciphertext).
    /// </summary>
    /// <param name="databaseKey">The database key of a local Database, or the routing key of a Remote Database.</param>
    /// <param name="clipId">The clip.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The clip and its content, or <c>null</c> if the Database has no such clip.</returns>
    /// <exception cref="RemoteDatabaseUnavailableException">A Remote Database cannot be read right now.</exception>
    Task<ClipContent?> GetClipContentAsync(string databaseKey, Guid clipId, CancellationToken cancellationToken = default);
}
