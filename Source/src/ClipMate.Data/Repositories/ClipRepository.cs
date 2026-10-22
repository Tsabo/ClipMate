using System.Data;
using System.Text;
using System.Text.Json;
using ClipMate.Core.Models;
using ClipMate.Core.Models.Search;
using ClipMate.Core.Repositories;
using ClipMate.Data.Dapper;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Repositories;

/// <summary>
/// Entity Framework Core implementation of the clip repository.
/// Handles ClipMate storage architecture: Clip metadata + ClipData formats + BLOB content.
/// </summary>
public class ClipRepository : IClipRepository
{
    private static bool _dapperConfigured;
    private readonly ClipMateDbContext _context;
    private readonly ILogger<ClipRepository> _logger;

    public ClipRepository(ClipMateDbContext context, ILogger<ClipRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Configure Dapper once for GUID and DateTimeOffset handling
        if (_dapperConfigured)
            return;

        SqlMapper.AddTypeHandler(new GuidTypeHandler());
        SqlMapper.AddTypeHandler(new DateTimeOffsetTypeHandler());
        SqlMapper.RemoveTypeMap(typeof(Guid));
        SqlMapper.AddTypeMap(typeof(Guid), DbType.String);
        _dapperConfigured = true;
    }

    public async Task<Clip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var clip = await _context.Clips
            .Where(p => !p.Del) // Exclude soft-deleted clips
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (clip != null)
            await LoadFormatFlagsAsync([clip], cancellationToken);

