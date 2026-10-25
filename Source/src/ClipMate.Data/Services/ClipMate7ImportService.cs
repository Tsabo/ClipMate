using ClipMate.Core.Models;
using ClipMate.Core.Models.Export;
using ClipMate.Core.Models.Import;
using ClipMate.Core.Services;
using ClipMate.Core.ValueObjects;
using ClipMate.Data.Legacy.ClipMate7;
using ClipMate.Data.Legacy.Dbisam;
using ClipMate.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Imports an original ClipMate 7.x database (Elevatesoft DBISAM 4 tables) into a ClipMate SQLite database.
/// </summary>
/// <remarks>
/// ClipMate 7 tables map almost 1:1 onto this schema: COLL → Collections, CLIP → Clips, CLIPDATA + BLOB* → ClipData +
/// BLOB tables, SHORTCUT → Shortcuts. GUIDs are preserved, so collections that also exist in the default ClipMate
/// layout (Inbox, Safe, Overflow, Samples, ...) are matched rather than duplicated, and re-importing is idempotent.
/// Clip content is converted to the representation clipboard capture produces (see <see cref="ClipMate7FormatConverter" />).
/// Clips encrypted in ClipMate 7 are decrypted with whichever of the user's ClipMate 7 keys opens them (see
/// <see cref="ClipMate7Encryption" />) and immediately encrypted again with ClipMate's own encryption using that same
/// key, so they stay encrypted.
/// </remarks>
public class ClipMate7ImportService : IClipMate7ImportService
{
    private const int _batchSize = 200;

    // ClipMate 7 table names
    private const string _clipTable = "clip";
    private const string _clipDataTable = "clipdata";
    private const string _collectionTable = "coll";
    private const string _shortcutTable = "shortcut";
    private static readonly string[] _blobTables = ["blobtxt", "blobpng", "blobjpg", "blobblob"];

    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<ClipMate7ImportService> _logger;
    private readonly ISetupService _setupService;

    public ClipMate7ImportService(ISetupService setupService, IEncryptionService encryptionService, ILogger<ClipMate7ImportService> logger)
    {
        _setupService = setupService ?? throw new ArgumentNullException(nameof(setupService));
        _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsClipMate7Database(string folder) =>
        !string.IsNullOrWhiteSpace(folder) &&
        Directory.Exists(folder) &&
        DbisamTable.Exists(folder, _clipTable) &&
        DbisamTable.Exists(folder, _collectionTable);

    public Task<ClipMate7ImportPreview> AnalyzeAsync(string sourceFolder, CancellationToken cancellationToken = default)
    {
        EnsureSourceFolder(sourceFolder);

        return Task.Run(() =>
        {
            try
            {
                var warnings = new List<string>();
                int clipCount = 0, encryptedCount = 0;

                using (var clipTable = DbisamTable.Open(sourceFolder, _clipTable))
                {
                    foreach (var item in clipTable.ReadRecords())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (item.GetInt32("ID") is not > 0)
                            continue;

                        clipCount++;
                        if (item.GetBoolean("ENCRYPTED"))
                            encryptedCount++;
                    }
                }

                using var collectionTable = DbisamTable.Open(sourceFolder, _collectionTable);

                if (encryptedCount > 0)
                {
                    warnings.Add($"{encryptedCount} clip(s) are encrypted in ClipMate 7. Enter their ClipMate 7 encryption keys to " +
                                 "import them (they stay encrypted, with the same keys); clips no key opens are skipped.");
                }

                foreach (var item in _blobTables.Where(p => !DbisamTable.Exists(sourceFolder, p)))
                    warnings.Add($"Table '{item}' is missing; clip formats stored in it cannot be imported.");

                return new ClipMate7ImportPreview
                {
                    ClipCount = clipCount,
                    CollectionCount = collectionTable.ActiveRecordCount,
                    FormatCount = CountRecords(sourceFolder, _clipDataTable),
                    ShortcutCount = CountRecords(sourceFolder, _shortcutTable),
                    EncryptedClipCount = encryptedCount,
                    Warnings = warnings,
                };
            }
            catch (Exception ex) when (ex is DbisamFormatException or IOException)
            {
                throw new InvalidOperationException($"Unable to read the ClipMate 7 database: {ex.Message}", ex);
            }
        }, cancellationToken);
    }

