using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipMate.App.ViewModels;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
using DevExpress.Mvvm;
using IMessenger = CommunityToolkit.Mvvm.Messaging.IMessenger;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.Services;

/// <summary>
/// Shows the clip the Scroll Next / Previous hotkeys reached in a popup next to the tray, as ClipMate 7's scroll
/// balloon does, so the user sees which clip is now on the clipboard without opening ClipMate.
/// </summary>
public sealed class HotkeyNavigationPopup : IRecipient<ClipReachedByHotkeyEvent>
{
    private const int _previewLineCount = 3;

    private readonly IClipService _clips;
    private readonly IConfigurationService _configurationService;
    private readonly ClipDescriber _describer;
    private readonly ILogger<HotkeyNavigationPopup> _logger;
    private readonly IMessenger _messenger;
    private readonly IClipPopupNotifications _notifications;
    private int _latestRequest;
    private INotification? _shown;

    public HotkeyNavigationPopup(ClipDescriber describer,
        IClipService clips,
        IConfigurationService configurationService,
        IClipPopupNotifications notifications,
        IMessenger messenger,
        ILogger<HotkeyNavigationPopup> logger)
    {
        _describer = describer ?? throw new ArgumentNullException(nameof(describer));
        _clips = clips ?? throw new ArgumentNullException(nameof(clips));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _messenger.Register(this);
    }

    /// <inheritdoc />
    public void Receive(ClipReachedByHotkeyEvent message) => _ = ShowAsync(message);

    /// <summary>
    /// Shows the clip reached in the popup, in place of the one shown for an earlier press. Completes once the popup
    /// is shown, without waiting for it to close. Called on the UI thread.
    /// </summary>
    public async Task ShowAsync(ClipReachedByHotkeyEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!_configurationService.Configuration.Appearance.ShowClipWhenScrollingWithHotkeys)
        {
            _logger.LogDebug("No hotkey navigation popup: Show clip when scrolling with hotkeys is off");
            return;
        }

        // One popup at a time: a new press replaces the shown popup, and a press whose clip is still loading when the
        // next one comes is never shown at all (the notification service would queue rather than replace)
        var request = ++_latestRequest;
        _shown?.Hide();
        _shown = null;

        try
        {
            var clip = message.Clip;
            var description = await _describer.DescribeAsync(clip, message.DatabaseKey);

            // An Encrypted Clip never reveals its content, even while its key is remembered
            if (!clip.Encrypted)
                await LoadContentAsync(clip, message.DatabaseKey);

            if (request != _latestRequest)
            {
                _logger.LogDebug("No hotkey navigation popup for clip {ClipId}: a later press replaced it", clip.Id);
                return;
            }

            var viewModel = new HotkeyNavigationPopupViewModel
            {
                Description = description,
                PreviewText = clip.Encrypted ? null : PreviewTextOf(clip),
                Thumbnail = clip.Encrypted ? null : ThumbnailOf(clip),
            };

            var notification = _notifications.Create(viewModel);
            _shown = notification;
            _ = OpenClipMateWhenClickedAsync(notification, clip);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show clip {ClipId} reached with the hotkeys", message.Clip.Id);
        }
    }

    /// <summary>
    /// Clicking the popup opens ClipMate with the clip selected, even if the clip list moved on since.
    /// </summary>
    private async Task OpenClipMateWhenClickedAsync(INotification notification, Clip clip)
    {
        try
        {
            var result = await notification.ShowAsync();
            if (ReferenceEquals(_shown, notification))
                _shown = null;

            if (result != NotificationResult.Activated)
                return;

            _messenger.Send(new SelectClipRequestedEvent(clip.Id));
            _messenger.Send(new ShowExplorerWindowEvent());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open ClipMate from the hotkey navigation popup for clip {ClipId}", clip.Id);
        }
    }

    /// <summary>
    /// Clips in the list carry only their metadata; the preview needs the content.
    /// </summary>
    private async Task LoadContentAsync(Clip clip, string? databaseKey)
    {
        if (clip.TextContent != null || clip.ImageData != null || string.IsNullOrEmpty(databaseKey))
            return;

        try
        {
            await _clips.LoadBlobDataAsync(databaseKey, clip);
        }
        catch (Exception ex)
        {
            // Still show the title and collection
            _logger.LogWarning(ex, "Could not load clip {ClipId} for the hotkey navigation popup's preview", clip.Id);
        }
    }

    private ImageSource? ThumbnailOf(Clip clip)
    {
        if (clip.ImageData is not { Length: > 0 } imageData)
            return null;

        try
        {
            // Decode straight to thumbnail size, keeping the aspect ratio
            using var header = new MemoryStream(imageData);
            var size = BitmapFrame.Create(header, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            using var content = new MemoryStream(imageData);
            var thumbnail = new BitmapImage();
            thumbnail.BeginInit();
            thumbnail.StreamSource = content;
            thumbnail.CacheOption = BitmapCacheOption.OnLoad;
            if (size.PixelWidth >= size.PixelHeight)
                thumbnail.DecodePixelWidth = Math.Min(size.PixelWidth, HotkeyNavigationPopupViewModel.ThumbnailSize);
            else
                thumbnail.DecodePixelHeight = Math.Min(size.PixelHeight, HotkeyNavigationPopupViewModel.ThumbnailSize);

            thumbnail.EndInit();
            thumbnail.Freeze();
            return thumbnail;
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or ArgumentException)
        {
            _logger.LogWarning(ex, "Could not make a thumbnail of clip {ClipId} for the hotkey navigation popup", clip.Id);
            return null;
        }
    }

    private static string? PreviewTextOf(Clip clip)
    {
        if (string.IsNullOrEmpty(clip.TextContent))
            return null;

        var lines = clip.TextContent.Split('\n', _previewLineCount + 1).Take(_previewLineCount).Select(p => p.TrimEnd('\r'));
        return string.Join('\n', lines);
    }
}
