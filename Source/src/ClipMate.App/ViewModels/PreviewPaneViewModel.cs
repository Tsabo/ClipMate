using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using ClipMate.Core.Models;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the preview pane (right pane).
/// Displays detailed content preview based on clip type (text, HTML, image, etc.).
/// </summary>
public partial class PreviewPaneViewModel : ObservableObject
{
    [ObservableProperty]
    private Clip? _selectedClip;

    [ObservableProperty]
    private string _previewText = string.Empty;

    [ObservableProperty]
    private string _previewHtml = string.Empty;

    [ObservableProperty]
    private ImageSource? _previewImageSource;

    [ObservableProperty]
    private bool _hasTextPreview = false;

    [ObservableProperty]
    private bool _hasHtmlPreview = false;

    [ObservableProperty]
    private bool _hasImagePreview = false;

    /// <summary>
    /// Sets the clip to preview and updates the preview content.
    /// </summary>
    /// <param name="clip">The clip to preview, or null to clear.</param>
    public void SetClip(Clip? clip)
    {
        SelectedClip = clip;
        
        if (clip == null)
        {
            Clear();
            return;
        }

        // Reset all preview flags
        HasTextPreview = false;
        HasHtmlPreview = false;
        HasImagePreview = false;
        PreviewText = string.Empty;
        PreviewHtml = string.Empty;
        PreviewImageSource = null;

        // Set preview based on clip type
        switch (clip.Type)
        {
            case ClipType.Text:
            case ClipType.RichText:
                PreviewText = clip.TextContent ?? string.Empty;
                HasTextPreview = true;
                break;

            case ClipType.Html:
                PreviewHtml = clip.HtmlContent ?? string.Empty;
                HasHtmlPreview = true;
                break;

            case ClipType.Image:
                if (clip.ImageData != null && clip.ImageData.Length > 0)
                {
                    HasImagePreview = true;
                    PreviewImageSource = LoadImageFromBytes(clip.ImageData);
                }
                break;

            case ClipType.Files:
                // For files, show the file paths as text
                PreviewText = clip.TextContent ?? string.Empty;
                HasTextPreview = true;
                break;
        }
    }

    /// <summary>
    /// Clears all preview data.
    /// </summary>
    public void Clear()
    {
        SelectedClip = null;
        PreviewText = string.Empty;
        PreviewHtml = string.Empty;
        PreviewImageSource = null;
        HasTextPreview = false;
        HasHtmlPreview = false;
        HasImagePreview = false;
    }

    /// <summary>
    /// Loads an image from byte array.
    /// </summary>
    private static ImageSource? LoadImageFromBytes(byte[] imageData)
    {
        try
        {
            var image = new BitmapImage();
            using var mem = new MemoryStream(imageData);
            mem.Position = 0;
            image.BeginInit();
            image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = mem;
            image.EndInit();
            image.Freeze(); // Important for cross-thread access
            return image;
        }
        catch
        {
            // If image loading fails, return null
            return null;
        }
    }
}