    public Task<int> CountDecryptableClipsAsync(string sourceFolder, IReadOnlyList<string> encryptionKeys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(encryptionKeys);
        EnsureSourceFolder(sourceFolder);

        return Task.Run(() =>
        {
            try
            {
                using var keyRing = ClipMate7KeyRing.Create(encryptionKeys, out _);
                if (keyRing.IsEmpty)
                    return 0;

                using var formats = FormatIndex.Open(sourceFolder);
                using var clipTable = DbisamTable.Open(sourceFolder, _clipTable);

                var count = 0;
                foreach (var item in clipTable.ReadRecords())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (item.GetInt32("ID") is not > 0 || !item.GetBoolean("ENCRYPTED"))
                        continue;

                    var text = formats.Get(item.GetInt32("ID")!.Value).FirstOrDefault(p => p.Code == Formats.Text.Code && p.Blob is not null);
                    if (text.Blob is not null && keyRing.TryDecryptText(text.Blob.GetBlob("DATA"), item.GetInt32("CHECKSUM") ?? 0, out _, out _, out _))
                        count++;
                }

                return count;
            }
            catch (Exception ex) when (ex is DbisamFormatException or IOException)
            {
                throw new InvalidOperationException($"Unable to read the ClipMate 7 database: {ex.Message}", ex);
            }
        }, cancellationToken);
    }

    public async Task<ClipMate7ImportResult> ImportAsync(ClipMate7ImportOptions options,
        Action<ExportProgressMessage>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        EnsureSourceFolder(options.SourceFolder);

        if (string.IsNullOrWhiteSpace(options.TargetDatabasePath))
            throw new ArgumentException("Target database path is required.", nameof(options));

        var targetPath = Path.GetFullPath(options.TargetDatabasePath);

        if (options.CreateNewDatabase)
        {
            if (File.Exists(targetPath))
                throw new InvalidOperationException($"A database already exists at '{targetPath}'.");

            Report(progressCallback, "Creating database...");
            if (!await _setupService.CreateDatabaseAsync(targetPath, cancellationToken))
                throw new InvalidOperationException($"Failed to create database at '{targetPath}'.");

            await _setupService.SeedDefaultDataAsync(targetPath, cancellationToken);
        }
        else if (!File.Exists(targetPath))
            throw new FileNotFoundException("Target database not found.", targetPath);

        _logger.LogInformation("Importing ClipMate 7 database from {Source} into {Target}", options.SourceFolder, targetPath);

        try
        {
            using var keyRing = ClipMate7KeyRing.Create(options.EncryptionKeys, out var keyWarnings);
            var result = await Task.Run(() => ImportCoreAsync(options.SourceFolder, targetPath, keyRing, progressCallback, cancellationToken), cancellationToken);
            if (result.EncryptedClipsSkipped > 0)
                result.Warnings.InsertRange(0, keyWarnings);

            _logger.LogInformation(
                "ClipMate 7 import complete: {Clips} clips, {Collections} collections, {Shortcuts} shortcuts imported; " +
                "{EncryptedImported} of them kept encrypted; " +
                "{Duplicates} duplicate, {Encrypted} encrypted, {Unreadable} unreadable clips skipped",
                result.ClipsImported, result.CollectionsImported, result.ShortcutsImported, result.EncryptedClipsImported,
                result.DuplicateClipsSkipped, result.EncryptedClipsSkipped, result.UnreadableClipsSkipped);

            Report(progressCallback, "Import complete.", isComplete: true);
            return result;
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
                _logger.LogInformation("ClipMate 7 import cancelled");
            else
                _logger.LogError(ex, "ClipMate 7 import failed");

            if (options.CreateNewDatabase)
                DeleteDatabaseFile(targetPath);

            if (ex is DbisamFormatException or IOException)
                throw new InvalidOperationException($"Unable to read the ClipMate 7 database: {ex.Message}", ex);

            throw;
        }
    }

    private async Task<ClipMate7ImportResult> ImportCoreAsync(string sourceFolder,
        string targetPath,
        ClipMate7KeyRing keyRing,
        Action<ExportProgressMessage>? progressCallback,
        CancellationToken cancellationToken)
    {
        var result = new ClipMate7ImportResult();

        var optionsBuilder = new DbContextOptionsBuilder<ClipMateDbContext>();
        optionsBuilder.UseSqlite($"Data Source={targetPath}");
        await using var context = new ClipMateDbContext(optionsBuilder.Options);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // Collections
        Report(progressCallback, "Importing collections...");
        var existingCollections = (await context.Collections.AsNoTracking().ToListAsync(cancellationToken))
            .Select(p => new ExistingCollection(p.Id, p.ParentId, p.Title, p.Role,
                p.LmType != CollectionLmType.Virtual && p.ListType == CollectionListType.Normal))
            .ToList();

        var collectionMap = ImportCollections(sourceFolder, context, existingCollections, result);
        await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        collectionMap.FallbackId = existingCollections.FirstOrDefault(p => p is { Role: CollectionRole.Inbox, IsClipTarget: true })?.Id
                                   ?? existingCollections.FirstOrDefault(p => p.IsClipTarget)?.Id;

        Report(progressCallback, "Indexing clip formats...");
        using var formats = FormatIndex.Open(sourceFolder);

        // Clips
        var existingClipIds = (await context.Clips.AsNoTracking().Select(p => p.Id).ToListAsync(cancellationToken)).ToHashSet();
        var importedClipIds = new Dictionary<int, Guid>();

        using (var clipTable = DbisamTable.Open(sourceFolder, _clipTable))
        {
            var total = clipTable.ActiveRecordCount;
            var processed = 0;
            var pending = 0;

            foreach (var item in clipTable.ReadRecords())
            {
                cancellationToken.ThrowIfCancellationRequested();
                processed++;

                var clip = await TryAddClipAsync(item, context, formats, collectionMap, existingClipIds, keyRing, result,
                    cancellationToken);

                if (clip is not null)
                {
                    importedClipIds[item.GetInt32("ID")!.Value] = clip.Id;
                    existingClipIds.Add(clip.Id);
                    pending++;
                }

                if (pending >= _batchSize)
                {
                    await context.SaveChangesAsync(cancellationToken);
                    context.ChangeTracker.Clear();
                    pending = 0;
                }

                if (processed % _batchSize == 0 || processed == total)
                    Report(progressCallback, $"Imported {result.ClipsImported} of {total} clip(s)...", processed: processed, total: total);
            }

            await context.SaveChangesAsync(cancellationToken);
            context.ChangeTracker.Clear();
        }

        // Shortcuts (only for clips imported in this run; nicknames are unique)
        if (DbisamTable.Exists(sourceFolder, _shortcutTable))
        {
            Report(progressCallback, "Importing shortcuts...");
            var nicknames = (await context.Shortcuts.AsNoTracking().Select(p => p.Nickname).ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var importedGuids = importedClipIds.Values.ToHashSet();

            using var shortcutTable = DbisamTable.Open(sourceFolder, _shortcutTable);
            foreach (var item in shortcutTable.ReadRecords())
            {
                var nickname = item.GetString("NICKNAME");
                var clipGuid = item.GetGuid("CLIP_GUID")
                               ?? (item.GetInt32("CLIP_ID") is { } clipId && importedClipIds.TryGetValue(clipId, out var mapped)
                                   ? mapped
                                   : null);

                if (string.IsNullOrWhiteSpace(nickname) || clipGuid is not { } guid || !importedGuids.Contains(guid) || !nicknames.Add(nickname))
                    continue;

                context.Shortcuts.Add(new Shortcut
                {
                    Id = Guid.NewGuid(),
                    ClipId = guid,
                    ClipGuid = guid,
                    Nickname = nickname,
                });

                result.ShortcutsImported++;
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// Adds collections that do not already exist in the target, parents first. A ClipMate 7 collection is matched to
    /// an existing one by GUID, then by title under the same parent (e.g. "InBox" and "Safe" created with different
    /// GUIDs). Virtual (SQL-based) collections are skipped: their queries are written in DBISAM SQL.
    /// </summary>
    /// <param name="existing">Collections already in the target; imported collections are appended.</param>
    private static CollectionMap ImportCollections(string sourceFolder,
        ClipMateDbContext context,
        List<ExistingCollection> existing,
        ClipMate7ImportResult result)
    {
        using var table = DbisamTable.Open(sourceFolder, _collectionTable);
        var rows = table.ReadRecords()
            .Where(p => p.GetInt32("ID") is > 0)
            .Select(p => (Record: p, Id: p.GetInt32("ID")!.Value, Guid: p.GetGuid("COLL_GUID") ?? Guid.NewGuid()))
            .ToList();

        var guidsByLegacyId = rows.ToDictionary(p => p.Id, p => p.Guid);
        var rowsByGuid = rows.GroupBy(p => p.Guid).ToDictionary(p => p.Key, p => p.First().Record);
        var resolved = new Dictionary<Guid, Guid?>();
        var inProgress = new HashSet<Guid>();
        var map = new CollectionMap();

        foreach (var item in rows)
        {
            if (Resolve(item.Guid) is { } target && existing.Any(p => p.Id == target && p.IsClipTarget))
            {
                map.ByGuid[item.Guid] = target;
                map.ByLegacyId[item.Id] = target;
            }
        }

        return map;

        // Returns the target collection for a ClipMate 7 collection GUID, or null if it is skipped
        Guid? Resolve(Guid guid)
        {
            if (resolved.TryGetValue(guid, out var done))
                return done;

            if (!rowsByGuid.TryGetValue(guid, out var record))
                return existing.Any(p => p.Id == guid) ? guid : null;

            if (!inProgress.Add(guid))
                return null; // Cycle in the parent chain

            Guid? target;
            if (IsVirtual(record))
            {
                result.VirtualCollectionsSkipped++;
                target = null;
            }
            else if (existing.Any(p => p.Id == guid))
                target = guid;
            else
            {
                var parentGuid = record.GetGuid("PARENT_GUID") is { } pg && pg != Guid.Empty
                    ? pg
                    : guidsByLegacyId.TryGetValue(record.GetInt32("PARENT_ID") ?? 0, out var mapped)
                        ? mapped
                        : (Guid?)null;

                var parentId = parentGuid is { } parent && parent != guid
                    ? Resolve(parent)
                    : null;

                var title = record.GetString("TITLE") is { Length: > 0 } t ? t : "Imported";
                var match = existing.FirstOrDefault(p =>
                    p.IsClipTarget && p.ParentId == parentId && string.Equals(p.Title, title, StringComparison.OrdinalIgnoreCase));

                if (match is not null)
                    target = match.Id;
                else
                {
                    var lastUpdate = record.GetDateTime("LAST_UPDATE_TIME");
                    context.Collections.Add(new Collection
                    {
                        Id = guid,
                        ParentId = parentId,
                        ParentGuid = parentId ?? Guid.Empty,
                        Title = title,
                        LmType = record.GetInt32("LMTYPE") == (int)CollectionLmType.Folder
                            ? CollectionLmType.Folder
                            : CollectionLmType.Normal,
                        ListType = CollectionListType.Normal,
                        SortKey = record.GetInt32("SORTKEY") ?? 0,
                        IlIndex = record.GetInt32("ILINDEX") ?? 0,
                        RetentionLimit = record.GetInt32("RETENTIONLIMIT") ?? 0,
                        NewClipsGo = record.GetInt32("NEWCLIPSGO") ?? 0,
                        AcceptNewClips = record.GetBoolean("ACCEPTNEWCLIPS"),
                        ReadOnly = record.GetBoolean("READONLY"),
                        AcceptDuplicates = record.GetBoolean("ACCEPTDUPLICATES"),
                        SortColumn = record.GetInt32("SORTCOLUMN") ?? 0,
                        SortAscending = record.GetBoolean("SORTASCENDING"),
                        Favorite = record.GetBoolean("FAVORITE"),
                        LastUserId = record.GetInt32("LASTUSER_ID"),
                        LastUpdateTime = lastUpdate,
                        LastKnownCount = record.GetInt32("LAST_KNOWN_COUNT"),
                        CreatedAt = lastUpdate ?? DateTime.Now,
                        Role = CollectionRole.None,
                    });

                    existing.Add(new ExistingCollection(guid, parentId, title, CollectionRole.None, true));
                    result.CollectionsImported++;
                    target = guid;
                }
            }

            inProgress.Remove(guid);
            resolved[guid] = target;
            return target;
        }
    }

    private static bool IsVirtual(DbisamRecord collection) =>
        collection.GetInt32("LMTYPE") == (int)CollectionLmType.Virtual ||
        (collection.GetInt32("LISTTYPE") ?? 0) != (int)CollectionListType.Normal;

    private async Task<Clip?> TryAddClipAsync(DbisamRecord record,
        ClipMateDbContext context,
        FormatIndex formatIndex,
        CollectionMap collectionMap,
        HashSet<Guid> existingClipIds,
        ClipMate7KeyRing keyRing,
        ClipMate7ImportResult result,
        CancellationToken cancellationToken)
    {
        var legacyId = record.GetInt32("ID") ?? 0;
        if (legacyId <= 0)
            return null;

        var clipId = record.GetGuid("CLIP_GUID") ?? Guid.NewGuid();
        if (existingClipIds.Contains(clipId))
        {
            result.DuplicateClipsSkipped++;
            return null;
        }

        var encrypted = record.GetBoolean("ENCRYPTED");
        if (encrypted && keyRing.IsEmpty)
        {
            result.EncryptedClipsSkipped++;
            return null;
        }

        // ClipMate 7 encrypts only the TEXT format; the key and salt that open it also decrypt the title
        byte[]? salt = null;
        ClipMate7KeyRing.Key? key = null;
        var formats = new List<ClipMate7Format>();
        foreach (var item in formatIndex.Get(legacyId))
        {
            if (item.Blob is not { } blob)
                continue;

            try
            {
                var data = blob.GetBlob("DATA");
                if (encrypted && item.Code == Formats.Text.Code)
                {
                    if (!keyRing.TryDecryptText(data, record.GetInt32("CHECKSUM") ?? 0, out data, out salt, out key))
                    {
                        result.EncryptedClipsSkipped++;
                        return null;
                    }
                }

                formats.Add(new ClipMate7Format(item.Name, item.Code, data));
            }
            catch (DbisamFormatException ex)
            {
                _logger.LogWarning(ex, "Unreadable data for ClipMate 7 clip {ClipId}, format {Format}", legacyId, item.Name);
                result.Warnings.Add($"Clip {legacyId} ({item.Name}): {ex.Message}");
            }
        }

        if (encrypted && key is null)
        {
            result.EncryptedClipsSkipped++;
            return null;
        }

        var customTitle = record.GetBoolean("CUSTOMTITLE");
        var title = record.GetString("TITLE");
        if (encrypted && !customTitle && title is not null)
            title = key!.Decryptor.TryDecryptTitle(title, salt!);

        var timestamp = record.GetDateTime("TIMESTAMP");
        var deleted = record.GetBoolean("DEL");
        var clip = new Clip
        {
            Id = clipId,
            CollectionId = collectionMap.Resolve(record.GetGuid("COLL_GUID"), record.GetInt32("COLLECTION_ID")),
            UserId = record.GetInt32("USER_ID"),
            Title = title,
            Creator = record.GetString("CREATOR"),
            CapturedAt = timestamp.HasValue
                ? new DateTimeOffset(timestamp.Value)
                : DateTimeOffset.Now,
            SortKey = record.GetInt32("SORTKEY") ?? 0,
            SourceUrl = record.GetString("SOURCEURL"),
            CustomTitle = customTitle,
            Locale = record.GetInt32("LOCALE") ?? 0,
            WrapCheck = record.GetBoolean("WRAPCHECK"),
            Icons = record.GetInt32("ICONS") ?? 0,
            Del = deleted,
            DelDate = deleted && record.GetDateTime("DELDATE") is { } delDate
                ? new DateTimeOffset(delDate)
                : null,
            Size = record.GetInt32("SIZE") ?? 0,
            ViewTab = record.GetInt32("VIEWTAB") ?? 0,
            Macro = record.GetBoolean("MACRO"),
            LastModified = record.GetDateTime("LASTMODIFIED") is { } lastModified
                ? new DateTimeOffset(lastModified)
                : null,
        };

        result.UnsupportedFormatsSkipped += ClipMate7FormatConverter.Apply(clip, formats);

        if (!ClipMate7FormatConverter.HasContent(clip))
        {
            result.UnreadableClipsSkipped++;
            return null;
        }

        context.Clips.Add(clip);
        await ClipContentStorage.AddContentAsync(context, clip, cancellationToken);

        if (encrypted)
        {
            // An encrypted title that could not be decrypted gets a title from the text, as a captured clip does
            if (string.IsNullOrWhiteSpace(clip.Title) && !customTitle)
                clip.Title = TitleFromText(clip.TextContent);

            await ClipContentEncryption.EncryptAsync(_encryptionService, key!.EncryptionKey, clip,
                context.BlobTxt.Local.Where(p => p.ClipId == clip.Id),
                context.BlobJpg.Local.Where(p => p.ClipId == clip.Id),
                context.BlobPng.Local.Where(p => p.ClipId == clip.Id),
                context.BlobBlob.Local.Where(p => p.ClipId == clip.Id));

            ClearContent(clip);
            result.EncryptedClipsImported++;
        }

        result.ClipsImported++;

        return clip;
    }

    /// <summary>
    /// The first non-empty line of the text, at most 60 characters (the TITLE field width).
    /// </summary>
    private static string? TitleFromText(string? text)
    {
        var line = text?.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return line is { Length: > 60 }
            ? string.Concat(line.AsSpan(0, 57), "...")
            : line;
    }

    /// <summary>
    /// Drops the plain-text transient content of a clip that has been encrypted.
    /// </summary>
    private static void ClearContent(Clip clip)
    {
        clip.TextContent = null;
        clip.RtfContent = null;
        clip.HtmlContent = null;
        clip.ImageData = null;
        clip.FilePathsJson = null;
    }

    private static int CountRecords(string folder, string tableName)
    {
        if (!DbisamTable.Exists(folder, tableName))
            return 0;

        using var table = DbisamTable.Open(folder, tableName);
        return table.ActiveRecordCount;
    }

    private void EnsureSourceFolder(string sourceFolder)
    {
        if (!IsClipMate7Database(sourceFolder))
            throw new InvalidOperationException($"'{sourceFolder}' does not contain a ClipMate 7 database (clip.dat and coll.dat were not found).");
    }

    private void DeleteDatabaseFile(string path)
    {
        try
        {
            SqliteConnection.ClearAllPools();
            foreach (var item in new[] { path, path + "-wal", path + "-shm", path + "-journal" })
            {
                if (File.Exists(item))
                    File.Delete(item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove incomplete database {Path}", path);
        }
    }

    private static void Report(Action<ExportProgressMessage>? progressCallback,
        string message,
        bool isComplete = false,
        int processed = 0,
        int total = 0) =>
        progressCallback?.Invoke(new ExportProgressMessage
        {
            Message = message,
            IsComplete = isComplete,
            Successful = true,
            ProcessedCount = processed,
            TotalCount = total,
        });

    /// <summary>
    /// The CLIPDATA rows (formats) of each ClipMate 7 clip, with the BLOB row holding each format's data. BLOB contents
    /// are read on demand, so the BLOB tables stay open until disposed.
    /// </summary>
    private sealed class FormatIndex : IDisposable
    {
        private readonly List<DbisamTable> _openTables = [];
        private readonly Dictionary<int, List<(int Id, string Name, int Code, DbisamRecord? Blob)>> _formatsByClipId = [];

        private FormatIndex()
        {
        }

        public static FormatIndex Open(string sourceFolder)
        {
            var index = new FormatIndex();
            try
            {
                var blobsByClipDataId = new Dictionary<int, DbisamRecord>();
                foreach (var item in _blobTables.Where(p => DbisamTable.Exists(sourceFolder, p)))
                {
                    var table = DbisamTable.Open(sourceFolder, item);
                    index._openTables.Add(table);
                    foreach (var record in table.ReadRecords())
                    {
                        if (record.GetInt32("CLIPDATA_ID") is { } clipDataId && clipDataId > 0)
                            blobsByClipDataId.TryAdd(clipDataId, record);
                    }
                }

                if (!DbisamTable.Exists(sourceFolder, _clipDataTable))
                    return index;

                using var clipDataTable = DbisamTable.Open(sourceFolder, _clipDataTable);
                foreach (var item in clipDataTable.ReadRecords())
                {
                    var id = item.GetInt32("ID") ?? 0;
                    var clipId = item.GetInt32("CLIP_ID") ?? 0;
                    if (id <= 0 || clipId <= 0)
                        continue;

                    if (!index._formatsByClipId.TryGetValue(clipId, out var list))
                        index._formatsByClipId[clipId] = list = [];

                    list.Add((id, item.GetString("FORMAT_NAME") ?? string.Empty, item.GetInt32("FORMAT") ?? 0,
                        blobsByClipDataId.GetValueOrDefault(id)));
                }

                return index;
            }
            catch
            {
                index.Dispose();
                throw;
            }
        }

        /// <summary>
        /// The formats of a clip, by its ClipMate 7 ID. Blob is null when the format's data is missing.
        /// </summary>
        public IReadOnlyList<(int Id, string Name, int Code, DbisamRecord? Blob)> Get(int legacyClipId) =>
            _formatsByClipId.GetValueOrDefault(legacyClipId) ?? [];

        public void Dispose()
        {
            foreach (var item in _openTables)
                item.Dispose();
        }
    }

    /// <summary>
    /// The ClipMate 7 keys the user entered. ClipMate 7 asks for a key each time clips are encrypted, so each
    /// encrypted clip is tried with every key; the key that opens it also re-encrypts it with ClipMate's encryption.
    /// </summary>
    private sealed class ClipMate7KeyRing : IDisposable
    {
        private readonly List<Key> _keys;

        private ClipMate7KeyRing(List<Key> keys) => _keys = keys;

        public bool IsEmpty => _keys.Count == 0;

        /// <summary>
        /// Builds the key ring from the keys entered, ignoring blanks and duplicates.
        /// </summary>
        /// <param name="warnings">Keys that cannot be used: encrypted clips must stay encrypted, so a key ClipMate's
        /// encryption does not accept cannot import the clips it opens.</param>
        public static ClipMate7KeyRing Create(IEnumerable<string> passphrases, out List<string> warnings)
        {
            warnings = [];
            var keys = new List<Key>();
            foreach (var item in passphrases.Where(p => !string.IsNullOrEmpty(p)).Distinct(StringComparer.Ordinal))
            {
                try
                {
                    keys.Add(new Key(new ClipMate7Encryption(item), EncryptionKey.FromPassphrase(item)));
                }
                catch (ArgumentException ex)
                {
                    warnings.Add($"A ClipMate 7 key was not used because ClipMate cannot encrypt with it: {ex.Message.Split(" (Parameter")[0]}");
                }
            }

            return new ClipMate7KeyRing(keys);
        }

        /// <summary>
        /// Decrypts an encrypted clip's TEXT with the key that opens it.
        /// </summary>
        /// <remarks>
        /// With a checksum only the right key opens the clip. Without one, a wrong key can still produce text-like
        /// bytes, so of the keys that pass, the one giving the fewest non-ASCII characters wins.
        /// </remarks>
        public bool TryDecryptText(byte[] stored, int checksum, out byte[] plainText, out byte[] salt, out Key? key)
        {
            plainText = [];
            salt = [];
            key = null;
            var bestScore = int.MaxValue;

            foreach (var item in _keys)
            {
                if (!item.Decryptor.TryDecryptText(stored, checksum, out var plain, out var itemSalt))
                    continue;

                var score = checksum != 0 ? 0 : plain.Count(p => p >= 0x80);
                if (score >= bestScore)
                    continue;

                (plainText, salt, key, bestScore) = (plain, itemSalt, item, score);
                if (score == 0)
                    break;
            }

            return key is not null;
        }

        public void Dispose()
        {
            foreach (var item in _keys)
                item.EncryptionKey.Dispose();
        }

        /// <summary>
        /// One key: <see cref="Decryptor" /> opens ClipMate 7 clips; <see cref="EncryptionKey" /> encrypts them again.
        /// </summary>
        public sealed record Key(ClipMate7Encryption Decryptor, EncryptionKey EncryptionKey);
    }

    private sealed record ExistingCollection(Guid Id, Guid? ParentId, string Title, CollectionRole Role, bool IsClipTarget);

    /// <summary>
    /// Maps ClipMate 7 collections (by GUID or legacy integer ID) to the target collections clips are stored in.
    /// </summary>
    private sealed class CollectionMap
    {
        public Dictionary<Guid, Guid> ByGuid { get; } = [];

        public Dictionary<int, Guid> ByLegacyId { get; } = [];

        /// <summary>
        /// Collection for clips whose ClipMate 7 collection was skipped or missing (the Inbox).
        /// </summary>
        public Guid? FallbackId { get; set; }

        public Guid? Resolve(Guid? collectionGuid, int? legacyCollectionId)
        {
            if (collectionGuid is { } guid && ByGuid.TryGetValue(guid, out var byGuid))
                return byGuid;

            if (legacyCollectionId is { } id && ByLegacyId.TryGetValue(id, out var byId))
                return byId;

            return FallbackId;
        }
    }
}
