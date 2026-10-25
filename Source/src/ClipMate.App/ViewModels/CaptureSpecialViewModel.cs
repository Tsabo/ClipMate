using System.Collections.ObjectModel;
using ClipMate.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClipMate.App.ViewModels;

/// <summary>
/// The Capture Special dialog: the Formats on the clipboard, to choose which ones to capture. As in ClipMate 7, text,
/// image, Rich Text and HTML start ticked.
/// </summary>
public sealed class CaptureSpecialViewModel
{
    private static readonly HashSet<string> _tickedAtFirst = new(StringComparer.OrdinalIgnoreCase)
    {
        "CF_UNICODETEXT", "CF_TEXT", "CF_DIB", "CF_DIBV5", "PNG", "Rich Text Format", "HTML Format",
    };

    public CaptureSpecialViewModel(ClipboardReading reading)
    {
        Reading = reading ?? throw new ArgumentNullException(nameof(reading));
        foreach (var item in reading.Formats)
            Formats.Add(new CaptureSpecialFormat(item.Name, item.Data.Length) { IsSelected = _tickedAtFirst.Contains(item.Name) });
    }

    public ClipboardReading Reading { get; }

    public ObservableCollection<CaptureSpecialFormat> Formats { get; } = [];

    /// <summary>
    /// The names of the ticked Formats.
    /// </summary>
    public IReadOnlyList<string> SelectedFormatNames => Formats.Where(p => p.IsSelected).Select(p => p.Name).ToList();
}

/// <summary>
/// One Format in the Capture Special dialog.
/// </summary>
public sealed partial class CaptureSpecialFormat(string name, int size) : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public string Name { get; } = name;

    public int Size { get; } = size;
}
