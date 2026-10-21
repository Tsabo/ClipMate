using System.Globalization;
using ClipMate.Core.Constants;
using ClipMate.Core.Models;
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
    private const int _maxTextLength = 2048;
    private const int _maxTitleLength = 4096;

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

    public static ClipSummary ToMessage(Clip clip) => new()
    {
        Id = clip.Id.ToString("D", CultureInfo.InvariantCulture),
        Title = clip.Title ?? string.Empty,
        CapturedAtUnixMs = clip.CapturedAt.ToUnixTimeMilliseconds(),
        CapturedAtOffsetMinutes = (int)clip.CapturedAt.Offset.TotalMinutes,
        Size = clip.Size,
        Kind = clip.Type switch
        {
            ClipType.RichText => ClipKind.RichText,
            ClipType.Html => ClipKind.Html,
            ClipType.Image => ClipKind.Image,
            ClipType.Files => ClipKind.Files,
            ClipType.Custom => ClipKind.Custom,
            var _ => ClipKind.Text,
        },
        HasText = clip.HasText,
        HasRtf = clip.HasRtf,
        HasHtml = clip.HasHtml,
        HasBitmap = clip.HasBitmap,
        HasFiles = clip.HasFiles,
        SourceApplicationName = clip.SourceApplicationName ?? string.Empty,
        SourceApplicationTitle = clip.SourceApplicationTitle ?? string.Empty,
        SourceUrl = clip.SourceUrl ?? string.Empty,
        Creator = clip.Creator ?? string.Empty,
        CustomTitle = clip.CustomTitle,
        Macro = clip.Macro,
        SortKey = clip.SortKey,
        Encrypted = clip.Encrypted,
        EncryptionSalt = clip.EncryptionSalt ?? string.Empty,
        EncryptionIv = clip.EncryptionIv ?? string.Empty,
        EncryptionMethod = clip.EncryptionMethod ?? string.Empty,
        CollectionId = clip.CollectionId?.ToString("D", CultureInfo.InvariantCulture) ?? string.Empty,
        FolderId = clip.FolderId?.ToString("D", CultureInfo.InvariantCulture) ?? string.Empty,
    };

    /// <summary>
    /// Reads a clip summary from a Peer, or <c>null</c> if it is unusable (an invalid ID).
    /// </summary>
    public static Clip? FromMessage(ClipSummary summary)
    {
        if (!Guid.TryParse(summary.Id, out var id))
            return null;

        DateTimeOffset capturedAt;
        try
        {
            var offset = TimeSpan.FromMinutes(Math.Clamp(summary.CapturedAtOffsetMinutes, -14 * 60, 14 * 60));
            capturedAt = DateTimeOffset.FromUnixTimeMilliseconds(summary.CapturedAtUnixMs).ToOffset(offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            capturedAt = DateTimeOffset.UnixEpoch;
        }

        var clip = new Clip
        {
            Id = id,
            Title = CleanOptional(summary.Title, _maxTitleLength),
            CapturedAt = capturedAt,
            Size = Math.Max(0, summary.Size),
            Type = summary.Kind switch
            {
                ClipKind.RichText => ClipType.RichText,
                ClipKind.Html => ClipType.Html,
                ClipKind.Image => ClipType.Image,
                ClipKind.Files => ClipType.Files,
                ClipKind.Custom => ClipType.Custom,
                var _ => ClipType.Text,
            },
            HasText = summary.HasText,
            HasRtf = summary.HasRtf,
            HasHtml = summary.HasHtml,
            HasBitmap = summary.HasBitmap,
            HasFiles = summary.HasFiles,
            SourceApplicationName = CleanOptional(summary.SourceApplicationName, _maxTextLength),
            SourceApplicationTitle = CleanOptional(summary.SourceApplicationTitle, _maxTextLength),
            SourceUrl = CleanOptional(summary.SourceUrl, _maxTextLength),
            Creator = CleanOptional(summary.Creator, _maxNameLength),
            CustomTitle = summary.CustomTitle,
            Macro = summary.Macro,
            SortKey = summary.SortKey,
            Encrypted = summary.Encrypted,
            EncryptionSalt = CleanOptional(summary.EncryptionSalt, _maxTextLength),
            EncryptionIv = CleanOptional(summary.EncryptionIv, _maxTextLength),
            EncryptionMethod = CleanOptional(summary.EncryptionMethod, _maxNameLength),
            CollectionId = Guid.TryParse(summary.CollectionId, out var collectionId) ? collectionId : null,
            FolderId = Guid.TryParse(summary.FolderId, out var folderId) ? folderId : null,
        };

        clip.RefreshIconGlyph();
        return clip;
    }

    public static ClipPage FromMessage(GetClipPageReply reply)
    {
        var clips = reply.Clips.Select(FromMessage).OfType<Clip>().ToList();
        var offset = Math.Max(0, reply.Offset);
        return new ClipPage(clips, offset, Math.Max(reply.TotalCount, offset + clips.Count));
    }

    public static ClipFormat ToMessage(ClipData format) => new()
    {
        Id = format.Id.ToString("D", CultureInfo.InvariantCulture),
        Format = format.Format,
        FormatName = format.FormatName,
        StorageType = format.StorageType,
        Size = format.Size,
    };

    /// <summary>
    /// Reads a clip format from a Peer, or <c>null</c> if it is unusable (an invalid ID or unknown storage type).
    /// </summary>
    public static ClipData? FromMessage(ClipFormat format, Guid clipId)
    {
        if (!Guid.TryParse(format.Id, out var id) || format.StorageType is < StorageType.Text or > StorageType.Binary)
            return null;

        return new ClipData
        {
            Id = id,
            ClipId = clipId,
            Format = format.Format,
            FormatName = CleanName(format.FormatName, "Unknown"),
            StorageType = format.StorageType,
            Size = Math.Max(0, format.Size),
        };
    }

    public static ChangeNotice ToMessage(SharedCollectionChange change) => new()
    {
        CollectionChanged = new CollectionChanged
        {
            DatabaseId = change.DatabaseId.ToString("D", CultureInfo.InvariantCulture),
            CollectionId = change.CollectionId.ToString("D", CultureInfo.InvariantCulture),
        },
    };

    /// <summary>
    /// Reads a change notice from a Peer, or <c>null</c> if it is unusable (invalid IDs, or a kind this version does not know).
    /// </summary>
    public static PeerChangeNotice? FromMessage(ChangeNotice notice) => notice.ChangeCase switch
    {
        ChangeNotice.ChangeOneofCase.CollectionChanged when Guid.TryParse(notice.CollectionChanged.DatabaseId, out var databaseId) &&
                                                            Guid.TryParse(notice.CollectionChanged.CollectionId, out var collectionId) =>
            new CollectionChangedNotice(new SharedCollectionChange(databaseId, collectionId)),
        ChangeNotice.ChangeOneofCase.SharedDatabasesChanged => new SharedDatabasesChangedNotice(),
        var _ => null,
    };

    private static string? CleanOptional(string text, int maxLength)
    {
        var clean = new string(text.Where(p => !char.IsControl(p)).ToArray()).Trim();
        if (clean.Length > maxLength)
            clean = clean[..maxLength];

        return clean.Length == 0 ? null : clean;
    }

    private static string CleanName(string name, string fallback)
    {
        var clean = new string(name.Where(p => !char.IsControl(p)).ToArray()).Trim();
        if (clean.Length > _maxNameLength)
            clean = clean[.._maxNameLength];

        return clean.Length == 0 ? fallback : clean;
    }
}
