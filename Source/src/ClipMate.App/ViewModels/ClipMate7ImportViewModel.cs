using System.IO;
using ClipMate.Core.Helpers;
using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Models.Export;
using ClipMate.Core.Models.Import;
using ClipMate.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClipMate.App.ViewModels;

/// <summary>
/// A database the import can merge into.
/// </summary>
/// <param name="Name">Display name.</param>
/// <param name="FilePath">SQLite database file path.</param>
public sealed record ClipMate7ImportTarget(string Name, string FilePath);

/// <summary>
/// ViewModel for importing an original ClipMate 7.x database, either into a new database or merged into an existing one.
/// </summary>
public partial class ClipMate7ImportViewModel : ObservableObject
{
    private readonly List<string> _encryptionKeys = [];
    private readonly IClipMate7ImportService _importService;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEncryptedClips))]
    private int _encryptedClipCount;

    /// <summary>
    /// A ClipMate 7 key being entered. ClipMate 7 asks for a key each time clips are encrypted, so the user can add
    /// several (<see cref="AddEncryptionKeyCommand" />).
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddEncryptionKeyCommand))]
    private string _encryptionKey = string.Empty;

    /// <summary>
    /// How many keys have been added and how many encrypted clips they open. The keys themselves are never shown.
    /// </summary>
    [ObservableProperty]
    private string _encryptionKeysStatus = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteImportCommand))]
    private bool _isAnalyzed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DoneCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExecuteImportCommand))]
    private bool _isComplete;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMerge))]
    private bool _isNewDatabase = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteImportCommand))]
    private bool _isProcessing;

    [ObservableProperty]
    private string _newDatabaseFilePath;

    [ObservableProperty]
    private string _newDatabaseName = "ClipMate 7 Import";

    [ObservableProperty]
    private string _processingLog = string.Empty;

    [ObservableProperty]
    private int _progressMaximum = 100;

    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private ClipMate7ImportTarget? _selectedTarget;

    [ObservableProperty]
    private string _sourceFolder = string.Empty;

    public ClipMate7ImportViewModel(IClipMate7ImportService importService, IEnumerable<ClipMate7ImportTarget> targets)
    {
        _importService = importService ?? throw new ArgumentNullException(nameof(importService));
        Targets = targets.ToList();
        SelectedTarget = Targets.FirstOrDefault();

        _newDatabaseFilePath = Path.Combine(ClipMatePaths.DataDirectory, "ClipMate7 Import", "clipmate.db");
    }

    /// <summary>
    /// Default location of a ClipMate 7 database.
    /// </summary>
    public static string DefaultSourceFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Thornsoft Development", "ClipMate7");

    /// <summary>
    /// Existing databases that clips can be merged into.
    /// </summary>
    public IReadOnlyList<ClipMate7ImportTarget> Targets { get; }

    /// <summary>
    /// True when the ClipMate 7 database has encrypted clips, which need their ClipMate 7 key to be imported.
    /// </summary>
    public bool HasEncryptedClips => EncryptedClipCount > 0;

    public bool IsMerge
    {
        get => !IsNewDatabase;
        set => IsNewDatabase = !value;
    }

    /// <summary>
    /// The database created by a successful "new database" import, to be added to the database list.
    /// </summary>
    public DatabaseConfiguration? CreatedDatabase { get; private set; }

    /// <summary>
    /// The existing database clips were merged into by a successful import.
    /// </summary>
    public string? MergedDatabasePath { get; private set; }

    /// <summary>
    /// Raised when the dialog should close.
    /// </summary>
    public event EventHandler? CloseRequested;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(SourceFolder) && _importService.IsClipMate7Database(DefaultSourceFolder))
            await SetSourceFolderAsync(DefaultSourceFolder);
    }

    /// <summary>
    /// Sets the ClipMate 7 database folder and analyzes it.
    /// </summary>
    public async Task SetSourceFolderAsync(string folder)
    {
        SourceFolder = folder;
        IsAnalyzed = false;
        EncryptedClipCount = 0;
        _encryptionKeys.Clear();
        EncryptionKey = string.Empty;
        EncryptionKeysStatus = string.Empty;

        if (!_importService.IsClipMate7Database(folder))
        {
            ProcessingLog = "This folder does not contain a ClipMate 7 database (clip.dat and coll.dat were not found).\n";
            return;
        }

        ProcessingLog = "Reading ClipMate 7 database...\n";

        try
        {
            var preview = await _importService.AnalyzeAsync(folder);

            ProcessingLog = $"Found {preview.ClipCount} clip(s), {preview.CollectionCount} collection(s), " +
                            $"{preview.FormatCount} clip format(s) and {preview.ShortcutCount} shortcut(s).\n";

            foreach (var item in preview.Warnings)
                ProcessingLog += $"Note: {item}\n";

            EncryptedClipCount = preview.EncryptedClipCount;
            IsAnalyzed = true;
        }
        catch (Exception ex)
        {
            ProcessingLog = $"{ex.Message}\n";
        }
    }

    private bool CanAddEncryptionKey() => EncryptionKey.Length > 0 && !IsProcessing;

    /// <summary>
    /// Adds the key being entered, then reports how many encrypted clips the keys added so far open.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddEncryptionKey))]
    private async Task AddEncryptionKey()
    {
        if (!_encryptionKeys.Contains(EncryptionKey, StringComparer.Ordinal))
            _encryptionKeys.Add(EncryptionKey);

        EncryptionKey = string.Empty;
        await UpdateEncryptionKeysStatusAsync();
    }

    [RelayCommand]
    private void ClearEncryptionKeys()
    {
        _encryptionKeys.Clear();
        EncryptionKey = string.Empty;
        EncryptionKeysStatus = string.Empty;
    }

    private async Task UpdateEncryptionKeysStatusAsync()
    {
        try
        {
            var opened = await _importService.CountDecryptableClipsAsync(SourceFolder, _encryptionKeys);
            EncryptionKeysStatus = $"{_encryptionKeys.Count} key(s) entered. They open {opened} of {EncryptedClipCount} encrypted clip(s)" +
                                   (opened < EncryptedClipCount
                                       ? "; add the key(s) for the rest, or they are skipped."
                                       : ".");
        }
        catch (Exception ex)
        {
            EncryptionKeysStatus = ex.Message;
        }
    }

    private bool CanExecuteImport() => IsAnalyzed && !IsProcessing && !IsComplete;

    [RelayCommand(CanExecute = nameof(CanExecuteImport))]
    private async Task ExecuteImport()
    {
        var targetPath = IsNewDatabase
            ? Environment.ExpandEnvironmentVariables(NewDatabaseFilePath ?? string.Empty)
            : SelectedTarget?.FilePath;

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            ProcessingLog += "Error: Please choose a target database.\n";
            return;
        }

        if (IsNewDatabase)
        {
            if (string.IsNullOrWhiteSpace(NewDatabaseName))
            {
                ProcessingLog += "Error: Please enter a name for the new database.\n";
                return;
            }

            if (File.Exists(targetPath))
            {
                ProcessingLog += $"Error: A database already exists at {targetPath}. Choose another file, or merge into it instead.\n";
                return;
            }
        }

        IsProcessing = true;
        ProgressValue = 0;
        ProcessingLog += IsNewDatabase
            ? $"Importing into new database {targetPath}...\n"
            : $"Merging into {SelectedTarget!.Name}...\n";

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var result = await _importService.ImportAsync(new ClipMate7ImportOptions
            {
                SourceFolder = SourceFolder,
                TargetDatabasePath = targetPath,
                CreateNewDatabase = IsNewDatabase,
                EncryptionKeys = HasEncryptedClips
                    ? [.._encryptionKeys, EncryptionKey] // A key typed but not added counts too
                    : [],
            }, OnProgress, _cancellationTokenSource.Token);

            ProcessingLog += FormatSummary(result);

            if (IsNewDatabase)
            {
                CreatedDatabase = new DatabaseConfiguration
                {
                    Name = NewDatabaseName.Trim(),
                    FilePath = targetPath,
                    AutoLoad = true,
                };

                ProcessingLog += "The new database has been added to the database list. Click OK in Options to save and open it.\n";
            }
            else
                MergedDatabasePath = targetPath;

            IsComplete = true;
        }
        catch (OperationCanceledException)
        {
            ProcessingLog += "Import cancelled. No changes were made.\n";
        }
        catch (Exception ex)
        {
            ProcessingLog += $"Import failed: {ex.Message}\nNo changes were made.\n";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private void OnProgress(ExportProgressMessage message)
    {
        if (message.TotalCount > 0)
        {
            ProgressMaximum = message.TotalCount;
            ProgressValue = message.ProcessedCount;
        }

        if (!message.IsComplete)
            ProcessingLog += $"{message.Message}\n";
    }

    private static string FormatSummary(ClipMate7ImportResult result)
    {
        var summary = $"Imported {result.ClipsImported} clip(s), {result.CollectionsImported} collection(s) and {result.ShortcutsImported} shortcut(s).\n";

        if (result.DuplicateClipsSkipped > 0)
            summary += $"Skipped {result.DuplicateClipsSkipped} clip(s) already in the database.\n";

        if (result.EncryptedClipsImported > 0)
            summary += $"{result.EncryptedClipsImported} encrypted clip(s) were imported and are still encrypted, each with the key that opened it.\n";

        if (result.EncryptedClipsSkipped > 0)
            summary += $"Skipped {result.EncryptedClipsSkipped} encrypted clip(s) that none of the ClipMate 7 keys entered opens.\n";

        if (result.UnreadableClipsSkipped > 0)
            summary += $"Skipped {result.UnreadableClipsSkipped} clip(s) with no importable content.\n";

        if (result.VirtualCollectionsSkipped > 0)
            summary += $"Skipped {result.VirtualCollectionsSkipped} virtual collection(s); ClipMate provides its own.\n";

        if (result.UnsupportedFormatsSkipped > 0)
            summary += $"Dropped {result.UnsupportedFormatsSkipped} unsupported clip format(s).\n";

        foreach (var item in result.Warnings.Take(10))
            summary += $"Warning: {item}\n";

        return summary;
    }

    /// <summary>
    /// Cancels a running import, or closes the dialog.
    /// </summary>
    [RelayCommand]
    private void Abort()
    {
        if (IsProcessing)
        {
            _cancellationTokenSource?.Cancel();
            ProcessingLog += "Cancelling import...\n";
        }
        else
            CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool CanDone() => IsComplete;

    [RelayCommand(CanExecute = nameof(CanDone))]
    private void Done() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
