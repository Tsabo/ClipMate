namespace ClipMate.Core.Models;

/// <summary>
/// Represents both collections and folders in ClipMate 7.5 architecture.
/// Matches ClipMate 7.5 COLL table structure exactly.
/// The LmType field determines if this is a collection (0), virtual collection (1), or folder (2).
/// </summary>
public class Collection
{
    /// <summary>
    /// Unique identifier (COLL_GUID in ClipMate 7.5).
    /// </summary>
    public Guid Id { get; set; }

    // ==================== Hierarchy ====================

    /// <summary>
    /// Parent collection/folder ID for hierarchical structure (PARENT_ID in ClipMate 7.5).
    /// Null for root-level collections.
    /// </summary>
    public Guid? ParentId { get; set; }

    /// <summary>
    /// Parent collection GUID (PARENT_GUID in ClipMate 7.5).
    /// Denormalized for performance.
    /// </summary>
    public Guid? ParentGuid { get; set; }

    // ==================== ClipMate 7.5 Fields - EXACT MATCH ====================

    /// <summary>
    /// Display name (TITLE in ClipMate 7.5, 60 chars max).
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// List mode type (LMTYPE in ClipMate 7.5).
    /// - 0 = Normal collection/folder
    /// - 1 = Virtual collection (SQL-based)
    /// - 2 = Folder within a collection
    /// </summary>
    public int LmType { get; set; }

    /// <summary>
    /// List type (LISTTYPE in ClipMate 7.5).
    /// - 0 = Normal list
    /// - 1 = Smart/Virtual collection
    /// - 3 = SQL-based collection
    /// </summary>
    public int ListType { get; set; }

    /// <summary>
    /// Display order (SORTKEY in ClipMate 7.5).
    /// Lower values appear first in tree view.
    /// </summary>
    public int SortKey { get; set; }

    /// <summary>
    /// Icon list index (ILINDEX in ClipMate 7.5).
    /// Index into image list for tree view icon.
    /// </summary>
    public int IlIndex { get; set; }

    /// <summary>
    /// Maximum clips to retain (RETENTIONLIMIT in ClipMate 7.5).
    /// 0 = unlimited, otherwise auto-delete oldest clips when limit reached.
    /// </summary>
    public int RetentionLimit { get; set; }

    /// <summary>
    /// Where new clips go (NEWCLIPSGO in ClipMate 7.5).
    /// 1 = New clips come to this collection
    /// 0 = New clips go elsewhere
    /// </summary>
    public int NewClipsGo { get; set; }

    /// <summary>
    /// Whether to accept new clips (ACCEPTNEWCLIPS in ClipMate 7.5).
    /// </summary>
    public bool AcceptNewClips { get; set; }

    /// <summary>
    /// Read-only flag (READONLY in ClipMate 7.5).
    /// Prevents editing or deleting clips in this collection.
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// Allow duplicate clips (ACCEPTDUPLICATES in ClipMate 7.5).
    /// If false, duplicate content will not be stored.
    /// </summary>
    public bool AcceptDuplicates { get; set; }

    /// <summary>
    /// Default sort column (SORTCOLUMN in ClipMate 7.5).
    /// - -2 = Sort by date
    /// - -3 = Custom sort order
    /// - Positive values = Column index
    /// </summary>
    public int SortColumn { get; set; }

    /// <summary>
    /// Sort direction (SORTASCENDING in ClipMate 7.5).
    /// True = ascending, False = descending.
    /// </summary>
    public bool SortAscending { get; set; }

    /// <summary>
    /// Encryption flag (ENCRYPTED in ClipMate 7.5).
    /// Whether this collection's content is encrypted.
    /// </summary>
    public bool Encrypted { get; set; }

    /// <summary>
    /// Favorite flag (FAVORITE in ClipMate 7.5).
    /// Marks collection as favorite for quick access.
    /// </summary>
    public bool Favorite { get; set; }

    /// <summary>
    /// Last user who modified this collection (LASTUSER_ID in ClipMate 7.5).
    /// </summary>
    public int? LastUserId { get; set; }

