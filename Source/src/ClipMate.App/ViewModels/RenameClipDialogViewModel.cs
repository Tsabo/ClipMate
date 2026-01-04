using System.Diagnostics;
using ClipMate.Core.Events;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the Rename Clip dialog.
/// </summary>
public partial class RenameClipDialogViewModel : ObservableObject
{
    private static string? _lastPrefix; // Static to persist between dialog invocations
    private readonly IMessenger _messenger;
    private readonly IShortcutService _shortcutService;

    [ObservableProperty]
    private bool _carryOverLastPrefix;

    private Guid _clipId;
    private string? _databaseKey;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private string? _shortCut;

    [ObservableProperty]
    private string? _title;

    public RenameClipDialogViewModel(IShortcutService shortcutService, IMessenger messenger)
    {
        _shortcutService = shortcutService ?? throw new ArgumentNullException(nameof(shortcutService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
    }

    public bool CanSave => ShortCut is not { Length: > 64 };

    /// <summary>
    /// Initializes the dialog with the clip's current data.
    /// </summary>
    /// <param name="clipId">The clip ID.</param>
    /// <param name="databaseKey">The database key.</param>
    /// <param name="currentTitle">The current title.</param>
    /// <param name="currentShortcut">The current shortcut (if any).</param>
    public Task InitializeAsync(Guid clipId, string databaseKey, string? currentTitle, string? currentShortcut)
    {
        _clipId = clipId;
        _databaseKey = databaseKey;
        Title = currentTitle;
        ShortCut = currentShortcut;

        Debug.WriteLine($"[RenameClipDialogViewModel.InitializeAsync] Initialized with ClipId={clipId}, DatabaseKey='{databaseKey}', Title='{currentTitle}', Shortcut='{currentShortcut}'");

        // Apply "Carry Over Last Prefix" logic if enabled and there's a last prefix
        if (CarryOverLastPrefix && !string.IsNullOrEmpty(_lastPrefix) && string.IsNullOrEmpty(currentShortcut))
            ShortCut = _lastPrefix;

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task OkAsync()
    {
        if (string.IsNullOrEmpty(_databaseKey))
            return;

        // Extract and save the prefix if CarryOverLastPrefix is enabled
        if (CarryOverLastPrefix && !string.IsNullOrEmpty(ShortCut))
        {
            var lastDotIndex = ShortCut.LastIndexOf('.');
            _lastPrefix = lastDotIndex > 0
                ? ShortCut[..(lastDotIndex + 1)]
                : null; // Include the dot
        }

        // Update or delete the shortcut (service handles missing table internally)
        Debug.WriteLine($"[RenameClipDialogViewModel.OkAsync] Calling UpdateClipShortcutAsync with ClipId={_clipId}, DatabaseKey='{_databaseKey}', Shortcut='{ShortCut}', Title='{Title}'");
        await _shortcutService.UpdateClipShortcutAsync(_databaseKey, _clipId, ShortCut, Title);

        // Send message to notify all listeners that the clip was updated
        _messenger.Send(new ClipUpdatedMessage(_clipId, Title));
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

    partial void OnShortCutChanged(string? value)
    {
        // Validate max length (64 characters)
        if (value is { Length: > 64 })
            ShortCut = value[..64];
    }
}
