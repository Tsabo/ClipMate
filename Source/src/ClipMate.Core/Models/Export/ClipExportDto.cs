namespace ClipMate.Core.Models.Export;

/// <summary>
/// Simplified clip DTO for XML serialization.
/// Contains only serializable properties from Clip.
/// </summary>
public class ClipExportDto
{
    public Guid Id { get; set; }
    public Guid? CollectionId { get; set; }
    public Guid? FolderId { get; set; }
    public string? Title { get; set; }
    public string? Creator { get; set; }
    public DateTime CapturedAt { get; set; }
    public int SortKey { get; set; }
    public string? SourceUrl { get; set; }
    public bool CustomTitle { get; set; }
    public int Locale { get; set; }
    public bool WrapCheck { get; set; }
    public bool Encrypted { get; set; }
    public int Icons { get; set; }
    public int Size { get; set; }
    public int Checksum { get; set; }
    public int ViewTab { get; set; }
    public bool Macro { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string? SourceApplicationName { get; set; }
    public string? SourceApplicationTitle { get; set; }
    public int PasteCount { get; set; }
    public bool IsFavorite { get; set; }
    public string? Label { get; set; }
    public ClipType Type { get; set; }
    public string? TextContent { get; set; }
    public string? RtfContent { get; set; }
    public string? HtmlContent { get; set; }

    public static ClipExportDto FromClip(Clip clip)
    {
        return new ClipExportDto
        {
            Id = clip.Id,
            CollectionId = clip.CollectionId,
            FolderId = clip.FolderId,
            Title = clip.Title,
            Creator = clip.Creator,
            CapturedAt = clip.CapturedAt.UtcDateTime,
            SortKey = clip.SortKey,
            SourceUrl = clip.SourceUrl,
            CustomTitle = clip.CustomTitle,
            Locale = clip.Locale,
            WrapCheck = clip.WrapCheck,
            Encrypted = clip.Encrypted,
            Icons = clip.Icons,
            Size = clip.Size,
            Checksum = clip.Checksum,
            ViewTab = clip.ViewTab,
            Macro = clip.Macro,
            ContentHash = clip.ContentHash,
            SourceApplicationName = clip.SourceApplicationName,
            SourceApplicationTitle = clip.SourceApplicationTitle,
            PasteCount = clip.PasteCount,
            IsFavorite = clip.IsFavorite,
            Label = clip.Label,
            Type = clip.Type,
            TextContent = clip.TextContent,
            RtfContent = clip.RtfContent,
            HtmlContent = clip.HtmlContent,
        };
    }

    public Clip ToClip()
    {
        return new Clip
        {
            Id = Id,
            CollectionId = CollectionId,
            FolderId = FolderId,
            Title = Title,
            Creator = Creator,
            CapturedAt = new DateTimeOffset(CapturedAt, TimeSpan.Zero),
            SortKey = SortKey,
            SourceUrl = SourceUrl,
            CustomTitle = CustomTitle,
            Locale = Locale,
            WrapCheck = WrapCheck,
            Encrypted = Encrypted,
            Icons = Icons,
            Size = Size,
            Checksum = Checksum,
            ViewTab = ViewTab,
            Macro = Macro,
            ContentHash = ContentHash,
            SourceApplicationName = SourceApplicationName,
            SourceApplicationTitle = SourceApplicationTitle,
            PasteCount = PasteCount,
            IsFavorite = IsFavorite,
            Label = Label,
            Type = Type,
            TextContent = TextContent,
            RtfContent = RtfContent,
            HtmlContent = HtmlContent,
        };
    }
}