    /// <summary>
    /// Last modification timestamp (LAST_UPDATE_TIME in ClipMate 7.5).
    /// </summary>
    public DateTime? LastUpdateTime { get; set; }

    /// <summary>
    /// Cached clip count for performance (LAST_KNOWN_COUNT in ClipMate 7.5).
    /// Avoids expensive COUNT queries.
    /// </summary>
    public int? LastKnownCount { get; set; }

    /// <summary>
    /// SQL query for virtual/smart collections (SQL in ClipMate 7.5, 256 chars max).
    /// Only used when ListType = 1 or 3.
    /// Example: "Select clip.* from clip where Clip.TimeStamp >= '#DATE#' and del = false order by ID"
    /// </summary>
    public string? Sql { get; set; }

    // ==================== Compatibility Properties ====================
    // These provide compatibility with existing code that expects the old property names

    /// <summary>
    /// Compatibility property for Title (maps to Title field).
    /// </summary>
    public string Name
    {
        get => Title;
        set => Title = value;
    }

    /// <summary>
    /// Optional description of the collection's purpose.
    /// Not in ClipMate 7.5, but maintained for backward compatibility.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Sort order for displaying collections in the tree view.
    /// Maps to SortKey for backward compatibility.
    /// </summary>
    public int SortOrder
    {
        get => SortKey;
        set => SortKey = value;
    }

    /// <summary>
    /// Timestamp when the collection was created.
    /// Not in ClipMate 7.5, but maintained for backward compatibility.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp of last modification.
    /// Maps to LastUpdateTime for backward compatibility.
    /// </summary>
    public DateTime? ModifiedAt
    {
        get => LastUpdateTime;
        set => LastUpdateTime = value;
    }

    // ==================== Navigation ====================

    /// <summary>
    /// Parent collection (for hierarchy).
    /// </summary>
    public Collection? Parent { get; set; }

    /// <summary>
    /// Child collections/folders.
    /// </summary>
    public ICollection<Collection> Children { get; set; } = new List<Collection>();

    // ==================== Helper Properties ====================

    /// <summary>
    /// Whether this is a folder (LmType == 2).
    /// </summary>
    public bool IsFolder => LmType == 2;

    /// <summary>
    /// Whether this is a virtual/smart collection.
    /// </summary>
    public bool IsVirtual => LmType == 1 || ListType == 1 || ListType == 3;

    /// <summary>
    /// Whether this collection is active (receives new clips).
    /// </summary>
    public bool IsActive
    {
        get => NewClipsGo == 1;
        set => NewClipsGo = value ? 1 : 0;
    }

    /// <summary>
    /// Whether this is a special collection (Trash Can, Search Results).
    /// </summary>
    public bool IsSpecial => Title.Contains("Trash", StringComparison.OrdinalIgnoreCase) ||
                            Title.Contains("Search Results", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this collection is read-only (rejects new clips, shown in red).
    /// </summary>
    public bool IsReadOnly => ReadOnly || !AcceptNewClips;

    /// <summary>
    /// Icon/emoji for this collection (optional).
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Virtual collection SQL query (maps to Sql for clarity).
    /// </summary>
    public string? VirtualCollectionQuery
    {
        get => Sql;
        set => Sql = value;
    }

    /// <summary>
    /// Purge policy for this collection.
    /// </summary>
    public PurgePolicy PurgePolicy
    {
        get
        {
            if (RetentionLimit == 0)
            {
                return PurgePolicy.Never;
            }
            return PurgePolicy.KeepLast;
        }
    }
}

/// <summary>
/// Purge policy for collections.
/// </summary>
public enum PurgePolicy
{
    /// <summary>
    /// Never purge clips (safe collection).
    /// </summary>
    Never,

    /// <summary>
    /// Keep only the last N clips (based on RetentionLimit).
    /// </summary>
    KeepLast,

    /// <summary>
    /// Purge clips older than specified days.
    /// </summary>
    PurgeByAge
}
