namespace ClipMate.Core.Models;

/// <summary>
/// Represents clip data prepared for printing.
/// </summary>
public class PrintClipData
{
    public Guid ClipId { get; set; }
    public bool IsImage { get; init; }
    public byte[]? ImageData { get; init; }
    public string? Title { get; init; }
    public string? Creator { get; init; }
    public DateTime Created { get; init; }
    public string? Url { get; init; }
    public string? Content { get; init; }
}
