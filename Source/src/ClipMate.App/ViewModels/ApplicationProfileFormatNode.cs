using CommunityToolkit.Mvvm.ComponentModel;

namespace ClipMate.App.ViewModels;

/// <summary>
/// Represents a clipboard format node under an application profile in the tree view.
/// Allows enabling/disabling specific clipboard formats for capture.
/// </summary>
public partial class ApplicationProfileFormatNode : TreeNodeBase
{
    [ObservableProperty]
    private bool _enabled;

    private readonly string _formatName;

    /// <summary>
    /// Creates a new format node.
    /// </summary>
    /// <param name="formatName">The clipboard format name (e.g., "CF_UNICODETEXT", "HTML Format").</param>
    /// <param name="enabled">Whether this format is enabled for capture.</param>
    public ApplicationProfileFormatNode(string formatName, bool enabled)
    {
        _formatName = formatName ?? throw new ArgumentNullException(nameof(formatName));
        _enabled = enabled;
    }

    /// <summary>
    /// The clipboard format name.
    /// </summary>
    public string FormatName => _formatName;

    /// <summary>
    /// Display name with description.
    /// </summary>
    public override string Name => $"{_formatName} - {GetFormatDescription(_formatName)}";

    /// <summary>
    /// Format-specific icon.
    /// </summary>
    public override string Icon => GetFormatIcon(_formatName);

    /// <summary>
    /// Node type for format.
    /// </summary>
    public override TreeNodeType NodeType => TreeNodeType.ApplicationProfileFormat;

    /// <summary>
    /// Gets an appropriate icon for the clipboard format.
    /// </summary>
    public static string GetFormatIcon(string formatName)
    {
        return formatName?.ToUpperInvariant() switch
        {
            "CF_TEXT" or "TEXT" or "CF_UNICODETEXT" => "📝", // Text emoji
            "CF_DIB" or "CF_BITMAP" or "BITMAP" => "🖼️", // Picture frame emoji
            "CF_HDROP" or "HDROP" => "📁", // Folder emoji for file drops
            "HTML FORMAT" or "CF_HTML" => "🌐", // Globe emoji for HTML
            "RICH TEXT FORMAT" or "CF_RTF" or "RTF" => "📄", // Document emoji
            _ => "📋" // Default clipboard emoji
        };
    }

    /// <summary>
    /// Gets a human-readable description for the clipboard format.
    /// </summary>
    public static string GetFormatDescription(string formatName)
    {
        return formatName?.ToUpperInvariant() switch
        {
            "CF_TEXT" or "TEXT" => "Plain Text (ANSI)",
            "CF_UNICODETEXT" => "Plain Text (Unicode)",
            "CF_DIB" or "CF_BITMAP" or "BITMAP" => "Bitmap Image",
            "CF_HDROP" or "HDROP" => "File Drop List",
            "HTML FORMAT" or "CF_HTML" => "HTML Markup",
            "RICH TEXT FORMAT" or "CF_RTF" or "RTF" => "Rich Text Format",
            "CF_WAVE" => "Audio Waveform",
            "CF_ENHMETAFILE" => "Enhanced Metafile",
            "CF_METAFILEPICT" => "Windows Metafile",
            "DATAOBJECT" => "Serialized Data Object",
            "OLEOBJECT" => "OLE Object",
            "OLEPRIVATEDATA" => "OLE Private Data",
            "CF_LOCALE" or "LOCALE" => "Locale Information",
            _ => "Custom Format"
        };
    }
}
