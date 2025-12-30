namespace ClipMate.Core.Models.Export;

/// <summary>
/// Simplified collection DTO for XML serialization.
/// Contains only serializable properties from Collection.
/// </summary>
public class CollectionExportDto
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public CollectionLmType LmType { get; set; }
    public CollectionListType ListType { get; set; }
    public int SortKey { get; set; }
    public int IlIndex { get; set; }
    public int RetentionLimit { get; set; }
    public long MaxBytes { get; set; }
    public int MaxAgeDays { get; set; }
    public CollectionRole Role { get; set; }
    public int NewClipsGo { get; set; }
    public bool AcceptNewClips { get; set; }
    public bool ReadOnly { get; set; }
    public bool AcceptDuplicates { get; set; }
    public int SortColumn { get; set; }
    public bool SortAscending { get; set; }
    public bool Encrypted { get; set; }
    public bool Favorite { get; set; }
    public string? Description { get; set; }
    public string? Sql { get; set; }
    public string? Icon { get; set; }

    public static CollectionExportDto FromCollection(Collection coll)
    {
        return new CollectionExportDto
        {
            Id = coll.Id,
            ParentId = coll.ParentId,
            Title = coll.Title,
            LmType = coll.LmType,
            ListType = coll.ListType,
            SortKey = coll.SortKey,
            IlIndex = coll.IlIndex,
            RetentionLimit = coll.RetentionLimit,
            MaxBytes = coll.MaxBytes,
            MaxAgeDays = coll.MaxAgeDays,
            Role = coll.Role,
            NewClipsGo = coll.NewClipsGo,
            AcceptNewClips = coll.AcceptNewClips,
            ReadOnly = coll.ReadOnly,
            AcceptDuplicates = coll.AcceptDuplicates,
            SortColumn = coll.SortColumn,
            SortAscending = coll.SortAscending,
            Encrypted = coll.Encrypted,
            Favorite = coll.Favorite,
            Description = coll.Description,
            Sql = coll.Sql,
            Icon = coll.Icon,
        };
    }

    public Collection ToCollection()
    {
        return new Collection
        {
            Id = Id,
            ParentId = ParentId,
            Title = Title,
            LmType = LmType,
            ListType = ListType,
            SortKey = SortKey,
            IlIndex = IlIndex,
            RetentionLimit = RetentionLimit,
            MaxBytes = MaxBytes,
            MaxAgeDays = MaxAgeDays,
            Role = Role,
            NewClipsGo = NewClipsGo,
            AcceptNewClips = AcceptNewClips,
            ReadOnly = ReadOnly,
            AcceptDuplicates = AcceptDuplicates,
            SortColumn = SortColumn,
            SortAscending = SortAscending,
            Encrypted = Encrypted,
            Favorite = Favorite,
            Description = Description,
            Sql = Sql,
            Icon = Icon,
        };
    }
}
