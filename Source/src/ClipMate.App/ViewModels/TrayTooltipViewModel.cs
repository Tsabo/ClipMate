using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Platform.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace ClipMate.App.ViewModels;

/// <summary>
/// The tray icon's tooltip (ClipMate 7's System Tray Hint): the Current Clip's title, or the current collection.
/// </summary>
public sealed class TrayTooltipViewModel : ObservableObject, IRecipient<CurrentClipChangedEvent>,
    IRecipient<CurrentCollectionChangedEvent>, IRecipient<PreferencesChangedEvent>, IDisposable
{
    /// <summary>
    /// The most characters Windows shows in a tray icon's tooltip.
    /// </summary>
    public const int MaxLength = 127;

    private const string _appName = "ClipMate";
    private const string _ellipsis = "…";

    private readonly ICollectionService _collectionService;
    private readonly IConfigurationService _configurationService;
    private readonly IMessenger _messenger;
    private string? _currentCollection;
    private Clip? _currentClip;

    public TrayTooltipViewModel(IMessenger messenger, IConfigurationService configurationService,
        ICollectionService collectionService)
    {
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));

        _messenger.Register<CurrentClipChangedEvent>(this);
        _messenger.Register<CurrentCollectionChangedEvent>(this);
        _messenger.Register<PreferencesChangedEvent>(this);
    }

    /// <summary>
    /// The tooltip text.
    /// </summary>
    public string Text
    {
        get;
        private set => SetProperty(ref field, value);
    } = _appName;

    /// <summary>
    /// Starts from the active collection (where captures go), since ClipMate may start in the tray, before any
    /// collection is picked in the tree.
    /// </summary>
    public async Task InitializeAsync()
    {
        Collection active;
        try
        {
            active = await _collectionService.GetActiveAsync();
        }
        catch (InvalidOperationException)
        {
            // No active collection yet: the first one picked in the tree names it
            return;
        }

        UiThread.Run(() =>
        {
            // A collection picked meanwhile is newer
            if (_currentCollection != null)
                return;

            _currentCollection = ClipDescription.CollectionOf(active.Title);
            Refresh();
        });
    }

    /// <summary>
    /// Another clip became the Current Clip: captured, put on the clipboard, or armed by Power Paste.
    /// </summary>
    public void Receive(CurrentClipChangedEvent message) =>
        UiThread.Run(() =>
        {
            _currentClip = message.CurrentClip.Clip;
            Refresh();
        });

    /// <summary>
    /// Another collection was selected.
    /// </summary>
    public void Receive(CurrentCollectionChangedEvent message) =>
        UiThread.Run(() =>
        {
            _currentCollection = message.Name;
            Refresh();
        });

    /// <summary>
    /// The System Tray Hint option may have changed.
    /// </summary>
    public void Receive(PreferencesChangedEvent message) => UiThread.Run(Refresh);

    public void Dispose()
    {
        _messenger.UnregisterAll(this);
    }

    private void Refresh()
    {
        var detail = _configurationService.Configuration.Appearance.SystemTrayHint == SystemTrayHint.Collection
            ? _currentCollection
            : _currentClip == null ? null : ClipDescription.TitleOf(_currentClip);

        var text = string.IsNullOrEmpty(detail) ? _appName : detail;
        Text = text.Length <= MaxLength ? text : Shorten(text);
    }

    /// <summary>
    /// Cuts <paramref name="text" /> to <see cref="MaxLength" /> with an ellipsis, never between the two halves of a
    /// character outside the Basic Multilingual Plane, such as an emoji.
    /// </summary>
    private static string Shorten(string text)
    {
        var length = MaxLength - _ellipsis.Length;
        if (char.IsLowSurrogate(text[length]) && char.IsHighSurrogate(text[length - 1]))
            length--;

        return text[..length] + _ellipsis;
    }
}
