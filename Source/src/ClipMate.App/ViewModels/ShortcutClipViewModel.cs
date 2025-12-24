using ClipMate.Core.Models;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel wrapper for a clip displayed in shortcut mode.
/// Provides display properties without modifying the underlying Clip object.
/// </summary>
public class ShortcutClipViewModel
{
    private readonly Guid _shortcutId;

    public ShortcutClipViewModel(Clip clip, string nickname, string databaseKey, Guid shortcutId)
    {
        Clip = clip;
        DisplayTitle = nickname;
        DatabaseKey = databaseKey;
        _shortcutId = shortcutId;
    }

    // Expose clip properties for grid binding
    public Guid Id => Clip.Id;
    public string IconGlyph => Clip.IconGlyph;
    public string DisplayTitle { get; }

    public DateTimeOffset CapturedAt => Clip.CapturedAt;
    public string SizeDisplay => Clip.SizeDisplay;
    public string SourceDisplay => Clip.SourceDisplay;
    public bool Encrypted => Clip.Encrypted;
    public string SourceUrl => $"<{Clip.Id}><{_shortcutId}><0>";

    // Expose the underlying clip for operations that need it
    public Clip Clip { get; }

    public string DatabaseKey { get; }
}
