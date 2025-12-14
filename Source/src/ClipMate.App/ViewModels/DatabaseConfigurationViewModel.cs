using System.IO;
using ClipMate.Core.Models.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClipMate.App.ViewModels;

/// <summary>
/// Wrapper for DatabaseConfiguration with additional UI properties.
/// </summary>
public partial class DatabaseConfigurationViewModel : ObservableObject
{
    [ObservableProperty]
    private string _status = "✅";

    [ObservableProperty]
    private string _statusTooltip = "OK";

    public DatabaseConfigurationViewModel(string databaseKey, DatabaseConfiguration config)
    {
        DatabaseKey = databaseKey;
        Configuration = config;
        UpdateStatus();
    }

    /// <summary>
    /// Original dictionary key for this database.
    /// </summary>
    public string DatabaseKey { get; set; }

    public DatabaseConfiguration Configuration { get; }

    public string Name => Configuration.Name;

    public string FilePath => Configuration.FilePath;

    public bool ReadOnly => Configuration.ReadOnly;

    public bool MultiUser => Configuration.MultiUser;

    public string UserName => Configuration.UserName;

    public int PurgeDays => Configuration.PurgeDays;

    public string? RemoteHost => Configuration.RemoteHost;

    public void UpdateStatus()
    {
        if (string.IsNullOrEmpty(Configuration.FilePath))
        {
            Status = "❌";
            StatusTooltip = "No database file specified";
            return;
        }

        var expandedPath = Environment.ExpandEnvironmentVariables(Configuration.FilePath);

        if (!File.Exists(expandedPath))
        {
            Status = "❌";
            StatusTooltip = "Database file not found";
        }
        else
        {
            Status = "✅";
            StatusTooltip = "OK";
        }
    }
}
