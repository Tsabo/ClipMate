namespace ClipMate.App.Models.TreeNodes;

/// <summary>
/// Represents a database root node in the tree (e.g., "My Clips").
/// </summary>
public class DatabaseTreeNode : TreeNodeBase
{
    public DatabaseTreeNode(string name, string databasePath, bool hasError = false)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        DatabasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
        HasError = hasError;
    }

    /// <summary>
    /// Database connection string or identifier.
    /// </summary>
    public string DatabasePath { get; }

    /// <summary>
    /// Indicates whether the database has an error (missing file, invalid schema, etc.).
    /// </summary>
    public bool HasError { get; set; }

    public override string Name { get; }

    public override string Icon => IconFor(HasError);

    /// <summary>
    /// The icon of a local Database: a red X when it has an error, else the database icon. Also shown for it in the
    /// search dialog's "Search in".
    /// </summary>
    public static string IconFor(bool hasError) => hasError
        ? "❌"
        : "💾";

    public override TreeNodeType NodeType => TreeNodeType.Database;
}
