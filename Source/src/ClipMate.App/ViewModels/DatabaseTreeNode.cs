namespace ClipMate.App.ViewModels;

/// <summary>
/// Represents a database root node in the tree (e.g., "My Clips").
/// </summary>
public class DatabaseTreeNode : TreeNodeBase
{
    public DatabaseTreeNode(string name, string databasePath)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        DatabasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
    }

    /// <summary>
    /// Database connection string or identifier.
    /// </summary>
    public string DatabasePath { get; }

    public override string Name { get; }

    public override string Icon => "💾"; // Database icon

    public override TreeNodeType NodeType => TreeNodeType.Database;
}
