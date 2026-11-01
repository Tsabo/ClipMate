using System.Windows.Media;
using Image = Emoji.Wpf.Image;

namespace ClipMate.App.Models;

/// <summary>
/// One entry of the search dialog's "Search in" control: "All Databases", a Database, or a collection or folder in one.
/// </summary>
/// <param name="Target">What choosing it searches.</param>
/// <param name="Name">The Database, collection or folder name, as the collection tree shows it.</param>
/// <param name="Depth">How deep it is in the tree: 0 for a Database, 1 for its collections, 2 and more for folders.</param>
/// <param name="IsAvailable">Whether it can be searched right now; an unavailable Database (offline Peer, missing file) is shown grayed out.</param>
public sealed record SearchInOption(SearchTarget Target, string Name, int Depth, bool IsAvailable)
{
    // Each icon drawn once and shared, outside the record, so drawing it never changes how an entry compares: the
    // combo finds the chosen entry by value. Emoji.Wpf's drawing isn't thread-safe, hence the lock.
    private static readonly Dictionary<string, ImageSource> _iconImages = [];
    private static readonly Lock _iconImagesLock = new();

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
    public required string Icon { get; init; }

    /// <summary>
    /// Gets <see cref="Icon" /> as an image, drawn as the collection tree draws it, once.
    /// </summary>
    public ImageSource IconImage
    {
        get
        {
            lock (_iconImagesLock)
            {
                if (!_iconImages.TryGetValue(Icon, out var image))
                    _iconImages[Icon] = image = DrawIcon(Icon);

                return image;
            }
        }
    }

    /// <summary>
    /// The entry for one of <paramref name="parent" />'s collections or folders, with its path under the parent's.
    /// </summary>
    public static SearchInOption Under(SearchInOption parent, SearchTarget target, string name, string icon)
    {
        ArgumentNullException.ThrowIfNull(parent);
        return new SearchInOption(target, name, parent.Depth + 1, parent.IsAvailable) { Path = TreePath.Join(parent.Path, name), Icon = icon };
    }

    /// <summary>
    /// Gets whether choosing this entry searches <paramref name="target" />.
    /// </summary>
    public bool Searches(SearchTarget? target) => Target.IsSameChoiceAs(target);

    private static DrawingImage DrawIcon(string icon)
    {
        var image = new DrawingImage();
        Image.SetSource(image, icon);
        return image;
    }
}
