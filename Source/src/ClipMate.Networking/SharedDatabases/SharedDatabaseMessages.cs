using System.Globalization;
using ClipMate.Core.Models.Networking;
using ClipMate.Networking.Protocol;

namespace ClipMate.Networking.SharedDatabases;

/// <summary>
/// Converts Shared Database models to and from their wire messages. What arrives from a Peer is checked and cleaned:
/// entries with invalid IDs are dropped and text is stripped of control characters and shortened.
/// </summary>
internal static class SharedDatabaseMessages
{
    private const int _maxNameLength = 128;

    public static SharedDatabaseInfo ToMessage(SharedDatabase database) => new()
    {
        DatabaseId = database.DatabaseId.ToString("D", CultureInfo.InvariantCulture),
        Name = database.Name,
    };

    public static CollectionEntry ToMessage(SharedCollection collection) => new()
    {
        Id = collection.Id.ToString("D", CultureInfo.InvariantCulture),
        ParentId = collection.ParentId?.ToString("D", CultureInfo.InvariantCulture) ?? string.Empty,
        Title = collection.Title,
        Kind = collection.Kind switch
        {
            SharedCollectionKind.Folder => CollectionKind.Folder,
            SharedCollectionKind.Smart => CollectionKind.Smart,
            SharedCollectionKind.SqlBased => CollectionKind.SqlBased,
            var _ => CollectionKind.Collection,
        },
        SortKey = collection.SortKey,
    };

    public static IReadOnlyList<SharedDatabase> FromMessage(ListSharedDatabasesReply reply)
    {
        var result = new List<SharedDatabase>(reply.Databases.Count);
        foreach (var item in reply.Databases)
        {
            if (Guid.TryParse(item.DatabaseId, out var databaseId) && result.All(p => p.DatabaseId != databaseId))
                result.Add(new SharedDatabase(databaseId, CleanName(item.Name, "Database")));
        }

        return result;
    }

    public static IReadOnlyList<SharedCollection> FromMessage(GetCollectionTreeReply reply)
    {
        var result = new List<SharedCollection>(reply.Collections.Count);
        foreach (var item in reply.Collections)
        {
            if (!Guid.TryParse(item.Id, out var id))
                continue;

            Guid? parentId = Guid.TryParse(item.ParentId, out var parent) && parent != id ? parent : null;
            var kind = item.Kind switch
            {
                CollectionKind.Folder => SharedCollectionKind.Folder,
                CollectionKind.Smart => SharedCollectionKind.Smart,
                CollectionKind.SqlBased => SharedCollectionKind.SqlBased,
                var _ => SharedCollectionKind.Collection,
            };

            result.Add(new SharedCollection(id, parentId, CleanName(item.Title, "Collection"), kind, item.SortKey));
        }

        return result;
    }

    private static string CleanName(string name, string fallback)
    {
        var clean = new string(name.Where(p => !char.IsControl(p)).ToArray()).Trim();
        if (clean.Length > _maxNameLength)
            clean = clean[.._maxNameLength];

        return clean.Length == 0 ? fallback : clean;
    }
}
