using System.Data;

namespace ClipMate.Data.Services;

/// <summary>
/// Result of a SQL query execution.
/// </summary>
public sealed class SqlQueryResult
{
    /// <summary>
    /// Gets or sets the result data table.
    /// </summary>
    public DataTable? Data { get; init; }

    /// <summary>
    /// Gets or sets the number of rows returned.
    /// </summary>
    public int RowCount { get; init; }

    /// <summary>
    /// Gets whether the query returned any results.
    /// </summary>
    public bool HasResults => RowCount > 0;
}
