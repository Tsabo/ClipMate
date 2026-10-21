namespace ClipMate.Core.Models;

/// <summary>
/// Single-row record stored inside every database file that identifies the Database.
/// Because it lives in the file, the Database ID survives renaming the Database and copying the file.
/// </summary>
public class DatabaseInfo
{
    /// <summary>
    /// The fixed primary key of the single row.
    /// </summary>
    public const int SingletonId = 1;

    /// <summary>
    /// Gets or sets the primary key. Always <see cref="SingletonId" />.
    /// </summary>
    public int Id { get; set; } = SingletonId;

    /// <summary>
    /// Gets or sets the permanent Database ID, generated once. It changes only when the user explicitly gives the
    /// Database a new ID (e.g. so a copied file can be shared alongside its original).
    /// </summary>
    public Guid DatabaseId { get; set; }
}
