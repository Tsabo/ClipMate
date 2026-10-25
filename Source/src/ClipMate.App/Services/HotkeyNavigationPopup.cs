using ClipMate.App.ViewModels;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.Messaging;
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
    private readonly IClipPopupNotifications _notifications;

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
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(messenger);

        messenger.Register(this);
    }

    /// <inheritdoc />
    public void Receive(ClipReachedByHotkeyEvent message) => _ = ShowAsync(message);

    /// <summary>
    /// Shows the clip reached in the popup.
    /// </summary>
    public async Task ShowAsync(ClipReachedByHotkeyEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var clip = message.Clip;
            var description = await _describer.DescribeAsync(clip, message.DatabaseKey);

            // An Encrypted Clip never reveals its content, even while its key is remembered
            if (!clip.Encrypted)
                await LoadContentAsync(clip, message.DatabaseKey);

            var viewModel = new HotkeyNavigationPopupViewModel
            {
                Title = description.Title,
                Collection = description.Collection,
                PreviewText = clip.Encrypted ? null : PreviewTextOf(clip),
            };

            _ = _notifications.Create(viewModel).ShowAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show clip {ClipId} reached with the hotkeys", message.Clip.Id);
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

    private static string? PreviewTextOf(Clip clip)
    {
        if (string.IsNullOrEmpty(clip.TextContent))
            return null;

        var lines = clip.TextContent.Split('\n', _previewLineCount + 1).Take(_previewLineCount).Select(p => p.TrimEnd('\r'));
        return string.Join('\n', lines);
    }
}
