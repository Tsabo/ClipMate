using System.Windows.Media;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using Image = Emoji.Wpf.Image;

namespace ClipMate.App.Models;

/// <summary>
/// What the search dialog searches: a local Database or a Remote Database, or a collection or folder in it (searched
/// with the "current collection" and "specific folder" scopes).
/// </summary>
/// <param name="DatabaseKey">The Database's key; a <see cref="RemoteDatabaseKey" /> for a Remote Database.</param>
/// <param name="DatabaseName">The Database's name as the collection tree shows it.</param>
public sealed record SearchTarget(string DatabaseKey, string DatabaseName)
{
    /// <summary>
    /// Every available Database, local and remote, searched at once; each one's matches go to its own Search Results
    /// node. Its <see cref="DatabaseKey" /> names no Database.
    /// </summary>
    public static SearchTarget AllDatabases { get; } = new(string.Empty, "All Databases") { IsAllDatabases = true };

    /// <summary>
    /// The collection tree has no node for "All Databases", so it has an icon of its own.
    /// </summary>
    public const string AllDatabasesIcon = "🗃️";

    /// <summary>
    /// Gets whether the target is <see cref="AllDatabases" />.
    /// </summary>
    public bool IsAllDatabases { get; private init; }

    /// <summary>
    /// The selected collection, or <c>null</c> when none is selected or it cannot be searched by membership (a Smart or
    /// SQL-based collection, the Trashcan, search results).
    /// </summary>
    public Guid? CollectionId { get; init; }

    /// <summary>
    /// The selected collection's name.
    /// </summary>
    public string? CollectionName { get; init; }

    /// <summary>
    /// The selected folder, or <c>null</c> when no folder is selected.
    /// </summary>
    public Guid? FolderId { get; init; }

    /// <summary>
    /// The selected folder's name.
    /// </summary>
    public string? FolderName { get; init; }

    /// <summary>
    /// Gets whether the target is a Remote Database: searched by its Peer, with no SQL. Set by whoever makes the
    /// target from a Remote Database's node.
    /// </summary>
    public bool IsRemote { get; init; }
}

/// <summary>
/// One entry of the search dialog's "Search in" control: a Database, or a collection or folder in one.
/// </summary>
/// <param name="Target">What choosing it searches.</param>
/// <param name="Name">The Database, collection or folder name, as the collection tree shows it.</param>
/// <param name="Depth">How deep it is in the tree: 0 for a Database, 1 for its collections, 2 and more for folders.</param>
/// <param name="IsAvailable">Whether it can be searched right now; an unavailable Database (offline Peer, missing file) is shown grayed out.</param>
public sealed record SearchInOption(SearchTarget Target, string Name, int Depth, bool IsAvailable)
{
    /// <summary>
    /// Gets where it is, for the closed control: e.g. "My Clips › Inbox › Projects".
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Gets how far the entry is indented in the open list, in pixels, so it reads like the collection tree.
    /// </summary>
    public double Indent => Depth * 16;

    /// <summary>
    /// Gets the collection tree's icon for the entry (an emoji).
    /// </summary>
    public required string Icon
    {
        get;
        init
        {
            field = value;
            _iconImage = null;
        }
    }

    // Drawn when first shown, on the UI thread; a copy with another icon draws its own
    private ImageSource? _iconImage;

    /// <summary>
    /// Gets <see cref="Icon" /> as an image, drawn as the collection tree draws it, once.
    /// </summary>
    public ImageSource IconImage => _iconImage ??= DrawIcon(Icon);

    private static DrawingImage DrawIcon(string icon)
    {
        var image = new DrawingImage();
        Image.SetSource(image, icon);
        return image;
    }

    /// <summary>
    /// Gets whether choosing this entry searches <paramref name="target" />: the same Database and the same folder, or with
    /// no folder the same collection. IDs alone never match, as Databases share collection IDs.
    /// </summary>
    public bool Searches(SearchTarget? target) =>
        target != null && target.DatabaseKey == Target.DatabaseKey && target.FolderId == Target.FolderId &&
        (target.FolderId != null || target.CollectionId == Target.CollectionId);
}
