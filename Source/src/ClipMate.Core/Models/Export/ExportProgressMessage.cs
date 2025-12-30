namespace ClipMate.Core.Models.Export;

/// <summary>
/// Progress message for export/import operations.
/// </summary>
public class ExportProgressMessage
{
    public bool IsComplete { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool Successful { get; set; }
    public int ProcessedCount { get; set; }
    public int TotalCount { get; set; }
}
