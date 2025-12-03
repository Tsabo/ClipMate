using System.Collections.ObjectModel;
using System.Diagnostics;
using ClipMate.App.Models;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the Clip Properties dialog.
/// </summary>
public partial class ClipPropertiesViewModel : ObservableObject
{
    private readonly IClipService _clipService;
    private readonly ICollectionService _collectionService;
    private readonly IFolderService _folderService;

    [ObservableProperty]
    private DateTimeOffset _capturedAt;

    [ObservableProperty]
    private string _clipId = string.Empty;

    [ObservableProperty]
    private string _collectionId = string.Empty;

    [ObservableProperty]
    private string _creator = string.Empty;

    [ObservableProperty]
    private ObservableCollection<DataFormatInfo> _dataFormats = [];

    [ObservableProperty]
    private bool _encrypted;

    [ObservableProperty]
    private string _folderName = string.Empty;

    [ObservableProperty]
    private DateTimeOffset? _lastModified;

    [ObservableProperty]
    private int _locale;

    [ObservableProperty]
    private bool _macro;

    private Clip? _originalClip;

    [ObservableProperty]
    private string? _shortcut;

    [ObservableProperty]
    private int _size;

    [ObservableProperty]
    private int _sortKey;

    [ObservableProperty]
    private string? _sourceUrl;

    [ObservableProperty]
    private string _statusText = "Status: No updates since read from disk.";

    [ObservableProperty]
    private string? _title;

    [ObservableProperty]
    private int? _userId;

    public ClipPropertiesViewModel(IClipService clipService, IFolderService folderService, ICollectionService collectionService)
    {
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
        _folderService = folderService ?? throw new ArgumentNullException(nameof(folderService));
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
    }

    /// <summary>
    /// Loads clip data into the view model.
    /// </summary>
#pragma warning disable IDE0016 // Null-coalescing is more readable than pattern matching in this method
    public async Task LoadClipAsync(Clip clip, CancellationToken cancellationToken = default)
    {
        if (clip == null)
            throw new ArgumentNullException(nameof(clip));

        _originalClip = clip;

        ClipId = clip.Id.ToString();
        CollectionId = clip.CollectionId?.ToString() ?? string.Empty;

        // Load folder name or collection name
        if (clip.FolderId.HasValue)
        {
            var folder = await _folderService.GetByIdAsync(clip.FolderId.Value, cancellationToken);
            FolderName = folder is not null
                ? folder.Name
                : "Unknown";
        }
        else if (clip.CollectionId.HasValue)
        {
            // When no folder is assigned, show the collection name
            var collection = await _collectionService.GetByIdAsync(clip.CollectionId.Value, cancellationToken);
            FolderName = collection is not null
                ? collection.Name
                : "(Unknown Collection)";
        }
        else
            FolderName = "(No Collection)";

        Title = clip.Title;
        SourceUrl = clip.SourceUrl;
        Creator = clip.Creator ?? string.Empty;
        CapturedAt = clip.CapturedAt;
        LastModified = clip.LastModified;
        SortKey = clip.SortKey;
        Locale = clip.Locale;
        Encrypted = clip.Encrypted;
        Macro = clip.Macro;
        Size = clip.Size;
        UserId = clip.UserId;
        Shortcut = string.Empty; // TODO: Load from shortcuts table

        // Load data formats through service layer
        DataFormats.Clear();
        var clipDataFormats = await _clipService.GetClipFormatsAsync(clip.Id, cancellationToken);

        foreach (var item in clipDataFormats.OrderBy(p => p.FormatName))
        {
            var icon = GetIconForFormat(item.FormatName, item.Format);
            DataFormats.Add(new DataFormatInfo
            {
                Icon = icon,
                FormatName = $"{item.FormatName} (Format: {item.Format}, Size: {item.Size} bytes)"
            });
        }
    }
#pragma warning restore IDE0016

    [RelayCommand]
    private async Task OkAsync()
    {
        if (_originalClip == null)
            return;

        // Update the clip with edited values
        _originalClip.Title = Title;
        _originalClip.SourceUrl = SourceUrl;
        _originalClip.SortKey = SortKey;
        _originalClip.Locale = Locale;
        _originalClip.Encrypted = Encrypted;
        _originalClip.Macro = Macro;
        _originalClip.LastModified = DateTimeOffset.Now;

        await _clipService.UpdateAsync(_originalClip);

        StatusText = "Status: Clip updated successfully.";
    }

    [RelayCommand]
    private void Cancel()
    {
        // Dialog will be closed by IsCancel binding
    }

    [RelayCommand]
    private void Help()
    {
        // TODO: Open help documentation
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://clipmate.com/help/clip-properties",
            UseShellExecute = true
        });
    }

    /// <summary>
    /// Gets the appropriate icon for a clipboard format.
    /// </summary>
    private static string GetIconForFormat(string formatName, int formatCode)
    {
        // Text formats
        if (formatCode == Formats.Text.Code || formatCode == Formats.UnicodeText.Code ||
            formatName == Formats.Text.Name || formatName == Formats.UnicodeText.Name)
            return "📄"; // Document

        // RTF format
        if (formatName == "CF_RTF" || formatCode == Formats.RichText.Code)
            return "🅰"; // Letter A (formatted)

        // HTML format
        if (formatName == Formats.Html.Name || formatCode == Formats.Html.Code || formatCode == Formats.HtmlAlt.Code)
            return "🌐"; // Globe (web)

        // Image formats
        if (formatCode == Formats.Bitmap.Code || formatCode == Formats.Dib.Code ||
            formatName == Formats.Bitmap.Name || formatName == Formats.Dib.Name || formatName == Formats.EnhMetafile.Name)
            return "🖼"; // Picture frame

        // File list format
        if (formatCode == Formats.HDrop.Code || formatName == Formats.HDrop.Name)
            return "📁"; // Folder

        // Unknown format
        return "❓";
    }
}
