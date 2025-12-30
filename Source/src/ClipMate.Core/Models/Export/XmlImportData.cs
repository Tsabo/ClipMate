namespace ClipMate.Core.Models.Export;

/// <summary>
/// XML import result data.
/// </summary>
public class XmlImportData
{
    public DateTime ImportedAt { get; set; }
    public string SourceFile { get; set; } = string.Empty;
    public Guid TargetCollectionId { get; set; }
    public List<Clip> Clips { get; set; } = [];
    public List<Collection> Collections { get; set; } = [];
    public int ClipCount { get; set; }
    public int CollectionCount { get; set; }
}
