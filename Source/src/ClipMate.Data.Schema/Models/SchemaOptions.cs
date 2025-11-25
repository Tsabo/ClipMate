namespace ClipMate.Data.Schema.Models;

/// <summary>
/// Configuration options for schema operations.
/// </summary>
public class SchemaOptions
{
    public HashSet<string> IgnoredTables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, HashSet<string>> IgnoredColumns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool ValidateBeforeMigration { get; set; } = true;
    public bool EnableCaching { get; set; } = true;
}
