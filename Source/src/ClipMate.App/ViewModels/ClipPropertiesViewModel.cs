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
    private readonly IShortcutService _shortcutService;

    [ObservableProperty]
    private DateTimeOffset _capturedAt;

    [ObservableProperty]
    private string _clipId = string.Empty;

    [ObservableProperty]
    private string _collectionId = string.Empty;

    [ObservableProperty]
    private string _collectionName = string.Empty;

    [ObservableProperty]
    private string _creator = string.Empty;

    private string? _databaseKey;

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

    public ClipPropertiesViewModel(IClipService clipService, IFolderService folderService, ICollectionService collectionService, IShortcutService shortcutService)
    {
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
        _folderService = folderService ?? throw new ArgumentNullException(nameof(folderService));
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _shortcutService = shortcutService ?? throw new ArgumentNullException(nameof(shortcutService));
    }

    /// <summary>
    /// Loads a clip's data into the properties dialog.
    /// </summary>
    /// <param name="clip">The clip to load.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task LoadClipAsync(Clip clip, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clip);

        _originalClip = clip;

        // Get database key from active collection
        _databaseKey = _collectionService.GetActiveDatabaseKey();
        if (string.IsNullOrEmpty(_databaseKey))
            throw new InvalidOperationException("No active database key found");

        ClipId = clip.Id.ToString();
        CollectionId = clip.CollectionId?.ToString() ?? string.Empty;

        // Load collection and folder names
        if (clip.CollectionId.HasValue)
        {
            var collection = await _collectionService.GetByIdAsync(clip.CollectionId.Value, cancellationToken);
            if (collection != null)
                CollectionName = collection.Title;
            else
                CollectionName = "(Unknown Collection)";
        }
        else
            CollectionName = "(No Collection)";

        if (clip.FolderId.HasValue)
        {
            var folder = await _folderService.GetByIdAsync(clip.FolderId.Value, cancellationToken);
            FolderName = folder?.Name ?? "(Unknown Folder)";
        }
        else
            FolderName = "(No Folder)";

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

        // Load shortcut from database
        var existingShortcut = await _shortcutService.GetByClipIdAsync(_databaseKey, clip.Id, cancellationToken);
        Shortcut = existingShortcut?.Nickname ?? string.Empty;

        // Load data formats through service layer
        DataFormats.Clear();
        var clipDataFormats = await _clipService.GetClipFormatsAsync(_databaseKey, clip.Id, cancellationToken);

        foreach (var item in clipDataFormats.OrderBy(p => p.FormatName))
        {
            var icon = GetIconForFormat(item.FormatName, item.Format);
            DataFormats.Add(new DataFormatInfo
            {
                Icon = icon,
                FormatName = $"{item.FormatName} (Format: {item.Format}, Size: {item.Size} bytes)",
            });
        }
    }

    [RelayCommand]
    private async Task OkAsync()
    {
        if (_originalClip == null || string.IsNullOrEmpty(_databaseKey))
            return;

        // Validate shortcut uniqueness if it has changed and is not empty
        if (!string.IsNullOrWhiteSpace(Shortcut))
        {
            var existingShortcut = await _shortcutService.GetByClipIdAsync(_databaseKey, _originalClip.Id);
            var shortcutChanged = existingShortcut?.Nickname != Shortcut;

            if (shortcutChanged)
            {
                // Check if this shortcut is already used by another clip
                var allShortcuts = await _shortcutService.GetAllAsync(_databaseKey);
                var conflictingShortcut = allShortcuts.FirstOrDefault(s =>
                    s.Nickname.Equals(Shortcut, StringComparison.OrdinalIgnoreCase) &&
                    s.ClipId != _originalClip.Id);

                if (conflictingShortcut != null)
                {
                    StatusText = $"Error: Shortcut '{Shortcut}' is already used by another clip.";
                    return;
                }
            }
        }

        // Update the clip with edited values
        _originalClip.Title = Title;
        _originalClip.SourceUrl = SourceUrl;
        _originalClip.SortKey = SortKey;
        _originalClip.Locale = Locale;
        _originalClip.Encrypted = Encrypted;
        _originalClip.Macro = Macro;
        _originalClip.LastModified = DateTimeOffset.Now;

        await _clipService.UpdateAsync(_databaseKey, _originalClip);

        // Update or delete the shortcut (null/empty shortcut deletes it)
        await _shortcutService.UpdateClipShortcutAsync(_databaseKey, _originalClip.Id, Shortcut, Title);

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
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://jeremy.browns.info/ClipMate/user-interface/cliplist/",
            UseShellExecute = true,
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
