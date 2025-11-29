namespace ClipMate.Core.Models.Configuration;

/// <summary>
/// Defines which view to show initially when ClipMate starts.
/// </summary>
public enum InitialShowMode
{
    /// <summary>
    /// Start minimized to system tray (no window shown).
    /// </summary>
    Nothing,

    /// <summary>
    /// Show the Classic view window.
    /// </summary>
    Classic,

    /// <summary>
    /// Show the Explorer view window.
    /// </summary>
    Explorer
}

/// <summary>
/// Defines what happens when clicking a collection icon.
/// </summary>
public enum CollectionIconClickBehavior
{
    /// <summary>
    /// Show the collection selection menu.
    /// </summary>
    MenuAppears,

    /// <summary>
    /// Cycle to the next collection.
    /// </summary>
    NextCollectionSelected
}

/// <summary>
/// Defines the layout mode for the Explorer view.
/// </summary>
public enum ExplorerLayoutMode
{
    /// <summary>
    /// Editor takes full width, tree on the side.
    /// </summary>
    FullWidthEditor,

    /// <summary>
    /// Collection tree takes full height, editor below.
    /// </summary>
    FullHeightCollectionTree
}

/// <summary>
/// Defines the default editor view type.
/// </summary>
public enum EditorViewType
{
    /// <summary>
    /// Plain text view.
    /// </summary>
    Text,

    /// <summary>
    /// Unicode text view (same as Text in Monaco).
    /// </summary>
    Unicode,

    /// <summary>
    /// Rich Text Format view.
    /// </summary>
    Rtf,

    /// <summary>
    /// Bitmap image view.
    /// </summary>
    Bitmap,

    /// <summary>
    /// Picture/image view.
    /// </summary>
    Picture,

    /// <summary>
    /// HTML view.
    /// </summary>
    Html,

    /// <summary>
    /// Binary/hex view.
    /// </summary>
    Binary
}
