namespace ClipMate.Core.Models;

/// <summary>
/// Defines the type of folder and its special behavior.
/// </summary>
public enum FolderType
{
    /// <summary>
    /// Normal user-created folder. Accepts clips via clipboard capture and manual insertion.
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Default inbox folder where new clipboard captures are stored.
    /// Accepts clips via clipboard capture and manual insertion.
    /// </summary>
    Inbox = 1,

    /// <summary>
    /// Safe folder for important clips that should be preserved.
    /// Accepts clips via manual insertion only.
    /// </summary>
    Safe = 2,

    /// <summary>
    /// Overflow folder for clips that exceed the Inbox capacity.
    /// Accepts clips automatically when Inbox is full.
    /// </summary>
    Overflow = 3,

    /// <summary>
    /// Samples folder for template/example clips.
    /// Accepts clips via manual insertion only.
    /// </summary>
    Samples = 4,

    /// <summary>
    /// Virtual folder for linked/referenced clips (not stored locally).
    /// May have special display/behavior rules.
    /// </summary>
    Virtual = 5,

    /// <summary>
    /// Trash can folder for deleted clips.
    /// Accepts clips when user deletes them. Can be emptied to permanently delete.
    /// </summary>
    TrashCan = 6,

    /// <summary>
    /// Search results folder - dynamically populated by search queries.
    /// Read-only, cannot accept manual clip insertion.
    /// </summary>
    SearchResults = 7
}
