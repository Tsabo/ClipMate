using ClipMate.Core.Models.Configuration;
using ClipMate.Core.Services.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClipMate.App.ViewModels;

/// <summary>
/// What the Database properties dialog needs to check whether a Database can be shared with Peers.
/// </summary>
/// <param name="SharedDatabases">Finds a Shared Database that already uses the same Database ID.</param>
/// <param name="DatabaseIds">Gives a Database a new Database ID.</param>
/// <param name="OtherDatabases">The other configured Databases, as currently edited.</param>
public sealed record DatabaseSharingContext(
    ISharedDatabaseService SharedDatabases,
    IDatabaseIdService DatabaseIds,
    IReadOnlyList<DatabaseConfiguration> OtherDatabases);

/// <summary>
/// ViewModel for the DatabaseEditDialog.
/// </summary>
public partial class DatabaseEditViewModel : ObservableObject
{
    private const string _databaseIdNotAssignedText = "Assigned when the database is opened";

    private int _checkVersion;

    /// <summary>
    /// Read-only display of the permanent Database ID stored in the database file.
    /// </summary>
    [ObservableProperty]
    private string _databaseIdText = _databaseIdNotAssignedText;

    [ObservableProperty]
    private string _databaseName = "My Clips";

    [ObservableProperty]
    private string _databaseFilePath = string.Empty;

    [ObservableProperty]
    private bool _autoLoad = true;

    [ObservableProperty]
    private bool _multiUser;

    [ObservableProperty]
    private string _userName = Environment.UserName;

    [ObservableProperty]
    private bool _allowBackup = true;

    [ObservableProperty]
    private bool _readOnly;

    [ObservableProperty]
    private int _purgeDays = 7;

    [ObservableProperty]
    private CleanupMethod _cleanupMethod = CleanupMethod.AtStartup;

    [ObservableProperty]
    private TempFileLocation _tempFileLocation = TempFileLocation.DatabaseDirectory;

    /// <summary>
    /// Whether paired Devices can browse this Database (read-only) while networking is on.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAccept))]
    private bool _shareWithPeers;

    /// <summary>
    /// Why this Database cannot be shared, or null when it can.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasShareConflict), nameof(CanAccept))]
    [NotifyCanExecuteChangedFor(nameof(GiveNewDatabaseIdCommand))]
    private string? _shareConflictMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAccept))]
    private bool _isCheckingSharing;

    private DatabaseSharingContext? _sharing;

    /// <summary>
    /// True when another Shared Database already uses this Database's Database ID.
    /// </summary>
    public bool HasShareConflict => ShareConflictMessage != null;

    /// <summary>
    /// False while sharing is turned on for a Database that cannot be shared (or is still being checked).
    /// </summary>
    public bool CanAccept => !ShareWithPeers || !HasShareConflict && !IsCheckingSharing;

    /// <summary>
    /// Sets the Database ID shown in the dialog; null when the database does not have one yet.
    /// </summary>
    public void SetDatabaseId(Guid? databaseId) =>
        DatabaseIdText = databaseId?.ToString("D") ?? _databaseIdNotAssignedText;

    /// <summary>
    /// Turns on checking that this Database can be shared: two Shared Databases with one Database ID (a copied file
    /// keeps its ID) cannot be told apart by Peers, so sharing such a Database is refused until it gets a new ID.
    /// </summary>
    public void UseSharingChecks(DatabaseSharingContext sharing)
    {
        _sharing = sharing ?? throw new ArgumentNullException(nameof(sharing));
        _ = CheckSharingAsync();
    }

    /// <summary>
    /// Checks whether this Database can be shared, setting <see cref="ShareConflictMessage" />.
    /// </summary>
    public async Task CheckSharingAsync()
    {
        var version = Interlocked.Increment(ref _checkVersion);
        if (!ShareWithPeers || _sharing == null || string.IsNullOrWhiteSpace(DatabaseFilePath))
        {
            IsCheckingSharing = false;
            ShareConflictMessage = null;
            return;
        }

        IsCheckingSharing = true;
        string? message;
        try
        {
            var conflict = await _sharing.SharedDatabases.FindDatabaseIdConflictAsync(DatabaseFilePath, _sharing.OtherDatabases);
            message = conflict == null
                ? null
                : $"“{conflict.Name}” is already shared with the same Database ID, probably because one database file " +
                  "was copied from the other. Peers could not tell the two apart, so this Database can be shared only once it has its own Database ID.";
        }
        catch (Exception ex)
        {
            message = $"Could not check whether this Database can be shared: {ex.Message}";
        }

        if (version != Volatile.Read(ref _checkVersion))
            return; // A newer check started meanwhile

        ShareConflictMessage = message;
        IsCheckingSharing = false;
    }

    public void LoadFrom(DatabaseConfiguration config)
    {
        DatabaseName = config.Name;
        DatabaseFilePath = config.FilePath;
        AutoLoad = config.AutoLoad;
        MultiUser = config.MultiUser;
        UserName = config.UserName;
        AllowBackup = config.AllowBackup;
        ReadOnly = config.ReadOnly;
        PurgeDays = config.PurgeDays;
        CleanupMethod = config.CleanupMethod;
        TempFileLocation = config.TempFileLocation;
        ShareWithPeers = config.ShareWithPeers;
    }

    public DatabaseConfiguration ToConfiguration()
    {
        return new DatabaseConfiguration
        {
            Name = DatabaseName,
            FilePath = DatabaseFilePath,
            AutoLoad = AutoLoad,
            MultiUser = MultiUser,
            UserName = UserName,
            AllowBackup = AllowBackup,
            ReadOnly = ReadOnly,
            PurgeDays = PurgeDays,
            CleanupMethod = CleanupMethod,
            TempFileLocation = TempFileLocation,
            UseModificationTimeStamp = true,
            ShareWithPeers = ShareWithPeers,
        };
    }

    /// <summary>
    /// Gives this Database's file a new Database ID, so it can be shared alongside the Database it was copied from.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasShareConflict))]
    private async Task GiveNewDatabaseIdAsync()
    {
        if (_sharing == null)
            return;

        try
        {
            SetDatabaseId(await _sharing.DatabaseIds.AssignNewDatabaseIdAsync(DatabaseFilePath));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ShareConflictMessage = $"Could not give this Database a new ID: {ex.Message}";
            return;
        }

        await CheckSharingAsync();
    }

    partial void OnShareWithPeersChanged(bool value) => _ = CheckSharingAsync();

    partial void OnDatabaseFilePathChanged(string value) => _ = CheckSharingAsync();
}
