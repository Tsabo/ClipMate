namespace ClipMate.Core.Models;

/// <summary>
/// Defines the scope for search operations.
/// </summary>
public enum SearchScope
{
    /// <summary>
    /// Search within the current collection only.
    /// </summary>
    CurrentCollection = 0,

    /// <summary>
    /// Search across all collections.
    /// </summary>
    AllCollections = 1,

    /// <summary>
    /// Search within a specific folder.
    /// </summary>
    SpecificFolder = 2
}
