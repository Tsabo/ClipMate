using System.Diagnostics;
using System.Net;
using System.Security;
using ClipMate.App.Models.TreeNodes;
using ClipMate.App.ViewModels;
using ClipMate.App.Views.Dialogs;
using ClipMate.Core.Events;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Networking;
using ClipMate.Core.Services;
using ClipMate.Core.Services.Abstractions;
using ClipMate.Core.ValueObjects;
using ClipMate.Data.Services;
using ClipMate.Platform.Helpers;
using CommunityToolkit.Mvvm.Messaging;
using DevExpress.Xpf.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shortcut = ClipMate.Core.Models.Shortcut;

namespace ClipMate.App.Services;

/// <summary>
/// Coordinates clip operations (delete, rename, copy, move, export) across all windows.
/// Centralizes event handling that was previously duplicated in ExplorerWindowViewModel and ClassicViewModel.
/// Sends StatusUpdateEvent for status bar updates that ViewModels can handle.
/// </summary>
public class ClipOperationsCoordinator :
    IRecipient<DeleteClipsRequestedEvent>,
    IRecipient<RenameClipRequestedEvent>,
    IRecipient<CopyToCollectionRequestedEvent>,
    IRecipient<CopyToLocalRequestedEvent>,
    IRecipient<MoveToCollectionRequestedEvent>,
    IRecipient<CreateNewClipRequestedEvent>,
    IRecipient<ExportToXmlRequestedEvent>,
    IRecipient<ExportToFilesRequestedEvent>,
    IRecipient<ShowSearchWindowEvent>,
    IRecipient<PowerPasteUpRequestedEvent>,
    IRecipient<PowerPasteDownRequestedEvent>,
    IRecipient<PowerPasteToggleRequestedEvent>,
    IRecipient<OpenSourceUrlRequestedEvent>,
    IRecipient<ToggleMacroRequestedEvent>,
    IRecipient<CleanUpTextRequestedEvent>,
    IRecipient<RemoveLineBreaksRequestedEvent>,
    IRecipient<StripNonTextRequestedEvent>,
    IRecipient<CaseConversionRequestedEvent>,
    IRecipient<ShowClipPropertiesRequestedEvent>,
    IRecipient<ClipSelectedEvent>,
    IRecipient<EncryptClipsRequestedEvent>,
    IRecipient<DecryptClipsRequestedEvent>,
    IRecipient<LockClipsRequestedEvent>,
    IRecipient<ForgetEncryptionKeyRequestedEvent>,
    IRecipient<EncryptionKeyExpiredEvent>,
    IRecipient<ShowEncryptionCancelledEvent>,
    IRecipient<ConvertFilePointerRequestedEvent>,
    IRecipient<MoveClipSortOrderRequestedEvent>
{
    // The clip list's icon for a macro clip
    private const string _macroIcon = "⌨";

    private readonly IActiveWindowService _activeWindowService;
    private readonly ClipListViewModel _clipListViewModel;
    private readonly IClipService _clipService;
    private readonly ICollectionService _collectionService;
    private readonly ICurrentClipWriter _currentClipWriter;
    private readonly IDatabases _databases;
    private readonly CollectionTreeViewModel _collectionTreeViewModel;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<ClipOperationsCoordinator> _logger;
    private readonly IMessenger _messenger;
    private readonly IPowerPaste _powerPaste;
    private readonly ISearchService _searchService;
    private readonly IServiceProvider _serviceProvider;
    private readonly TextTransformService _textTransformService;

    // Guard flag to prevent infinite decryption loop
    private bool _isDecryptingClip;

    public ClipOperationsCoordinator(IActiveWindowService activeWindowService,
        ClipListViewModel clipListViewModel,
        CollectionTreeViewModel collectionTreeViewModel,
        IClipService clipService,
        ICollectionService collectionService,
        IConfigurationService configurationService,
        IPowerPaste powerPaste,
        ISearchService searchService,
        IMessenger messenger,
        IDatabases databases,
        IServiceProvider serviceProvider,
        ICurrentClipWriter currentClipWriter,
        TextTransformService textTransformService,
        ILogger<ClipOperationsCoordinator> logger)
    {
        _currentClipWriter = currentClipWriter ?? throw new ArgumentNullException(nameof(currentClipWriter));
        _textTransformService = textTransformService ?? throw new ArgumentNullException(nameof(textTransformService));
        _activeWindowService = activeWindowService ?? throw new ArgumentNullException(nameof(activeWindowService));
        _clipListViewModel = clipListViewModel ?? throw new ArgumentNullException(nameof(clipListViewModel));
        _collectionTreeViewModel = collectionTreeViewModel ?? throw new ArgumentNullException(nameof(collectionTreeViewModel));
        _clipService = clipService ?? throw new ArgumentNullException(nameof(clipService));
        _collectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _powerPaste = powerPaste ?? throw new ArgumentNullException(nameof(powerPaste));
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
        _databases = databases ?? throw new ArgumentNullException(nameof(databases));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Register for all clip operation events
        _messenger.Register<DeleteClipsRequestedEvent>(this);
        _messenger.Register<RenameClipRequestedEvent>(this);
        _messenger.Register<CopyToCollectionRequestedEvent>(this);
        _messenger.Register<CopyToLocalRequestedEvent>(this);
        _messenger.Register<MoveToCollectionRequestedEvent>(this);
        _messenger.Register<CreateNewClipRequestedEvent>(this);
        _messenger.Register<ExportToXmlRequestedEvent>(this);
        _messenger.Register<ExportToFilesRequestedEvent>(this);
        _messenger.Register<ShowSearchWindowEvent>(this);
        _messenger.Register<PowerPasteUpRequestedEvent>(this);
        _messenger.Register<PowerPasteDownRequestedEvent>(this);
        _messenger.Register<EncryptClipsRequestedEvent>(this);
        _messenger.Register<DecryptClipsRequestedEvent>(this);
        _messenger.Register<LockClipsRequestedEvent>(this);
        _messenger.Register<ForgetEncryptionKeyRequestedEvent>(this);
        _messenger.Register<PowerPasteToggleRequestedEvent>(this);
        _messenger.Register<OpenSourceUrlRequestedEvent>(this);
        _messenger.Register<ToggleMacroRequestedEvent>(this);
        _messenger.Register<CleanUpTextRequestedEvent>(this);
        _messenger.Register<RemoveLineBreaksRequestedEvent>(this);
        _messenger.Register<StripNonTextRequestedEvent>(this);
        _messenger.Register<CaseConversionRequestedEvent>(this);
        _messenger.Register<ShowClipPropertiesRequestedEvent>(this);
        _messenger.Register<ClipSelectedEvent>(this);
        _messenger.Register<ShowEncryptionCancelledEvent>(this);
        _messenger.Register<ConvertFilePointerRequestedEvent>(this);
        _messenger.Register<MoveClipSortOrderRequestedEvent>(this);

        // The clip list shows the clip waiting to be pasted
        _powerPaste.PositionChanged += OnPowerPastePositionChanged;

        _logger.LogDebug("ClipOperationsCoordinator initialized and registered for events");
    }

    /// <summary>
    /// Handles CaseConversionRequestedEvent to convert text case in selected clip.
    /// </summary>
    public async void Receive(CaseConversionRequestedEvent message)
    {
        if (RejectIfReadOnlyDatabase())
            return;

        var conversion = message.ConversionType switch
        {
            CaseConversionType.Upper => CaseConversion.Uppercase,
            CaseConversionType.Lower => CaseConversion.Lowercase,
            CaseConversionType.Title => CaseConversion.TitleCase,
            CaseConversionType.Sentence => CaseConversion.SentenceCase,
            CaseConversionType.Toggle => CaseConversion.InvertCase,
            var other => throw new ArgumentOutOfRangeException(nameof(message), other, null),
        };

        await TransformSelectedClipsAsync(p => _textTransformService.ConvertCase(p, conversion), p => _textTransformService.ConvertCase(p, conversion), "Converted the case of");
    }

    /// <summary>
    /// Handles CleanUpTextRequestedEvent to clean up whitespace in selected clip.
    /// </summary>
    public async void Receive(CleanUpTextRequestedEvent message)
    {
        if (RejectIfReadOnlyDatabase())
            return;

        if (_clipListViewModel.GetSelectedClipsInDisplayOrder().Count == 0)
        {
            SendStatus("No clip selected");
            return;
        }

        var dialog = _serviceProvider.GetRequiredService<TextCleanupDialog>();
        dialog.Owner = _activeWindowService.DialogOwner;
        if (dialog.ShowDialog() != true)
            return;

        var options = dialog.Options;
        await TransformSelectedClipsAsync(p => _textTransformService.CleanUpText(p, options), p => _textTransformService.CleanUpTitle(p, options), "Cleaned up");
    }

    /// <summary>
    /// Changes the text of every selected clip and saves it, leaving each clip with only its text. Like editing a clip,
    /// this puts the first of them on the clipboard.
    /// </summary>
    private async Task TransformSelectedClipsAsync(Func<string, string> transform, Func<string, string>? titleTransform, string doneStatus)
    {
        var selectedClips = _clipListViewModel.GetSelectedClipsInDisplayOrder();

        if (selectedClips.Count == 0)
        {
            SendStatus("No clip selected");
            return;
        }

        var databaseKey = GetDatabaseKeyForSelectedNode();

        if (string.IsNullOrEmpty(databaseKey))
        {
            _logger.LogError("Cannot change the selected clips' text: database key not found");
            SendStatus("Error: database not found", true);
            return;
        }

        try
        {
            var changedCount = 0;
            foreach (var clip in selectedClips)
            {
                var changed = await _clipService.TransformTextAsync(databaseKey, clip.Id, transform, titleTransform);
                if (changed == null)
                    continue;

                ShowConvertedClip(clip, changed);
                changedCount++;
            }

            if (changedCount == 0)
            {
                SendStatus("The selected clips have no text to change");
                return;
            }

            // Show the new text in the viewer
            if (_clipListViewModel.SelectedClip is { } selectedClip)
                _messenger.Send(new ClipSelectedEvent(selectedClip, databaseKey, false));

            await _currentClipWriter.MakeCurrentAsync(databaseKey, selectedClips[0].Id);

            SendStatus($"{doneStatus} {changedCount} clip(s)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to change the text of the selected clips");
            SendStatus("Error changing the selected clips", true);
        }
    }

    /// <summary>
    /// Updates the clip shown in the list with what changing its text changed, and refreshes its row. The list keeps its
    /// selection, which reloading it would lose.
    /// </summary>
    private void ShowConvertedClip(Clip shown, Clip converted)
    {
        shown.Title = converted.Title;
        shown.ContentHash = converted.ContentHash;
        shown.Checksum = converted.Checksum;
        shown.Size = converted.Size;
        shown.Type = converted.Type;
        shown.LastModified = converted.LastModified;
        shown.IconGlyph = converted.IconGlyph;
        shown.HasText = converted.HasText;
        shown.HasRtf = converted.HasRtf;
        shown.HasHtml = converted.HasHtml;
        shown.HasBitmap = converted.HasBitmap;
        shown.HasFiles = converted.HasFiles;

        _messenger.Send(new ClipUpdatedMessage(shown.Id, shown.Title));
    }

    /// <summary>
    /// Handles ClipSelectedEvent: while PowerPaste is active, picking a clip of its sequence moves the sequence to it
    /// and picking any other clip cancels it. Also handles automatic decryption of encrypted clips.
    /// </summary>
    public async void Receive(ClipSelectedEvent message)
    {
        // Selections PowerPaste makes itself (following the sequence) are not the user's
        if (_powerPaste.State == PowerPasteState.Active
            && message is { IsUserSelection: true, SelectedClip: not null })
        {
            if (_powerPaste.MoveTo(message.SelectedClip.Id))
                SendStatus("PowerPaste moved to the selected clip");
            else
            {
                // ClipMate 7's words. The window writes the picked clip's own status as it handles this event and again
                // once the clip is copied, so the message goes after them and stays until the user picks again.
                UiThread.Queue(() => _messenger.Send(new StatusUpdateEvent("Cancelled - Clip Outside of Original PowerPaste Range.")
                {
                    KeepUntilUserSelects = true,
                }));
            }
        }

        // Handle encrypted clip selection
        // Guard against re-entrant calls while decrypting (prevents infinite loop from context menu)
        if (message.SelectedClip is not { Encrypted: true, IsDecrypted: false } || _isDecryptingClip)
            return;

        _isDecryptingClip = true;
        _logger.LogInformation("Selected clip {ClipId} is encrypted - prompting for passphrase", message.SelectedClip.Id);

        try
        {
            var databaseKey = message.DatabaseKey ?? GetDatabaseKeyForSelectedNode();
            if (string.IsNullOrEmpty(databaseKey))
            {
                _logger.LogError("No database key available for encrypted clip");
                _messenger.Send(new ShowEncryptionCancelledEvent());
                return;
            }

            // Check if we have a cached key
            SecureString? passphrase;
            int? expirationMinutes;
            bool shouldCacheKey;

            if (EncryptionKeyDialogViewModel.HasCachedKey)
            {
                // Use cached key and extend expiration
                _logger.LogInformation("Using cached encryption key");
                using var tempViewModel = new EncryptionKeyDialogViewModel(_messenger);
                tempViewModel.InitializeForDecryption();
                passphrase = tempViewModel.GetPassphrase();

                // Get expiration setting from cached key
                expirationMinutes = tempViewModel.RememberUntilShutdown
                    ? null
                    : tempViewModel.RetentionMinutes;

                shouldCacheKey = true; // Re-associate this clip with the cached key
            }
            else
            {
                // Prompt for passphrase
                var dialog = ActivatorUtilities.CreateInstance<EncryptionKeyDialog>(_serviceProvider);
                dialog.Owner = _activeWindowService.DialogOwner;
                dialog.ViewModel.InitializeForDecryption();

                var result = dialog.ShowDialog();
                if (result != true)
                {
                    _logger.LogInformation("User cancelled passphrase entry for clip {ClipId}", message.SelectedClip.Id);
                    _messenger.Send(new ShowEncryptionCancelledEvent());
                    return;
                }

                passphrase = dialog.ViewModel.GetPassphrase();

                // Get expiration setting from dialog
                expirationMinutes = dialog.ViewModel.RememberUntilShutdown
                    ? null
                    : dialog.ViewModel.RetentionMinutes;

                shouldCacheKey = dialog.ViewModel.RememberForMinutes || dialog.ViewModel.RememberUntilShutdown;
            }

            if (passphrase == null || passphrase.Length == 0)
            {
                _logger.LogWarning("Empty passphrase provided");
                _messenger.Send(new ShowEncryptionCancelledEvent());
                return;
            }

            // Convert SecureString to string temporarily for EncryptionKey creation
            var passphraseString = new NetworkCredential(null, passphrase).Password;
            var encryptionKey = EncryptionKey.FromPassphrase(passphraseString, expirationMinutes);


            // Decrypt the clip temporarily for viewing (isPermanent=false)
            // Pass the actual clip object to avoid fetching encrypted version from database
            // This will decrypt the Title in-memory (on the clip object) and set IsDecrypted=true
            await _clipService.DecryptClipAsync(databaseKey, message.SelectedClip, encryptionKey);

            _logger.LogInformation("Successfully decrypted clip {ClipId} for viewing", message.SelectedClip.Id);
            SendStatus("Clip decrypted for viewing");

            // Cache the key for this clip if requested
            if (shouldCacheKey)
            {
                using var tempViewModel = new EncryptionKeyDialogViewModel(_messenger);
                tempViewModel.SetPassphrase(passphrase);
                tempViewModel.RememberForMinutes = expirationMinutes.HasValue;
                tempViewModel.RememberUntilShutdown = !expirationMinutes.HasValue;
                if (expirationMinutes.HasValue)
                    tempViewModel.RetentionMinutes = expirationMinutes.Value;

                tempViewModel.CacheKey(message.SelectedClip.Id);
            }

            // Note: DecryptClipsAsync already sets IsDecrypted=true and decrypts the Title
            // Update icon to show unlocked state (🔓 instead of 🔒)
            if (message.SelectedClip.IconGlyph.StartsWith("🔒"))
                message.SelectedClip.IconGlyph = message.SelectedClip.IconGlyph.Replace("🔒", "🔓");

            // Send ClipUpdatedMessage to trigger grid row refresh with the NOW-DECRYPTED title
            // This refreshes the UI without disrupting selection (unlike Remove/Insert pattern)
            _messenger.Send(new ClipUpdatedMessage(message.SelectedClip.Id, message.SelectedClip.Title));

            // Send ClipSelectedEvent to update ClipViewer with decrypted content
            _messenger.Send(new ClipSelectedEvent(message.SelectedClip, databaseKey));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decrypting clip on selection");
            _messenger.Send(new ShowEncryptionCancelledEvent());
            DXMessageBox.Show($"Failed to decrypt clip: {ex.Message}", "Decryption Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isDecryptingClip = false;
        }
    }

    /// <summary>
    /// Handles ConvertFilePointerRequestedEvent by converting selected Files-type clips to plain text.
    /// </summary>
    public async void Receive(ConvertFilePointerRequestedEvent message)
    {
        if (RejectIfReadOnlyDatabase())
            return;

        var selectedClips = _clipListViewModel.SelectedClips.Where(p => p.Type == ClipType.Files).ToList();

        if (selectedClips.Count == 0)
        {
            SendStatus("No file-pointer clips selected");
            return;
        }

        var databaseKey = GetDatabaseKeyForSelectedNode();

        if (string.IsNullOrEmpty(databaseKey))
        {
            _logger.LogError("Cannot convert file pointer: database key not found");
            SendStatus("Error: database not found", true);
            return;
        }

        try
        {
            var convertedCount = 0;
            foreach (var item in selectedClips)
            {
                if (await _clipService.ConvertFilePointerToTextAsync(databaseKey, item.Id))
                    convertedCount++;
            }

            SendStatus($"Converted {convertedCount} clip(s) to text");

            _messenger.Send(new ReloadClipsRequestedEvent());

            _logger.LogInformation("Converted {Count} file-pointer clip(s) to text", convertedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert file pointer to text");
            SendStatus("Error converting file pointer to text", true);
        }
    }

    /// <summary>
    /// Handles CopyToCollectionRequestedEvent to copy clips to another collection.
    /// </summary>
    public async void Receive(CopyToCollectionRequestedEvent message)
    {
        // Copying a remote clip to a collection is Copy to local
        if (IsShowingReadOnlyDatabase())
        {
            await CopyToLocalAsync(new CopyToLocalRequestedEvent(message.ClipIds));
            return;
        }

        var selectedClips = _clipListViewModel.SelectedClips;

        if (selectedClips.Count == 0)
        {
            SendStatus("No clips selected");
            return;
        }

        var sourceDatabaseKey = GetDatabaseKeyForSelectedNode();

        if (string.IsNullOrEmpty(sourceDatabaseKey))
        {
            _logger.LogError("Cannot copy clips: source database key not found");
            SendStatus("Error: source database not found", true);
            return;
        }

        // Create and show collection picker dialog
        var dialog = new CollectionPickerDialog(_serviceProvider)
        {
            Message = $"Select a collection to copy {selectedClips.Count} clip(s) to:",
            Owner = _activeWindowService.DialogOwner,
        };

        await dialog.LoadCollectionsAsync();

        if (dialog.ShowDialog() != true || dialog.SelectedCollectionId is null || string.IsNullOrEmpty(dialog.SelectedDatabaseKey))
        {
            SendStatus("Copy cancelled");
            return;
        }

        try
        {
            var targetDatabaseKey = dialog.SelectedDatabaseKey;
            var isCrossDatabase = !sourceDatabaseKey.Equals(targetDatabaseKey, StringComparison.OrdinalIgnoreCase);

            var copiedCount = 0;

            foreach (var item in selectedClips)
            {
                if (isCrossDatabase)
                {
                    await _clipService.CopyClipCrossDatabaseAsync(
                        sourceDatabaseKey,
                        item.Id,
                        targetDatabaseKey,
                        dialog.SelectedCollectionId.Value);
                }
                else
                {
                    await _clipService.CopyClipAsync(
                        sourceDatabaseKey,
                        item.Id,
                        dialog.SelectedCollectionId.Value);
                }

                copiedCount++;
            }

            var databaseMessage = isCrossDatabase
                ? " (cross-database)"
                : string.Empty;

            SendStatus($"Copied {copiedCount} clip(s){databaseMessage}");

            // Request clip list reload
            _messenger.Send(new ReloadClipsRequestedEvent());

            _logger.LogInformation("Copied {Count} clip(s) to collection {CollectionId}", copiedCount, dialog.SelectedCollectionId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy clips to collection");
            SendStatus("Error copying clips", true);
        }
    }

    /// <summary>
    /// Handles CopyToLocalRequestedEvent: stores clips of a Remote Database as new clips in a local collection.
    /// </summary>
    public async void Receive(CopyToLocalRequestedEvent message) => await CopyToLocalAsync(message);

    /// <summary>
    /// Copy to local. Asks for the local collection unless the request names one, then copies each clip (never moves:
    /// the Remote Database is read-only).
    /// </summary>
    private async Task CopyToLocalAsync(CopyToLocalRequestedEvent message)
    {
        try
        {
            var sourceDatabaseKey = message.SourceDatabaseKey ?? _clipListViewModel.CurrentDatabaseKey;
            if (string.IsNullOrEmpty(sourceDatabaseKey) || _databases.Get(sourceDatabaseKey).Remote == null)
            {
                SendStatus("Copy to local copies clips from a Remote Database.", true);
                return;
            }

            var clipIds = message.ClipIds.Count > 0
                ? message.ClipIds
                : _clipListViewModel.SelectedClips.Select(p => p.Id).ToList();

            if (clipIds.Count == 0)
            {
                SendStatus("No clips selected");
                return;
            }

            var targetDatabaseKey = message.TargetDatabaseKey;
            var targetCollectionId = message.TargetCollectionId;
            if (string.IsNullOrEmpty(targetDatabaseKey) || targetCollectionId is null)
            {
                // The picker lists local collections only
                var dialog = new CollectionPickerDialog(_serviceProvider)
                {
                    Message = $"Select a local collection to copy {clipIds.Count} clip(s) to:",
                    Owner = _activeWindowService.DialogOwner,
                };

                await dialog.LoadCollectionsAsync();

                if (dialog.ShowDialog() != true || dialog.SelectedCollectionId is null || string.IsNullOrEmpty(dialog.SelectedDatabaseKey))
                {
                    SendStatus("Copy to local cancelled");
                    return;
                }

                targetDatabaseKey = dialog.SelectedDatabaseKey;
                targetCollectionId = dialog.SelectedCollectionId;
            }

            var copiedCount = 0;
            try
            {
                foreach (var item in clipIds)
                {
                    await _clipService.CopyToLocalAsync(sourceDatabaseKey, item, targetDatabaseKey, targetCollectionId.Value);
                    copiedCount++;
                }
            }
            catch (DatabaseUnavailableException ex)
            {
                _logger.LogInformation("Copy to local stopped after {Count} clip(s): {Reason}", copiedCount, ex.Message);
                SendStatus($"Copied {copiedCount} of {clipIds.Count} clip(s) to local: the Remote Database is unavailable", true);
                return;
            }

            SendStatus($"Copied {copiedCount} clip(s) to local");
            _logger.LogInformation("Copied {Count} clip(s) to local collection {CollectionId} in {DatabaseKey}",
                copiedCount, targetCollectionId.Value, targetDatabaseKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Copy to local failed");
            SendStatus("Error copying clips to local", true);
        }
    }

    /// <summary>
    /// Handles CreateNewClipRequestedEvent to create a new empty clip.
    /// </summary>
    public async void Receive(CreateNewClipRequestedEvent message)
    {
        var currentCollectionId = _clipListViewModel.CurrentCollectionId;

        if (currentCollectionId == null)
        {
            SendStatus("No collection selected");
            return;
        }

        var databaseKey = GetDatabaseKeyForSelectedNode();

        if (string.IsNullOrEmpty(databaseKey))
        {
            _logger.LogError("Cannot create clip: database key not found");
            SendStatus("Error: database not found", true);
            return;
        }

        try
        {
            var newClip = new Clip
            {
                Title = "New Clip",
                TextContent = string.Empty,
                CollectionId = currentCollectionId.Value,
                CapturedAt = DateTimeOffset.UtcNow,
                Type = ClipType.Text,
            };

            var createdClip = await _clipService.CreateAsync(databaseKey, newClip);

            SendStatus($"Created new clip: {createdClip.Title}");

            // Request clip list reload
            _messenger.Send(new ReloadClipsRequestedEvent());

            _logger.LogInformation("Created new clip {ClipId}", createdClip.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create new clip");
            SendStatus("Error creating new clip", true);
        }
    }

    /// <summary>
    /// Handles decryption request by showing dialog and decrypting clips.
    /// </summary>
    public async void Receive(DecryptClipsRequestedEvent message)
    {
        if (RejectIfReadOnlyDatabase())
            return;

        var clipIds = message.ClipIds.Any()
            ? message.ClipIds
            : _clipListViewModel.SelectedClips.Select(p => p.Id).ToList();

        if (clipIds.Count == 0)
        {
            DXMessageBox.Show("Please select one or more clips to decrypt.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var dialog = ActivatorUtilities.CreateInstance<EncryptionKeyDialog>(_serviceProvider);
            dialog.Owner = _activeWindowService.DialogOwner;
            dialog.ViewModel.InitializeForDecryption();

            var result = dialog.ShowDialog();
            if (result != true)
                return;

            var passphrase = dialog.ViewModel.GetPassphrase();
            if (passphrase == null || passphrase.Length == 0)
            {
                DXMessageBox.Show("Passphrase is required.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var databaseKey = GetDatabaseKeyForSelectedNode();
            if (string.IsNullOrEmpty(databaseKey))
            {
                _logger.LogError("No database key available for decryption");
                return;
            }

            // Convert SecureString to string temporarily for EncryptionKey creation
            var passphraseString = new NetworkCredential(null, passphrase).Password;
            var encryptionKey = EncryptionKey.FromPassphrase(passphraseString);

            // Permanently decrypt the selected clips
            await _clipService.DecryptClipsAsync(databaseKey, clipIds, encryptionKey, true);
            _logger.LogInformation("Decrypted {Count} clip(s)", clipIds.Count);
            SendStatus($"Decrypted {clipIds.Count} clip(s)");

            // Request clip list reload to reflect decrypted state
            _messenger.Send(new ReloadClipsRequestedEvent());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decrypting clips");
            DXMessageBox.Show($"Failed to decrypt clips: {ex.Message}", "Decryption Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Handles DeleteClipsRequestedEvent to delete selected clips with confirmation.
    /// </summary>
    public async void Receive(DeleteClipsRequestedEvent message)
    {
        if (RejectIfReadOnlyDatabase())
            return;

        var selectedClips = _clipListViewModel.SelectedClips;

        if (selectedClips.Count == 0)
        {
            SendStatus("No clips selected");
            return;
        }

        var clipCount = selectedClips.Count;
        var confirmMessage = clipCount == 1
            ? $"Delete '{selectedClips[0].Title}'?"
            : $"Delete {clipCount} clips?";

        var result = DXMessageBox.Show(
            confirmMessage,
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            var databaseKey = GetDatabaseKeyForSelectedNode();

            if (string.IsNullOrEmpty(databaseKey))
            {
                _logger.LogError("Cannot delete clips: database key not found");
                SendStatus("Error: database not found", true);
                return;
            }

            var deletedIds = new List<Guid>();
            foreach (var item in selectedClips)
            {
                await _clipService.DeleteAsync(databaseKey, item.Id);
                deletedIds.Add(item.Id);
            }

            SendStatus($"Deleted {clipCount} clip(s)");

            // Notify UI to remove deleted clips from collection
            _messenger.Send(new ClipsDeletedEvent(deletedIds));

            _logger.LogInformation("Deleted {Count} clip(s)", clipCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete clips");
            SendStatus("Error deleting clips", true);
        }
    }

    /// <summary>
    /// Handles encryption request by showing dialog and encrypting clips.
    /// </summary>
    public async void Receive(EncryptClipsRequestedEvent message)
    {
        if (RejectIfReadOnlyDatabase())
            return;

        var clipIds = message.ClipIds.Any()
            ? message.ClipIds
            : _clipListViewModel.SelectedClips.Select(p => p.Id).ToList();

        if (clipIds.Count == 0)
        {
            DXMessageBox.Show("Please select one or more clips to encrypt.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var dialog = ActivatorUtilities.CreateInstance<EncryptionKeyDialog>(_serviceProvider);
            dialog.Owner = _activeWindowService.DialogOwner;
            dialog.ViewModel.InitializeForEncryption();

            var result = dialog.ShowDialog();
            if (result != true)
                return;

            var passphrase = dialog.ViewModel.GetPassphrase();
            if (passphrase == null || passphrase.Length == 0)
            {
                DXMessageBox.Show("Passphrase is required.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var databaseKey = GetDatabaseKeyForSelectedNode();
            if (string.IsNullOrEmpty(databaseKey))
            {
                _logger.LogError("No database key available for encryption");
                return;
            }

            // Convert SecureString to string temporarily for EncryptionKey creation
            var passphraseString = new NetworkCredential(null, passphrase).Password;
            var encryptionKey = EncryptionKey.FromPassphrase(passphraseString);
            await _clipService.EncryptClipsAsync(databaseKey, clipIds, encryptionKey);
            _logger.LogInformation("Encrypted {Count} clip(s)", clipIds.Count);
            SendStatus($"Encrypted {clipIds.Count} clip(s)");

            // Request clip list reload to show encrypted status
            _messenger.Send(new ReloadClipsRequestedEvent());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encrypting clips");
            DXMessageBox.Show($"Failed to encrypt clips: {ex.Message}", "Encryption Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Handles encryption key expiration event - re-locks all temporarily decrypted clips.
    /// </summary>
    public async void Receive(EncryptionKeyExpiredEvent message)
    {
        try
        {
            var databaseKey = GetDatabaseKeyForSelectedNode();
            if (string.IsNullOrEmpty(databaseKey))
            {
                _logger.LogError("No database key available for re-locking clips after key expiration");
                return;
            }

            await _clipService.LockClipsAsync(databaseKey);
            _logger.LogInformation("Re-locked temporarily decrypted clips after key expiration");
            SendStatus("Encryption key expired - clips locked");

            // Refresh UI to show locked state
            _messenger.Send(new PreferencesChangedEvent());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error re-locking clips after key expiration");
        }
    }

    /// <summary>
    /// Handles ExportToFilesRequestedEvent to display the flat-file export dialog.
    /// </summary>
    public void Receive(ExportToFilesRequestedEvent message)
    {
        var selectedClips = _clipListViewModel.SelectedClips;

        if (selectedClips.Count == 0)
        {
            DXMessageBox.Show(
                "Please select at least one clip to export.",
                "No Selection",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        var vm = ActivatorUtilities.CreateInstance<FlatFileExportViewModel>(_serviceProvider);
        vm.Initialize(_clipListViewModel.CurrentDatabaseKey!, selectedClips);

        var dialog = new FlatFileExportDialog(vm)
        {
            Owner = _activeWindowService.DialogOwner,
        };

        dialog.ShowDialog();
    }

    /// <summary>
    /// Handles ExportToXmlRequestedEvent to display the XML export dialog.
    /// </summary>
    public async void Receive(ExportToXmlRequestedEvent message)
    {
        var selectedClips = _clipListViewModel.SelectedClips;

        // Get current collection info
        var collectionName = "Clips";
        Guid? collectionId = null;

        if (_collectionTreeViewModel.SelectedNode is CollectionTreeNode collectionNode)
        {
            collectionName = collectionNode.Collection.Title;
            collectionId = collectionNode.Collection.Id;
        }

        var vm = ActivatorUtilities.CreateInstance<XmlExportViewModel>(_serviceProvider);

        // Check if only one clip is selected - offer to export entire collection
        if (selectedClips.Count == 1 && collectionId != null)
        {
            var result = DXMessageBox.Show(
                $"You have selected only one clip. Would you like to select the whole collection [{collectionName}] instead?",
                "Confirm Action",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Initialize with empty list first, then load entire collection
                vm.Initialize([], collectionName, collectionId);
                await vm.LoadEntireCollectionAsync();
            }
            else
                vm.Initialize(selectedClips, collectionName, collectionId);
        }
        else
            vm.Initialize(selectedClips, collectionName, collectionId);

        var dialog = new XmlExportDialog(vm)
        {
            Owner = _activeWindowService.DialogOwner,
        };

        dialog.ShowDialog();
    }

    /// <summary>
    /// Handles forget encryption key request.
    /// </summary>
    public void Receive(ForgetEncryptionKeyRequestedEvent message)
    {
        try
        {
            EncryptionKeyDialogViewModel.ForgetKey();
            _logger.LogInformation("Encryption key forgotten");
            SendStatus("Encryption key cleared from memory");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forgetting encryption key");
        }
    }

    /// <summary>
    /// Handles lock request by clearing decrypted BLOB cache and forgetting encryption key.
    /// </summary>
    public async void Receive(LockClipsRequestedEvent message)
    {
        try
        {
            var databaseKey = GetDatabaseKeyForSelectedNode();
            if (string.IsNullOrEmpty(databaseKey))
            {
                _logger.LogError("No database key available for locking clips");
                return;
            }

            // Determine which clips to lock
            IReadOnlyList<Guid>? clipIdsToLock;
            if (message.ClipIds.Any())
            {
                // Lock specific clips from message
                clipIdsToLock = message.ClipIds;
            }
            else if (message.LockAll)
            {
                // Lock all cached clips (pass null to service)
                clipIdsToLock = null;
            }
            else
            {
                // Lock selected clips
                clipIdsToLock = _clipListViewModel.SelectedClips.Select(p => p.Id).ToList();
            }

            var lockedClipIds = await _clipService.LockClipsAsync(databaseKey, clipIdsToLock);
            _logger.LogInformation("Locked {Count} encrypted clips", lockedClipIds.Count);

            // Forget cached keys only for the clips we actually locked
            // This allows selective locking while preserving keys for other decrypted clips
            if (lockedClipIds.Count > 0)
            {
                EncryptionKeyDialogViewModel.ForgetKeysForClips(lockedClipIds);
                _logger.LogInformation("Forgot encryption keys for {Count} locked clip(s)", lockedClipIds.Count);
            }

            // Notify UI that clips' cache expired so they refresh to show locked state
            // ClearClip doesn't invoke the expiration callback, so we send the message here
            foreach (var item in lockedClipIds)
                _messenger.Send(new ClipCacheExpiredMessage(item));

            if (lockedClipIds.Count > 0)
                SendStatus($"Locked {lockedClipIds.Count} clip(s)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error locking clips");
        }
    }

    /// <summary>
    /// Handles MoveClipSortOrderRequestedEvent by moving the selected clip to the top/bottom of its sort order.
    /// </summary>
    public async void Receive(MoveClipSortOrderRequestedEvent message)
    {
        if (RejectIfReadOnlyDatabase())
            return;

        var selectedClip = _clipListViewModel.SelectedClip;

        if (selectedClip == null)
        {
            SendStatus("No clip selected");
            return;
        }

        var databaseKey = GetDatabaseKeyForSelectedNode();

        if (string.IsNullOrEmpty(databaseKey))
        {
            _logger.LogError("Cannot move clip: database key not found");
            SendStatus("Error: database not found", true);
            return;
        }

        try
        {
            await _clipService.MoveClipSortOrderAsync(databaseKey, selectedClip.Id, message.ToTop);

            SendStatus(message.ToTop
                ? "Moved clip to top of sort order"
                : "Moved clip to bottom of sort order");

            _messenger.Send(new ReloadClipsRequestedEvent());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move clip sort order");
            SendStatus("Error moving clip", true);
        }
    }

    /// <summary>
    /// Handles MoveToCollectionRequestedEvent to move clips to another collection.
    /// </summary>
    public async void Receive(MoveToCollectionRequestedEvent message)
    {
        if (RejectIfReadOnlyDatabase())
            return;

        var selectedClips = _clipListViewModel.SelectedClips;

        if (selectedClips.Count == 0)
        {
            SendStatus("No clips selected");
            return;
        }

        var sourceDatabaseKey = GetDatabaseKeyForSelectedNode();

        if (string.IsNullOrEmpty(sourceDatabaseKey))
        {
            _logger.LogError("Cannot move clips: source database key not found");
            SendStatus("Error: source database not found", true);
            return;
        }

        // Create and show collection picker dialog
        var dialog = new CollectionPickerDialog(_serviceProvider)
        {
            Message = $"Select a collection to move {selectedClips.Count} clip(s) to:",
            Owner = _activeWindowService.DialogOwner,
        };

        await dialog.LoadCollectionsAsync();

        if (dialog.ShowDialog() != true || dialog.SelectedCollectionId is null || string.IsNullOrEmpty(dialog.SelectedDatabaseKey))
        {
            SendStatus("Move cancelled");
            return;
        }

        try
        {
            var targetDatabaseKey = dialog.SelectedDatabaseKey;
            var isCrossDatabase = !sourceDatabaseKey.Equals(targetDatabaseKey, StringComparison.OrdinalIgnoreCase);

            var movedCount = 0;

            foreach (var item in selectedClips)
            {
                if (isCrossDatabase)
                {
                    await _clipService.MoveClipCrossDatabaseAsync(
                        sourceDatabaseKey,
                        item.Id,
                        targetDatabaseKey,
                        dialog.SelectedCollectionId.Value);
                }
                else
                {
                    await _clipService.MoveClipAsync(
                        sourceDatabaseKey,
                        item.Id,
                        dialog.SelectedCollectionId.Value);
                }

                movedCount++;
            }

            var databaseMessage = isCrossDatabase
                ? " (cross-database)"
                : string.Empty;

            SendStatus($"Moved {movedCount} clip(s){databaseMessage}");

            // Request clip list reload
            _messenger.Send(new ReloadClipsRequestedEvent());

            _logger.LogInformation("Moved {Count} clip(s) to collection {CollectionId}", movedCount, dialog.SelectedCollectionId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move clips to collection");
            SendStatus("Error moving clips", true);
        }
    }

    /// <summary>
    /// Handles OpenSourceUrlRequestedEvent to open the source URL in the default browser.
    /// </summary>
    public void Receive(OpenSourceUrlRequestedEvent message)
    {
        var selectedClip = _clipListViewModel.SelectedClip;

        if (selectedClip == null)
        {
            SendStatus("No clip selected");
            return;
        }

        if (string.IsNullOrEmpty(selectedClip.SourceUrl))
        {
            SendStatus("Selected clip has no source URL");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = selectedClip.SourceUrl,
                UseShellExecute = true,
            });

            _logger.LogInformation("Opened source URL: {Url}", selectedClip.SourceUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open source URL: {Url}", selectedClip.SourceUrl);
            SendStatus($"Failed to open URL: {ex.Message}", true);
        }
    }

    /// <summary>
    /// Handles ToggleMacroRequestedEvent: flips the selected clip's macro flag and saves it (manual: Macro Pasting).
    /// </summary>
    public async void Receive(ToggleMacroRequestedEvent message)
    {
        var selectedClip = _clipListViewModel.SelectedClip;
        var databaseKey = _clipListViewModel.CurrentDatabaseKey;

        if (selectedClip == null || string.IsNullOrEmpty(databaseKey))
        {
            SendStatus("No clip selected");
            return;
        }

        try
        {
            selectedClip.Macro = !selectedClip.Macro;
            selectedClip.LastModified = DateTimeOffset.Now;
            await _clipService.UpdateAsync(databaseKey, selectedClip);

            // The list's icon is worked out when clips load; show or hide the macro icon now
            var icons = selectedClip.IconGlyph?.Replace(_macroIcon, string.Empty) ?? string.Empty;
            selectedClip.IconGlyph = selectedClip.Macro ? MacroIconAfterLock(icons) : icons;
            _messenger.Send(new ClipUpdatedMessage(selectedClip.Id, selectedClip.Title));

            SendStatus(selectedClip.Macro ? "Clip is now a macro" : "Clip is no longer a macro");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle the macro flag of clip {ClipId}", selectedClip.Id);
            SendStatus($"Failed to toggle macro: {ex.Message}", true);
        }
    }

    // The clip list shows a lock first (encrypted clips), then the macro icon, then the Format icons
    private static string MacroIconAfterLock(string icons) =>
        icons.StartsWith("🔒", StringComparison.Ordinal) || icons.StartsWith("🔓", StringComparison.Ordinal)
            ? icons[..2] + _macroIcon + icons[2..]
            : _macroIcon + icons;

    /// <summary>
    /// Handles PowerPasteDownRequestedEvent: starts going down, or stops when already going down.
    /// </summary>
    public async void Receive(PowerPasteDownRequestedEvent message) =>
        await RunPowerPasteCommandAsync((getClips, loadContent) => _powerPaste.ToggleAsync(PowerPasteDirection.Down, getClips, loadContent));

    /// <summary>
    /// Handles PowerPasteToggleRequestedEvent: stops when on, and otherwise starts in the last direction used.
    /// </summary>
    public async void Receive(PowerPasteToggleRequestedEvent message) =>
        await RunPowerPasteCommandAsync((getClips, loadContent) => _powerPaste.ToggleAsync(getClips, loadContent));

    /// <summary>
    /// Handles PowerPasteUpRequestedEvent: starts going up, or stops when already going up.
    /// </summary>
    public async void Receive(PowerPasteUpRequestedEvent message) =>
        await RunPowerPasteCommandAsync((getClips, loadContent) => _powerPaste.ToggleAsync(PowerPasteDirection.Up, getClips, loadContent));

    /// <summary>
    /// Runs a Power Paste command over the clip list and reports what it did. Each clip's content is loaded when it
    /// comes up: clips from the grid only carry metadata until loaded, and Power Paste offers every Format of each one.
    /// </summary>
    private async Task RunPowerPasteCommandAsync(Func<Func<PowerPasteDirection, IReadOnlyList<Clip>>, Func<Clip, Task>, Task> command)
    {
        var wasActive = _powerPaste.State == PowerPasteState.Active;
        var clipCount = 0;
        var databaseKey = GetDatabaseKeyForSelectedNode();

        try
        {
            await command(direction =>
                {
                    var clips = _clipListViewModel.GetClipsForPowerPaste(direction);
                    clipCount = clips.Count;
                    if (clips.Count == 0)
                        SendStatus("No clips selected for PowerPaste");

                    return clips;
                },
                clip => string.IsNullOrEmpty(databaseKey)
                    ? Task.CompletedTask
                    : _clipService.LoadBlobDataAsync(databaseKey, clip));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run the PowerPaste command");
            SendStatus("Error starting PowerPaste", true);

            return;
        }

        if (clipCount > 0)
            SendStatus($"PowerPaste {_powerPaste.Direction} started with {clipCount} clip(s)");
        else if (wasActive && _powerPaste.State != PowerPasteState.Active)
            SendStatus("PowerPaste stopped");
    }

    /// <summary>
    /// Selects the clip waiting to be pasted in the clip list, without copying it or counting it as the user's pick.
    /// </summary>
    private void OnPowerPastePositionChanged(object? sender, PowerPastePositionChangedEventArgs e)
    {
        if (e.CurrentClip is { } clip)
            UiThread.Run(() => _clipListViewModel.SelectClipWithoutCopying(clip.Id));
    }

    /// <summary>
    /// Handles RemoveLineBreaksRequestedEvent to remove line breaks from selected clip.
    /// </summary>
    public async void Receive(RemoveLineBreaksRequestedEvent message)
    {
        if (RejectIfReadOnlyDatabase())
            return;

        var mode = message.RemoveAll ? LineBreakMode.RemoveAll : LineBreakMode.PreserveParagraphs;
        await TransformSelectedClipsAsync(p => _textTransformService.RemoveLineBreaks(p, mode, message.JoinWithoutSpaces), null, "Removed the line breaks of");
    }

    /// <summary>
    /// Handles RenameClipRequestedEvent to rename a clip with a dialog.
    /// </summary>
    public async void Receive(RenameClipRequestedEvent message)
    {
        if (RejectIfReadOnlyDatabase())
            return;

        var selectedClip = _clipListViewModel.SelectedClip;

        if (selectedClip == null)
        {
            SendStatus("No clip selected");
            return;
        }

        var databaseKey = GetDatabaseKeyForSelectedNode();

        if (string.IsNullOrEmpty(databaseKey))
        {
            _logger.LogError("Cannot rename clip: database key not found");
            SendStatus("Error: database not found", true);
            return;
        }

        try
        {
            // Get the RenameClipDialogViewModel from DI
            var viewModel = _serviceProvider.GetService<RenameClipDialogViewModel>();

            if (viewModel == null)
            {
                _logger.LogError("RenameClipDialogViewModel not found in DI container");
                SendStatus("Error: dialog service not available", true);
                return;
            }

            // Get existing shortcut if any
            var shortcutService = _serviceProvider.GetService<ShortcutService>();
            Shortcut? existingShortcut = null;

            if (shortcutService != null)
            {
                try
                {
                    existingShortcut = await shortcutService.GetByClipIdAsync(databaseKey, selectedClip.Id);
                }
                catch (Exception ex) when (ex.Message.Contains("no such table"))
                {
                    // ShortCut table doesn't exist yet - this is OK
                    _logger.LogDebug("ShortCut table not found - will be created on first shortcut save");
                }
            }

            // Initialize the dialog ViewModel
            await viewModel.InitializeAsync(
                selectedClip.Id,
                databaseKey,
                selectedClip.Title,
                existingShortcut?.Nickname);

            // Create and show the dialog
            var dialog = new RenameClipDialog
            {
                DataContext = viewModel,
                Owner = _activeWindowService.DialogOwner,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            if (dialog.ShowDialog() == true)
            {
                // Update the clip in the collection
                var clip = _clipListViewModel.Clips.FirstOrDefault(p => p.Id == selectedClip.Id);

                clip?.Title = viewModel.Title;

                var title = viewModel.Title ?? string.Empty;
                SendStatus($"Updated clip: {title}");

                _logger.LogInformation("Renamed clip {ClipId} to '{Title}'", selectedClip.Id, viewModel.Title);
            }
            else
                SendStatus("Rename cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show rename dialog");
            SendStatus("Error showing rename dialog", true);
        }
    }

    /// <summary>
    /// Handles ShowClipPropertiesRequestedEvent to show the clip properties dialog.
    /// </summary>
    public async void Receive(ShowClipPropertiesRequestedEvent message)
    {
        var selectedClip = _clipListViewModel.SelectedClip;

        if (selectedClip == null)
        {
            _logger.LogDebug("ShowClipProperties: No clip selected");
            return;
        }

        try
        {
            var dialog = new ClipPropertiesDialog();
            var viewModel = _serviceProvider.GetRequiredService<ClipPropertiesViewModel>();

            await viewModel.LoadClipAsync(selectedClip);
            dialog.DataContext = viewModel;
            dialog.Owner = _activeWindowService.DialogOwner;
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show clip properties for clip {ClipId}", selectedClip.Id);
            SendStatus($"Failed to show clip properties: {ex.Message}", true);
        }
    }

    /// <summary>
    /// Handles ShowEncryptionCancelledEvent (forwarded to ClipViewerControl).
    /// </summary>
    public void Receive(ShowEncryptionCancelledEvent message)
    {
        // This event is primarily for ClipViewerControl to handle
        // Coordinator just needs to be registered to avoid warnings
    }

    /// <summary>
    /// Handles ShowSearchWindowEvent to display the search dialog.
    /// </summary>
    public void Receive(ShowSearchWindowEvent message)
    {
        _logger.LogInformation("Showing search window");

        try
        {
            var searchViewModel = _serviceProvider.GetRequiredService<SearchViewModel>();
            var logger = _serviceProvider.GetRequiredService<ILogger<SearchDialog>>();
            var dialog = new SearchDialog(searchViewModel, _searchService, logger)
            {
                Owner = _activeWindowService.DialogOwner,
            };

            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show search window");
            SendStatus("Error showing search window", true);
        }
    }

    /// <summary>
    /// Handles StripNonTextRequestedEvent to remove non-text formats from selected clip.
    /// </summary>
    public async void Receive(StripNonTextRequestedEvent message)
    {
        if (RejectIfReadOnlyDatabase())
            return;

        await TransformSelectedClipsAsync(p => p, null, "Stripped the non-text formats of");
    }

    /// <summary>
    /// Gets the database configuration key for the currently selected node.
    /// </summary>
    private string? GetDatabaseKeyForSelectedNode()
    {
        var node = _collectionTreeViewModel.SelectedNode;

        if (node == null)
            return null;

        // Traverse up the tree to find the DatabaseTreeNode
        var current = node;

        while (current != null)
        {
            if (current is DatabaseTreeNode dbNode)
                return dbNode.DatabasePath;

            current = current.Parent;
        }

        return null;
    }

    /// <summary>
    /// Sends a status update message to be displayed in the UI.
    /// </summary>
    /// <param name="message">The status message.</param>
    /// <param name="isError">Whether this is an error message.</param>
    /// <summary>
    /// A read-only Database (every Remote Database) can't be changed: refuses any request that would change, move,
    /// rename, delete, encrypt or decrypt the clips it shows.
    /// </summary>
    /// <returns><c>true</c> if the request was refused.</returns>
    private bool RejectIfReadOnlyDatabase()
    {
        if (!IsShowingReadOnlyDatabase())
            return false;

        SendStatus(DatabaseMessages.ReadOnly, true);
        return true;
    }

    private bool IsShowingReadOnlyDatabase() => _clipListViewModel.IsReadOnly;

    private void SendStatus(string message, bool isError = false) => _messenger.Send(new StatusUpdateEvent(message, isError));
}