        return clip;
    }

    public async Task<IReadOnlyList<Clip>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        // Fetch clips without ordering (SQLite doesn't support DateTimeOffset in ORDER BY)
        // Single query: fetch clips with their format flags in one database round-trip
        var clipsWithFormats = await _context.Clips
            .Where(p => !p.Del) // Exclude soft-deleted clips
            .GroupJoin(
                _context.ClipData,
                clip => clip.Id,
                clipData => clipData.ClipId,
                (clip, clipDataGroup) => new
                {
                    Clip = clip,
                    HasText = clipDataGroup.Any(p => p.Format == Formats.Text.Code || p.Format == Formats.UnicodeText.Code || p.FormatName == Formats.Text.Name || p.FormatName == Formats.UnicodeText.Name),
                    HasRtf = clipDataGroup.Any(p => p.FormatName == Formats.RichText.Name || p.Format == Formats.RichText.Code),
                    HasHtml = clipDataGroup.Any(p => p.FormatName == Formats.Html.Name || p.Format == Formats.Html.Code || p.Format == Formats.HtmlAlt.Code),
                    HasBitmap = clipDataGroup.Any(p => p.Format == Formats.Bitmap.Code || p.Format == Formats.Dib.Code || p.FormatName == Formats.Bitmap.Name || p.FormatName == Formats.Dib.Name),
                    HasFiles = clipDataGroup.Any(p => p.Format == Formats.HDrop.Code || p.FormatName == Formats.HDrop.Name),
                    FormatNames = string.Join(", ", clipDataGroup.Select(cd => cd.FormatName)),
                })
            .ToListAsync(cancellationToken);

        // Sort in memory and take the requested count (by manual SortKey, which also correlates with capture order)
        clipsWithFormats = clipsWithFormats
            .OrderByDescending(p => p.Clip.SortKey)
            .Take(count)
            .ToList();

        // Apply format flags to clips in memory
        foreach (var item in clipsWithFormats)
        {
            var clip = item.Clip;

            // Set format flags directly
            clip.HasText = item.HasText;
            clip.HasRtf = item.HasRtf;
            clip.HasHtml = item.HasHtml;
            clip.HasBitmap = item.HasBitmap;
            clip.HasFiles = item.HasFiles;

            // Pre-compute and cache the icon string
            var icons = new List<string>();
            if (clip.Macro)
                icons.Add("⌨");

            if (item.HasBitmap)
                icons.Add("🖼");

            if (item.HasRtf)
                icons.Add("🅰");

            if (item.HasHtml)
                icons.Add("🌐");

            if (item.HasFiles)
                icons.Add("📁");

            if (item.HasText)
                icons.Add("📄");

            // Add lock icon prefix for encrypted clips
            var lockIcon = clip.Encrypted
                ? clip.IsDecrypted
                    ? "🔓"
                    : "🔒"
                : string.Empty;

            clip.IconGlyph = icons.Count > 0
                ? lockIcon + string.Join("", icons)
                : lockIcon + "❓";

            // Fallback for clips with no ClipData
            if (!item.HasText && !item.HasRtf && !item.HasHtml && !item.HasBitmap && !item.HasFiles)
            {
                switch (clip.Type)
                {
                    case ClipType.Text:
                        clip.HasText = true;
                        clip.IconGlyph = "📄";
                        break;
                    case ClipType.Image:
                        clip.HasBitmap = true;
                        clip.IconGlyph = "🖼";
                        break;
                    case ClipType.Files:
                        clip.HasFiles = true;
                        clip.IconGlyph = "📁";
                        break;
                    default:
                        clip.IconGlyph = "❓";
                        break;
                }
            }
        }

        return clipsWithFormats
            .Select(p => p.Clip)
            .ToList();
    }

    public async Task<IReadOnlyList<Clip>> GetByCollectionAsync(Guid collectionId, CancellationToken cancellationToken = default)
    {
        // Fetch clips without ordering (SQLite doesn't support DateTimeOffset in ORDER BY)
        var clips = await _context.Clips
            .Where(p => p.CollectionId == collectionId && !p.Del)
            .ToListAsync(cancellationToken);

        // Sort in memory after fetching (by manual SortKey, which also correlates with capture order)
        clips = clips.OrderByDescending(p => p.SortKey).ToList();

        // Load format flags from ClipData table (not actual content)
        await LoadFormatFlagsAsync(clips, cancellationToken);

        return clips;
    }

    public async Task<IReadOnlyList<Clip>> GetByFolderAsync(Guid folderId, CancellationToken cancellationToken = default)
    {
        // Fetch clips without ordering (SQLite doesn't support DateTimeOffset in ORDER BY)
        var clips = await _context.Clips
            .Where(p => p.FolderId == folderId && !p.Del)
            .ToListAsync(cancellationToken);

        // Sort in memory after fetching (by manual SortKey, which also correlates with capture order)
        clips = clips.OrderByDescending(p => p.SortKey).ToList();

        // Load format flags from ClipData table (not actual content)
        await LoadFormatFlagsAsync(clips, cancellationToken);

        return clips;
    }

    /// <inheritdoc />
    public Task<ClipPage> GetPageByCollectionAsync(Guid collectionId, int offset, int count, CancellationToken cancellationToken = default) =>
        GetPageAsync(_context.Clips.Where(p => p.CollectionId == collectionId && !p.Del), offset, count, cancellationToken);

    /// <inheritdoc />
    public Task<ClipPage> GetPageByFolderAsync(Guid folderId, int offset, int count, CancellationToken cancellationToken = default) =>
        GetPageAsync(_context.Clips.Where(p => p.FolderId == folderId && !p.Del), offset, count, cancellationToken);

    /// <summary>
    /// Pages <paramref name="query" /> in the database by SortKey (newest first), loading format flags for the page only,
    /// so the first page of a large collection is quick.
    /// </summary>
    private async Task<ClipPage> GetPageAsync(IQueryable<Clip> query, int offset, int count, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var total = await query.CountAsync(cancellationToken);
        var clips = await query
            .OrderByDescending(p => p.SortKey)
            .ThenBy(p => p.Id)
            .Skip(offset)
            .Take(count)
            .ToListAsync(cancellationToken);

        await LoadFormatFlagsAsync(clips, cancellationToken);
        return new ClipPage(clips, offset, total);
    }

    public async Task<IReadOnlyList<Clip>> GetFavoritesAsync(CancellationToken cancellationToken = default)
    {
        // Fetch clips without ordering (SQLite doesn't support DateTimeOffset in ORDER BY)
        var clips = await _context.Clips
            .Where(p => p.IsFavorite && !p.Del)
            .ToListAsync(cancellationToken);

        // Sort in memory after fetching (by manual SortKey, which also correlates with capture order)
        clips = clips.OrderByDescending(p => p.SortKey).ToList();

        // Load format flags from ClipData table (not actual content)
        await LoadFormatFlagsAsync(clips, cancellationToken);

        return clips;
    }

    public async Task<IReadOnlyList<Clip>> GetDeletedAsync(CancellationToken cancellationToken = default)
    {
        // Fetch deleted clips (where Del=true) for the Trashcan virtual collection
        var clips = await _context.Clips
            .Where(p => p.Del)
            .ToListAsync(cancellationToken);

        // Sort by deletion date (most recently deleted first)
        clips = clips.OrderByDescending(p => p.DelDate ?? p.CapturedAt).ToList();

        // Load format flags from ClipData table (not actual content)
        await LoadFormatFlagsAsync(clips, cancellationToken);

        return clips;
    }

    public async Task<Clip?> GetByContentHashAsync(string contentHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentHash))
            return null;

        var clip = await _context.Clips
            .FirstOrDefaultAsync(p => p.ContentHash == contentHash, cancellationToken);

        if (clip != null)
            await LoadFormatFlagsAsync([clip], cancellationToken);

        return clip;
    }

    public async Task<IReadOnlyList<Clip>> SearchAsync(string searchText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return new List<Clip>();

        // Fetch clips without ordering (SQLite doesn't support DateTimeOffset in ORDER BY)
        var clips = await _context.Clips
            .Where(p => !p.Del && EF.Functions.Like(p.TextContent ?? "", $"%{searchText}%"))
            .ToListAsync(cancellationToken);

        // Sort in memory after fetching
        clips = clips.OrderByDescending(p => p.CapturedAt).ToList();

        // Load format flags from ClipData table (not actual content)
        await LoadFormatFlagsAsync(clips, cancellationToken);

        return clips;
    }

    public async Task<Clip> CreateAsync(Clip clip, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clip);

        // Generate auto-increment SortKey for ClipMate 7.5 compatibility
        // SortKey = (count + 1) * 100 to allow manual re-ordering between clips
        var clipCount = await _context.Clips.CountAsync(cancellationToken);
        clip.SortKey = (clipCount + 1) * 100;

        // Set denormalized GUIDs for ClipMate 7.5 compatibility
        // CLIP_GUID = Clip.Id (already set)
        // COLL_GUID = CollectionId (denormalized for performance)
        if (clip.CollectionId != Guid.Empty)
        {
            // Store collection GUID in a way that matches ClipMate's COLL_GUID field
            // This would require adding a COLL_GUID navigation property, but for now
            // we'll rely on CollectionId FK
        }

        // Add the clip metadata
        _context.Clips.Add(clip);

        // Create ClipData entries and store in BLOB tables
        await ClipContentStorage.AddContentAsync(_context, clip, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        // IMPORTANT: Load format flags to populate cached properties for UI display
        // This ensures new clips show checkboxes immediately without requiring a reload
        await LoadFormatFlagsAsync([clip], cancellationToken);

        return clip;
    }

    public async Task<bool> UpdateAsync(Clip clip, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clip);

        // Don't call Update if entity is already tracked - EF will auto-detect changes
        var entry = _context.Entry(clip);
        if (entry.State == EntityState.Detached)
            _context.Clips.Update(clip);

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var clip = await _context.Clips.FindAsync([id], cancellationToken);

        if (clip == null)
            return false;

        // Soft delete (ClipMate style)
        clip.Del = true;
        clip.DelDate = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        // Hard delete BLOB data to save space
        await DeleteClipBlobsAsync(id, cancellationToken);

        return true;
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        // Fetch all non-favorite clips without date filtering (SQLite doesn't support DateTimeOffset comparisons well)
        var allClips = await _context.Clips
            .Where(p => !p.IsFavorite)
            .ToListAsync(cancellationToken);

        // Filter by date in memory
        var clipsToDelete = allClips
            .Where(p => p.CapturedAt.DateTime < cutoffDate)
            .ToList();

        foreach (var item in clipsToDelete)
        {
            item.Del = true;
            item.DelDate = DateTimeOffset.Now;
            await DeleteClipBlobsAsync(item.Id, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return clipsToDelete.Count;
    }

    /// <inheritdoc />
    public async Task<bool> ConvertFilePointerToTextAsync(Guid clipId, CancellationToken cancellationToken = default)
    {
        var clip = await _context.Clips.FirstOrDefaultAsync(p => p.Id == clipId && !p.Del, cancellationToken);
        if (clip == null || clip.Type != ClipType.Files)
            return false;

        var clipDataEntries = await _context.ClipData
            .Where(p => p.ClipId == clipId)
            .ToListAsync(cancellationToken);

        var hDropEntry = clipDataEntries.FirstOrDefault(p => p.Format == Formats.HDrop.Code || p.FormatName == Formats.HDrop.Name);
        var textEntry = clipDataEntries.FirstOrDefault(p => p.Format == Formats.UnicodeText.Code || p.Format == Formats.Text.Code
                                                                                                 || p.FormatName == Formats.UnicodeText.Name || p.FormatName == Formats.Text.Name);

        if (textEntry == null)
        {
            // No text blob captured yet (e.g. AutoExpandHdropFilePointers was off) - derive it from the HDROP file list.
            if (hDropEntry == null)
                return false;

            var blobBlob = await _context.BlobBlob.FirstOrDefaultAsync(p => p.ClipDataId == hDropEntry.Id, cancellationToken);
            if (blobBlob == null)
                return false;

            var filePathsJson = Encoding.UTF8.GetString(blobBlob.Data);
            var filePaths = JsonSerializer.Deserialize<List<string>>(filePathsJson) ?? [];
            var text = string.Join(Environment.NewLine, filePaths);

            var newClipData = new ClipData
            {
                Id = Guid.NewGuid(),
                ClipId = clipId,
                FormatName = Formats.UnicodeText.Name,
                Format = Formats.UnicodeText.Code,
                Size = text.Length * 2,
                StorageType = 1, // TEXT
            };

            _context.ClipData.Add(newClipData);
            _context.BlobTxt.Add(new BlobTxt
            {
                Id = Guid.NewGuid(),
                ClipDataId = newClipData.Id,
                ClipId = clipId,
                Data = text,
            });
        }

        // Remove the HDROP entry and the drop effect that went with it (each cascades to its BlobBlob row).
        if (hDropEntry != null)
            _context.ClipData.Remove(hDropEntry);

        _context.ClipData.RemoveRange(clipDataEntries.Where(p => p.FormatName == Formats.PreferredDropEffect.Name));

        clip.Type = ClipType.Text;

        await _context.SaveChangesAsync(cancellationToken);

        // Refresh cached format flags (HasFiles/HasText/IconGlyph) to reflect the new type immediately.
        await LoadFormatFlagsAsync([clip], cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> MoveClipSortOrderAsync(Guid clipId, bool toTop, CancellationToken cancellationToken = default)
    {
        var clip = await _context.Clips.FirstOrDefaultAsync(p => p.Id == clipId && !p.Del, cancellationToken);
        if (clip == null)
            return false;

        var siblingKeys = await _context.Clips
            .Where(p => p.CollectionId == clip.CollectionId && !p.Del && p.Id != clipId)
            .Select(p => p.SortKey)
            .ToListAsync(cancellationToken);

        if (siblingKeys.Count == 0)
            return true; // Nothing to reorder against.

        // SortKey is displayed descending (matches insertion/CapturedAt order), so "top" is the max.
        clip.SortKey = toTop
            ? siblingKeys.Max() + 100
            : siblingKeys.Min() - 100;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<ClipData>> GetClipFormatsAsync(Guid clipId, CancellationToken cancellationToken = default)
    {
        return await _context.ClipData
            .Where(p => p.ClipId == clipId)
            .OrderBy(p => p.FormatName)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Clip>> GetClipsInCollectionAsync(Guid collectionId) => GetByCollectionAsync(collectionId);

    /// <inheritdoc />
    public async Task MoveClipsToCollectionAsync(IEnumerable<Guid> clipIds, Guid targetCollectionId)
    {
        var ids = clipIds.ToList();
        if (ids.Count == 0)
            return;

        var clips = await _context.Clips
            .Where(p => ids.Contains(p.Id))
            .ToListAsync();

        foreach (var item in clips)
            item.CollectionId = targetCollectionId;

        await _context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task DeleteClipsAsync(IEnumerable<Guid> clipIds)
    {
        var ids = clipIds.ToList();
        if (ids.Count == 0)
            return;

        foreach (var item in ids)
            await DeleteAsync(item);
    }

    public async Task SoftDeleteClipsAsync(IEnumerable<Guid> clipIds)
    {
        var ids = clipIds.ToList();
        if (ids.Count == 0)
            return;

        var clips = await _context.Clips
            .Where(p => ids.Contains(p.Id))
            .ToListAsync();

        foreach (var item in clips)
        {
            item.Del = true;
            item.DelDate = DateTimeOffset.Now;
        }

        await _context.SaveChangesAsync();
    }

    public async Task RestoreClipsAsync(IEnumerable<Guid> clipIds, Guid targetCollectionId)
    {
        var ids = clipIds.ToList();
        if (ids.Count == 0)
            return;

        var clips = await _context.Clips
            .Where(p => ids.Contains(p.Id))
            .ToListAsync();

        foreach (var item in clips)
        {
            item.Del = false;
            item.DelDate = null;
            item.CollectionId = targetCollectionId;
        }

        await _context.SaveChangesAsync();
    }

    public Task<IReadOnlyList<Clip>> ExecuteSqlQueryAsync(string sqlQuery, CancellationToken cancellationToken = default) =>
        QueryClipsAsync(sqlQuery, null, cancellationToken);

    public Task<IReadOnlyList<Clip>> ExecuteSqlQueryAsync(string sqlQuery, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var dynamicParameters = new DynamicParameters();
        foreach (var item in parameters)
            dynamicParameters.Add(item.Key, item.Value);

        return QueryClipsAsync(sqlQuery, dynamicParameters, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SearchMatches> ExecuteSearchQueryAsync(string sqlQuery, IReadOnlyDictionary<string, object?> parameters, int? maxResults,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sqlQuery);
        ArgumentNullException.ThrowIfNull(parameters);
        if (maxResults is < 0)
            throw new ArgumentOutOfRangeException(nameof(maxResults));

        cancellationToken.ThrowIfCancellationRequested();

        // Parameter values are deliberately not logged: they can contain clip search text.
        _logger.LogInformation("Executing search SQL query: {SqlQuery}", sqlQuery);

        var search = sqlQuery.Trim().TrimEnd(';');
        var dynamicParameters = new DynamicParameters();
        foreach (var item in parameters)
            dynamicParameters.Add(item.Key, item.Value);

        var connection = _context.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened)
            await _context.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            // Stops a long query inside SQLite itself, not just between rows
            await using var interrupt = cancellationToken.Register(() =>
            {
                if (connection is SqliteConnection { Handle: { } handle })
                    SQLitePCL.raw.sqlite3_interrupt(handle);
            });

            var (clips, total) = await QueryPageAsync(connection, search, dynamicParameters, 0, maxResults, cancellationToken);
            return new SearchMatches(clips, total);
        }
        catch (SqliteException ex) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("The search was cancelled.", ex, cancellationToken);
        }
        finally
        {
            if (opened)
                await _context.Database.CloseConnectionAsync();
        }
    }

    /// <inheritdoc />
    public async Task<ClipPage> ExecuteSqlPageAsync(string sqlQuery, int offset, int count, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sqlQuery);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        _logger.LogInformation("Executing collection SQL query: {SqlQuery}", sqlQuery);

        var connection = _context.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened)
            await _context.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            var (clips, total) = await QueryPageAsync(connection, sqlQuery.Trim().TrimEnd(';'), new DynamicParameters(), offset, count,
                cancellationToken);

            return new ClipPage(clips, offset, total);
        }
        finally
        {
            if (opened)
                await _context.Database.CloseConnectionAsync();
        }
    }

    /// <summary>
    /// Runs <paramref name="query" /> (a single SELECT) once, as a subquery ordered newest first (SortKey descending, then
    /// ID), and returns the rows from <paramref name="offset" />, at most <paramref name="limit" /> of them (all if
    /// <c>null</c>), with the total row count counted in the same pass. Only when that page is empty although rows may
    /// exist (an offset past the first row, or a limit of zero) is the query counted separately.
    /// </summary>
    private async Task<(List<Clip> Clips, int Total)> QueryPageAsync(IDbConnection connection, string query, DynamicParameters parameters,
        int offset, int? limit, CancellationToken cancellationToken)
    {
        parameters.Add("PageOffset", offset);
        parameters.Add("PageLimit", limit ?? -1); // SQLite: a negative LIMIT means no limit

        long total = 0;
        var clips = (await connection.QueryAsync<Clip, long, Clip>(new CommandDefinition(
                $"Select Matches.*, COUNT(*) Over () as MatchCount from ({query}\n) as Matches\nOrder By Matches.SortKey Desc, Matches.ID\nLimit @PageLimit Offset @PageOffset",
                parameters, cancellationToken: cancellationToken),
            (clip, count) =>
            {
                total = count;
                return clip;
            },
            "MatchCount")).ToList();

        if (clips.Count == 0 && (offset > 0 || limit == 0))
            total = await connection.ExecuteScalarAsync<long>(new CommandDefinition($"Select COUNT(*) from ({query}\n)", parameters,
                cancellationToken: cancellationToken));

        if (clips.Count > 0)
            await LoadFormatFlagsAsync(clips, cancellationToken);

        return (clips, (int)Math.Min(total, int.MaxValue));
    }

    private async Task<IReadOnlyList<Clip>> QueryClipsAsync(string sqlQuery, DynamicParameters? parameters, CancellationToken cancellationToken)
    {
        // Parameter values are deliberately not logged: they can contain clip search text.
        _logger.LogInformation("Executing search SQL query: {SqlQuery}", sqlQuery);

        // Use Dapper for flexible raw SQL support (handles SELECT *, implicit joins, custom functions)
        var connection = _context.Database.GetDbConnection();
        var command = new CommandDefinition(sqlQuery, parameters, cancellationToken: cancellationToken);
        var clips = (await connection.QueryAsync<Clip>(command)).ToList();

        // Load format flags for the results
        if (clips.Count > 0)
            await LoadFormatFlagsAsync(clips, cancellationToken);

        return clips;
    }

    public async Task<IEnumerable<Clip>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Fetch clips without ordering (SQLite doesn't support DateTimeOffset in ORDER BY)
        var clips = await _context.Clips
            .ToListAsync(cancellationToken);

        // Sort in memory after fetching (by manual SortKey, which also correlates with capture order)
        clips = clips.OrderByDescending(p => p.SortKey).ToList();

        await LoadFormatFlagsAsync(clips, cancellationToken);

        return clips;
    }

    public async Task<IEnumerable<Clip>> GetByTypeAsync(ClipType type, CancellationToken cancellationToken = default)
    {
        // Fetch clips without ordering (SQLite doesn't support DateTimeOffset in ORDER BY)
        var clips = await _context.Clips
            .Where(p => p.Type == type)
            .ToListAsync(cancellationToken);

        // Sort in memory after fetching (by manual SortKey, which also correlates with capture order)
        clips = clips.OrderByDescending(p => p.SortKey).ToList();

        await LoadFormatFlagsAsync(clips, cancellationToken);

        return clips;
    }

    public async Task<IReadOnlyList<Clip>> GetPinnedAsync(CancellationToken cancellationToken = default)
    {
        // Fetch clips without ordering (SQLite doesn't support DateTimeOffset in ORDER BY)
        var clips = await _context.Clips
            .Where(p => p.Label == "Pinned" && !p.Del)
            .ToListAsync(cancellationToken);

        // Sort in memory after fetching (by manual SortKey, which also correlates with capture order)
        clips = clips.OrderByDescending(p => p.SortKey).ToList();

        // Load format flags from ClipData table (not actual content)
        await LoadFormatFlagsAsync(clips, cancellationToken);

        return clips;
    }

    /// <summary>
    /// Helper to get registered clipboard format IDs (approximation for now).
    /// In real implementation, would call Win32 RegisterClipboardFormat.
    /// </summary>
    private static int RegisterClipboardFormat(string formatName)
    {
        // Standard format codes (approximation)
        return formatName switch
        {
            "Rich Text Format" => Formats.RichText.Code,
            "HTML Format" => Formats.Html.Code,
            var _ => 0x00FF, // Custom format
        };
    }

    public async Task<Clip> AddAsync(Clip clip, CancellationToken cancellationToken = default)
        => await CreateAsync(clip, cancellationToken);

    /// <summary>
    /// Deletes all BLOB data associated with a clip (cascades to ClipData and all BLOB tables).
    /// </summary>
    private async Task DeleteClipBlobsAsync(Guid clipId, CancellationToken cancellationToken)
    {
        // EF Core will cascade delete BLOBs when we delete ClipData (configured in OnModelCreating)
        var clipDataEntries = await _context.ClipData
            .Where(p => p.ClipId == clipId)
            .ToListAsync(cancellationToken);

        _context.ClipData.RemoveRange(clipDataEntries);
    }

    public async Task<long> GetCountAsync(CancellationToken cancellationToken = default)
        => await _context.Clips
            .Where(p => !p.Del)
            .LongCountAsync(cancellationToken);

    /// <summary>
    /// Loads format availability flags for a list of clips by checking ClipData table.
    /// Directly queries and aggregates format information to populate transient properties.
    /// This is much faster than loading actual content from BLOB tables.
    /// </summary>
    private async Task LoadFormatFlagsAsync(IEnumerable<Clip> clips, CancellationToken cancellationToken)
    {
        var clipsList = clips.ToList();

        if (clipsList.Count == 0)
            return;

        var clipIds = clipsList.Select(p => p.Id).ToList();

        // Query ClipData and aggregate format flags by ClipId in a single database query
        var formatFlags = await _context.ClipData
            .Where(p => clipIds.Contains(p.ClipId))
            .GroupBy(p => p.ClipId)
            .Select(p => new
            {
                ClipId = p.Key,
                HasText = p.Any(cd => cd.Format == Formats.Text.Code || cd.Format == Formats.UnicodeText.Code || cd.FormatName == Formats.Text.Name || cd.FormatName == Formats.UnicodeText.Name),
                HasRtf = p.Any(cd => cd.FormatName == "CF_RTF" || cd.Format == Formats.RichText.Code),
                HasHtml = p.Any(cd => cd.FormatName == Formats.Html.Name || cd.Format == Formats.Html.Code || cd.Format == Formats.HtmlAlt.Code),
                HasBitmap = p.Any(cd => cd.Format == Formats.Bitmap.Code || cd.Format == Formats.Dib.Code || cd.FormatName == Formats.Bitmap.Name || cd.FormatName == Formats.Dib.Name),
                HasFiles = p.Any(cd => cd.Format == Formats.HDrop.Code || cd.FormatName == Formats.HDrop.Name),
                FormatNames = string.Join(", ", p.Select(cd => cd.FormatName)),
            })
            .ToListAsync(cancellationToken);

        _logger.LogDebug("LoadFormatFlagsAsync: Loaded format flags for {ClipCount} clips from ClipData", formatFlags.Count);

        var formatFlagsDict = formatFlags.ToDictionary(p => p.ClipId);

        // Apply format flags to clips
        foreach (var item in clipsList)
        {
            if (formatFlagsDict.TryGetValue(item.Id, out var flags))
            {
                // Set format flags directly
                item.HasText = flags.HasText;
                item.HasRtf = flags.HasRtf;
                item.HasHtml = flags.HasHtml;
                item.HasBitmap = flags.HasBitmap;
                item.HasFiles = flags.HasFiles;

                // Pre-compute and cache the icon string
                var icons = new List<string>();
                if (item.Macro)
                    icons.Add("⌨");

                if (flags.HasBitmap)
                    icons.Add("🖼");

                if (flags.HasRtf)
                    icons.Add("🅰");

                if (flags.HasHtml)
                    icons.Add("🌐");

                if (flags.HasFiles)
                    icons.Add("📁");

                if (flags.HasText)
                    icons.Add("📄");

                // Add lock icon prefix for encrypted clips
                var lockIcon = item.Encrypted
                    ? item.IsDecrypted
                        ? "🔓"
                        : "🔒"
                    : string.Empty;

                item.IconGlyph = icons.Count > 0
                    ? lockIcon + string.Join("", icons)
                    : lockIcon + "❓";

                _logger.LogDebug("Clip {ClipId}: Formats loaded - {FormatNames}, Icon: {Icon}", item.Id, flags.FormatNames, item.IconGlyph);
            }
            else
            {
                _logger.LogDebug("Clip {ClipId}: No ClipData found, using Type={ClipType} fallback", item.Id, item.Type);

                // Fallback for clips created before ClipData implementation
                switch (item.Type)
                {
                    case ClipType.Text:
                        item.HasText = true;
                        item.IconGlyph = item.Macro
                            ? "⌨📄"
                            : "📄";

                        break;
                    case ClipType.Image:
                        item.HasBitmap = true;
                        item.IconGlyph = item.Macro
                            ? "⌨🖼"
                            : "🖼";

                        break;
                    case ClipType.Files:
                        item.HasFiles = true;
                        item.IconGlyph = item.Macro
                            ? "⌨📁"
                            : "📁";

                        break;
                    default:
                        item.IconGlyph = "❓";
                        break;
                }

                // Add lock icon prefix for encrypted clips
                if (item.Encrypted)
                {
                    var lockIcon = item.IsDecrypted
                        ? "🔓"
                        : "🔒";

                    item.IconGlyph = lockIcon + item.IconGlyph;
                }
            }
        }
    }

}
