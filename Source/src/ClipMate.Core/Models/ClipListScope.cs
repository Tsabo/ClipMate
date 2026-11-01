namespace ClipMate.Core.Models;

/// <summary>
/// Which clips of a Database a clip list shows: a collection's (evaluated with its own SQL for a Smart or SQL-based
/// collection), a folder's, or the Trash's.
/// </summary>
public abstract record ClipListScope
{
    private ClipListScope()
    {
    }

    /// <summary>
    /// The clips of a collection; with <paramref name="IncludeFolders" />, also those of all its folders.
    /// </summary>
    /// <param name="CollectionId">The collection.</param>
    /// <param name="IncludeFolders">Whether the clips of the collection's folders, at any depth, are included.</param>
    public sealed record Collection(Guid CollectionId, bool IncludeFolders = false) : ClipListScope;

    /// <summary>
    /// The clips of a folder.
    /// </summary>
    /// <param name="CollectionId">The collection the folder is in.</param>
    /// <param name="FolderId">The folder.</param>
    public sealed record Folder(Guid CollectionId, Guid FolderId) : ClipListScope;

    /// <summary>
    /// The deleted clips waiting in the Trash, most recently deleted first.
    /// </summary>
    public sealed record Trash : ClipListScope;
}
