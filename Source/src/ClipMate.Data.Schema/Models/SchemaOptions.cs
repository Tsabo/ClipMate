namespace ClipMate.Data.Schema.Models;

/// <summary>
/// Configuration options for schema operations.
/// </summary>
public class SchemaOptions
{
    public HashSet<string> IgnoredTables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, HashSet<string>> IgnoredColumns { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Tables that are no longer in the model and are dropped when found. Any other table missing from the model is
    /// only reported.
    /// </summary>
    public HashSet<string> RetiredTables { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool ValidateBeforeMigration { get; set; } = true;
    public bool EnableCaching { get; set; } = true;
}
