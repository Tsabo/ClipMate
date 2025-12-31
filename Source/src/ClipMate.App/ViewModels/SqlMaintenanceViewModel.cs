using System.Data;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace ClipMate.App.ViewModels;

/// <summary>
/// ViewModel for the SQL Maintenance dialog.
/// </summary>
public partial class SqlMaintenanceViewModel : ObservableObject
{
    private static readonly HashSet<string> DangerousKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "DROP", "DELETE", "UPDATE", "INSERT", "ALTER", "CREATE", "TRUNCATE",
        "REPLACE", "ATTACH", "DETACH", "VACUUM", "REINDEX", "PRAGMA",
    };

    private readonly ILogger<SqlMaintenanceViewModel> _logger;

    [ObservableProperty]
    private string _databaseName = string.Empty;

    [ObservableProperty]
    private bool _hasResults;

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private DataTable? _resultsTable;

    [ObservableProperty]
    private string _sqlQuery = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlMaintenanceViewModel" /> class.
    /// </summary>
    public SqlMaintenanceViewModel(ILogger<SqlMaintenanceViewModel> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Checks if the SQL query contains potentially dangerous keywords.
    /// </summary>
    /// <param name="sql">The SQL query to check.</param>
    /// <returns>A list of dangerous keywords found, or empty if safe.</returns>
    public static IReadOnlyList<string> GetDangerousKeywords(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return Array.Empty<string>();

        var found = new List<string>();
        var words = sql.Split([' ', '\t', '\n', '\r', '(', ')', ';', ','],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var item in words)
        {
            if (DangerousKeywords.Contains(item) && !found.Contains(item, StringComparer.OrdinalIgnoreCase))
                found.Add(item.ToUpperInvariant());
        }

        return found;
    }

    /// <summary>
    /// Formats the results table as a text string for export.
    /// </summary>
    public string FormatResultsAsText()
    {
        if (ResultsTable == null || ResultsTable.Columns.Count == 0)
            return StatusMessage;

        var sb = new StringBuilder();

        // Header row
        var columnNames = ResultsTable.Columns.Cast<DataColumn>().Select(column => column.ColumnName).ToList();

        sb.AppendLine(string.Join(", ", columnNames));
        sb.AppendLine(new string('-', 40));

        // Data rows
        foreach (DataRow item in ResultsTable.Rows)
        {
            var values = item.ItemArray.Select(p => p?.ToString() ?? "NULL").ToList();

            sb.AppendLine(string.Join(", ", values));
        }

        sb.AppendLine(new string('-', 40));
        sb.AppendLine(StatusMessage);

        return sb.ToString();
    }

    /// <summary>
    /// Clears the results and status.
    /// </summary>
    [RelayCommand]
    private void ClearResults()
    {
        ResultsTable = null;
        HasResults = false;
        StatusMessage = "Ready";
    }
}
