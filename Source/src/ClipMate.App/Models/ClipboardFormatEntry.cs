namespace ClipMate.App.Models;

/// <summary>
/// Represents a clipboard format entry for display.
/// </summary>
public sealed class ClipboardFormatEntry
{
    /// <summary>
    /// Gets or sets the format ID.
    /// </summary>
    public uint FormatId { get; init; }

    /// <summary>
    /// Gets or sets the format name.
    /// </summary>
    public string FormatName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the data size in bytes.
    /// </summary>
    public long? DataSize { get; init; }

    /// <summary>
    /// Gets the display text for the size.
    /// </summary>
    public string SizeDisplay => DataSize.HasValue
        ? FormatSize(DataSize.Value)
        : "N/A";

    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        var order = 0;
        var size = (double)bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }
}
