using System.Security.Cryptography;
using System.Text;
using ClipMate.Core.Constants;
using ClipMate.Core.Models;
using ClipMate.Platform.Helpers;
using Microsoft.Data.Sqlite;

namespace ClipMate.UITests.DocsScreenshots;

/// <summary>
/// Replaces the content of a database ClipMate has just created with the sample clips, written the way ClipMate's own
/// capture stores them, and leaves a file that stands alone: no WAL, and nothing that changes from run to run.
/// </summary>
public static class SampleDatabaseWriter
{
    private const string _creator = "Sam@HOME-PC";
    private const int _englishUnitedStates = 1033;

    // Fixed, unlike the current time and random ID ClipMate gave them when it created the database
    private static readonly DateTime _collectionsCreatedAt = new(2026, 10, 1, 9, 0, 0);
    private static readonly Guid _databaseId = StableId("database");

    public static void Write(string databasePath)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString());
        connection.Open();

        using (var transaction = connection.BeginTransaction())
        {
            // Everything ClipMate seeded, and anything it captured from the clipboard while it was running
            foreach (var table in new[] { "MonacoEditorState", "ShortCut", "BlobTxt", "BlobJpg", "BlobPng", "BlobBlob", "ClipData", "Clips" })
                Execute(connection, $"Delete from \"{table}\"");

            AddCollection(connection, SampleCollections.Recipes, "Recipes", 360);
            AddCollection(connection, SampleCollections.Work, "Work", 370);

            var sortKey = 0;
            foreach (var clip in SampleClips.Create())
            {
                sortKey += 100;
                AddClip(connection, clip, sortKey);
            }

            Execute(connection, """
                                Update Collections set
                                    CreatedAt = @time,
                                    LastUpdateTime = case when LastUpdateTime is null then null else @time end,
                                    ModifiedAt = case when ModifiedAt is null then null else @time end,
                                    LastKnownCount = (select count(*) from Clips where Clips.CollectionId = Collections.Id)
                                """, ("@time", _collectionsCreatedAt));
            Execute(connection, "Update DatabaseInfo set DatabaseId = @id", ("@id", Text(_databaseId)));
            transaction.Commit();
        }

        Execute(connection, "Vacuum");
        Execute(connection, "Pragma wal_checkpoint(TRUNCATE)");
        Execute(connection, "Pragma journal_mode=DELETE");
    }

    private static void AddCollection(SqliteConnection connection, Guid id, string title, int sortKey)
    {
        // As CollectionService.CreateAsync creates a collection the user adds
        Execute(connection, """
                            Insert into Collections (Id, ParentGuid, Title, Name, LmType, ListType, SortKey, SortOrder, IlIndex, RetentionLimit, MaxClips, MaxBytes, MaxAgeDays,
                                Role, NewClipsGo, IsActive, AcceptNewClips, ReadOnly, AcceptDuplicates, SortColumn, SortAscending, Encrypted, Favorite, CreatedAt, ModifiedAt, LastUpdateTime)
                            values (@id, null, @title, @title, @normal, @normalList, @sortKey, @sortKey, 0, 0, 0, 0, 0,
                                0, 0, 0, 1, 0, 1, 0, 0, 0, 0, @time, @time, @time)
                            """,
            ("@id", Text(id)), ("@title", title), ("@normal", (int)CollectionLmType.Normal), ("@normalList", (int)CollectionListType.Normal),
            ("@sortKey", sortKey), ("@time", _collectionsCreatedAt));
    }

    private static void AddClip(SqliteConnection connection, Clip clip, int sortKey)
    {
        clip.TextContent = clip.TextContent?.ReplaceLineEndings("\r\n");
        var hash = ContentHasher.HashClip(clip);
        var id = StableId($"clip {sortKey}");

        // The title and size as capture works them out (ClipboardSnapshotDecoder)
        var title = clip.Title ?? clip.TextContent!.Split("\r\n")[0];
        var size = ((clip.TextContent?.Length ?? 0) + (clip.RtfContent?.Length ?? 0) + (clip.HtmlContent?.Length ?? 0)) * 2 + (clip.ImageData?.Length ?? 0);
        var viewTab = clip.Type switch
        {
            ClipType.RichText => 1,
            ClipType.Html => 2,
            var _ => 0,
        };

        Execute(connection, """
                            Insert into Clips (Id, CollectionId, UserId, Title, Creator, CapturedAt, SortKey, SourceUrl, CustomTitle, Locale, WrapCheck, Encrypted, Icons, Del, Size,
                                Checksum, ViewTab, Macro, LastModified, ContentHash, SourceApplicationName, SourceApplicationTitle, PasteCount, IsFavorite, Type)
                            values (@id, @collection, 0, @title, @creator, @capturedAt, @sortKey, @sourceUrl, @customTitle, @locale, 0, 0, 0, 0, @size,
                                @checksum, @viewTab, 0, @capturedAt, @hash, @application, @window, 0, 0, @type)
                            """,
            ("@id", Text(id)), ("@collection", Text(clip.CollectionId!.Value)), ("@title", title), ("@creator", _creator), ("@capturedAt", clip.CapturedAt),
            ("@sortKey", sortKey), ("@sourceUrl", clip.SourceUrl), ("@customTitle", clip.CustomTitle), ("@locale", _englishUnitedStates), ("@size", size),
            ("@checksum", ContentHasher.Checksum(hash)), ("@viewTab", viewTab), ("@hash", hash), ("@application", clip.SourceApplicationName),
            ("@window", clip.SourceApplicationTitle), ("@type", (int)clip.Type));

        // The formats as ClipContentStorage stores a captured clip
        AddText(connection, id, Formats.UnicodeText.Name, Formats.UnicodeText.Code, clip.TextContent);
        AddText(connection, id, "CF_RTF", Formats.RichText.Code, clip.RtfContent);
        AddText(connection, id, Formats.Html.Name, Formats.Html.Code, clip.HtmlContent);
        if (clip.ImageData != null)
            AddBlob(connection, "BlobPng", id, Formats.Dib.Name, Formats.Dib.Code, StorageType.Png, clip.ImageData.Length, clip.ImageData);

        if (clip.FilePathsJson != null)
            AddBlob(connection, "BlobBlob", id, Formats.HDrop.Name, Formats.HDrop.Code, StorageType.Binary, clip.FilePathsJson.Length * 2, Encoding.UTF8.GetBytes(clip.FilePathsJson));
    }

    private static void AddText(SqliteConnection connection, Guid clipId, string formatName, int format, string? data)
    {
        if (data != null)
            AddBlob(connection, "BlobTxt", clipId, formatName, format, StorageType.Text, data.Length * 2, data);
    }

    private static void AddBlob(SqliteConnection connection, string table, Guid clipId, string formatName, int format, int storageType, int size, object data)
    {
        var clipDataId = StableId($"{clipId} {formatName}");
        Execute(connection, "Insert into ClipData (Id, ClipId, FormatName, Format, Size, StorageType) values (@id, @clip, @name, @format, @size, @storage)",
            ("@id", Text(clipDataId)), ("@clip", Text(clipId)), ("@name", formatName), ("@format", format), ("@size", size), ("@storage", storageType));
        Execute(connection, $"Insert into \"{table}\" (Id, ClipDataId, ClipId, Data) values (@id, @clipData, @clip, @data)",
            ("@id", Text(StableId($"{clipDataId} blob"))), ("@clipData", Text(clipDataId)), ("@clip", Text(clipId)), ("@data", data));
    }

    private static void Execute(SqliteConnection connection, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// EF Core stores a Guid as upper-case text; Microsoft.Data.Sqlite on its own would write a BLOB.
    /// </summary>
    private static string Text(Guid id) => id.ToString("D").ToUpperInvariant();

    private static Guid StableId(string name) => new(MD5.HashData(Encoding.UTF8.GetBytes(name)));
}
