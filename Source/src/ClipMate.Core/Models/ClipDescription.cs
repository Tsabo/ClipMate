namespace ClipMate.Core.Models;

/// <summary>
/// How ClipMate names a clip and its collection outside the clip list, such as in the tray tooltip and the hotkey
/// navigation popup.
/// </summary>
/// <param name="Title">The clip's title (see <see cref="TitleOf" />).</param>
/// <param name="Collection">The collection the clip is in (see <see cref="CollectionOf" />).</param>
public sealed record ClipDescription(string Title, string Collection)
{
    /// <summary>
    /// What an Encrypted Clip is called in place of its title.
    /// </summary>
    public const string EncryptedTitle = "Encrypted clip";

    /// <summary>
    /// The clip's title, or <see cref="EncryptedTitle" /> for an Encrypted Clip, even while its key is remembered.
    /// </summary>
    public static string TitleOf(Clip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        return clip.Encrypted ? EncryptedTitle : clip.DisplayTitle;
    }

    /// <summary>
    /// The collection's name, followed by "on &lt;Peer&gt;" for a collection of a Remote Database.
    /// </summary>
    /// <param name="collectionName">The collection or folder name.</param>
    /// <param name="peerName">The Peer Alias (or Device Name) of the Peer sharing it, or <c>null</c> for a local collection.</param>
    public static string CollectionOf(string collectionName, string? peerName = null)
    {
        ArgumentNullException.ThrowIfNull(collectionName);
        return string.IsNullOrWhiteSpace(peerName) ? collectionName : $"{collectionName} on {peerName}";
    }
}
