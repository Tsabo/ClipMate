using System.Windows.Media;

namespace ClipMate.App.ViewModels;

/// <summary>
/// What the hotkey navigation popup shows of the clip the Scroll Next / Previous hotkeys reached.
/// </summary>
public sealed class HotkeyNavigationPopupViewModel
{
    /// <summary>
    /// The most device-independent pixels a thumbnail is wide or high.
    /// </summary>
    public const int ThumbnailSize = 96;

    /// <summary>
    /// Gets the clip's title, or "Encrypted clip" for an Encrypted Clip.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the collection the clip is in; <c>&lt;collection&gt; on &lt;Peer&gt;</c> for a Remote Database.
    /// </summary>
    public required string Collection { get; init; }

    /// <summary>
    /// Gets the first lines of the clip's text, or <c>null</c> when it has none to show.
    /// </summary>
    public string? PreviewText { get; init; }

    /// <summary>
    /// Gets a thumbnail of an image clip, or <c>null</c>.
    /// </summary>
    public ImageSource? Thumbnail { get; init; }
}
