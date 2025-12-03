namespace ClipMate.Core.Models.Configuration;

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