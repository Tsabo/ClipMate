using ClipMate.Core.Models.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the DatabaseEditDialog.
/// </summary>
public partial class DatabaseEditViewModel : ObservableObject
{
    private const string _databaseIdNotAssignedText = "Assigned when the database is opened";

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
    /// Sets the Database ID shown in the dialog; null when the database does not have one yet.
    /// </summary>
    public void SetDatabaseId(Guid? databaseId) =>
        DatabaseIdText = databaseId?.ToString("D") ?? _databaseIdNotAssignedText;

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
        };
    }
}
