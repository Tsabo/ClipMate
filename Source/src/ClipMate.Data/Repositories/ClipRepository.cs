using System.Data;
using System.Text;
using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Repositories;

/// <summary>
/// Type handler for SQLite GUID string conversion.
/// SQLite stores GUIDs as strings, Dapper needs help converting them.
/// </summary>
public class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public override Guid Parse(object value)
    {
        return value switch
        {
            string s => Guid.Parse(s),
            Guid g => g,
            var _ => throw new InvalidOperationException($"Cannot convert {value.GetType()} to Guid"),
        };
    }

    public override void SetValue(IDbDataParameter parameter, Guid value) => parameter.Value = value.ToString();
}

/// <summary>
/// Type handler for SQLite DateTimeOffset string conversion.
/// SQLite stores DateTimeOffset as ISO 8601 strings.
/// </summary>
public class DateTimeOffsetTypeHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override DateTimeOffset Parse(object value)
    {
        return value switch
        {
            string s => DateTimeOffset.Parse(s),
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(dt),
            var _ => throw new InvalidOperationException($"Cannot convert {value.GetType()} to DateTimeOffset"),
        };
    }

    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value) => parameter.Value = value.ToString("o"); // ISO 8601
}

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

        // Sort in memory and take the requested count
        clipsWithFormats = clipsWithFormats
            .OrderByDescending(p => p.Clip.CapturedAt)
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

            clip.IconGlyph = icons.Count > 0
                ? string.Join("", icons)
                : "❓";

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

        // Sort in memory after fetching
        clips = clips.OrderByDescending(p => p.CapturedAt).ToList();

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

        // Sort in memory after fetching
        clips = clips.OrderByDescending(p => p.CapturedAt).ToList();

        // Load format flags from ClipData table (not actual content)
        await LoadFormatFlagsAsync(clips, cancellationToken);

        return clips;
    }

    public async Task<IReadOnlyList<Clip>> GetFavoritesAsync(CancellationToken cancellationToken = default)
    {
        // Fetch clips without ordering (SQLite doesn't support DateTimeOffset in ORDER BY)
        var clips = await _context.Clips
            .Where(p => p.IsFavorite && !p.Del)
            .ToListAsync(cancellationToken);

        // Sort in memory after fetching
        clips = clips.OrderByDescending(p => p.CapturedAt).ToList();

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
        await StoreClipContentAsync(clip, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        // IMPORTANT: Load format flags to populate cached properties for UI display
        // This ensures new clips show checkboxes immediately without requiring a reload
        await LoadFormatFlagsAsync([clip], cancellationToken);

        return clip;
    }

    public async Task<bool> UpdateAsync(Clip clip, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clip);

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
        clip.DelDate = DateTime.UtcNow;
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

    public async Task<IReadOnlyList<Clip>> ExecuteSqlQueryAsync(string sqlQuery, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing search SQL query: {SqlQuery}", sqlQuery);

        // Use Dapper for flexible raw SQL support (handles SELECT *, implicit joins, custom functions)
        var connection = _context.Database.GetDbConnection();
        var clips = (await connection.QueryAsync<Clip>(sqlQuery)).ToList();

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

        // Sort in memory after fetching
        clips = clips.OrderByDescending(p => p.CapturedAt).ToList();

        await LoadFormatFlagsAsync(clips, cancellationToken);

        return clips;
    }

    public async Task<IEnumerable<Clip>> GetByTypeAsync(ClipType type, CancellationToken cancellationToken = default)
    {
        // Fetch clips without ordering (SQLite doesn't support DateTimeOffset in ORDER BY)
        var clips = await _context.Clips
            .Where(p => p.Type == type)
            .ToListAsync(cancellationToken);

        // Sort in memory after fetching
        clips = clips.OrderByDescending(p => p.CapturedAt).ToList();

        await LoadFormatFlagsAsync(clips, cancellationToken);

        return clips;
    }

    public async Task<IReadOnlyList<Clip>> GetPinnedAsync(CancellationToken cancellationToken = default)
    {
        // Fetch clips without ordering (SQLite doesn't support DateTimeOffset in ORDER BY)
        var clips = await _context.Clips
            .Where(p => p.Label == "Pinned" && !p.Del)
            .ToListAsync(cancellationToken);

        // Sort in memory after fetching
        clips = clips.OrderByDescending(p => p.CapturedAt).ToList();

        // Load format flags from ClipData table (not actual content)
        await LoadFormatFlagsAsync(clips, cancellationToken);

        return clips;
    }

    /// <summary>
    /// Stores clip content in the appropriate BLOB tables based on available formats.
    /// Creates ClipData entries for each format and stores actual content separately.
    /// Transient properties (TextContent, ImageData, etc.) are NOT persisted to Clips table.
    /// </summary>
    private async Task StoreClipContentAsync(Clip clip, CancellationToken cancellationToken)
    {
        var clipDataEntries = new List<ClipData>();

        // Handle text formats (CF_TEXT = 1, CF_UNICODETEXT = 13)
        if (!string.IsNullOrEmpty(clip.TextContent))
        {
            var clipData = new ClipData
            {
                Id = Guid.NewGuid(),
                ClipId = clip.Id,
                FormatName = Formats.UnicodeText.Name,
                Format = Formats.UnicodeText.Code,
                Size = clip.TextContent.Length * 2, // Unicode bytes
                StorageType = 1, // TEXT
            };

            clipDataEntries.Add(clipData);

            // Store in BlobTxt table
            var blobTxt = new BlobTxt
            {
                Id = Guid.NewGuid(),
                ClipDataId = clipData.Id,
                ClipId = clip.Id, // Denormalized for performance
                Data = clip.TextContent,
            };

            _context.BlobTxt.Add(blobTxt);
        }

        // Handle RTF format (CF_RTF)
        if (!string.IsNullOrEmpty(clip.RtfContent))
        {
            var clipData = new ClipData
            {
                Id = Guid.NewGuid(),
                ClipId = clip.Id,
                FormatName = "CF_RTF",
                Format = Formats.RichText.Code,
                Size = clip.RtfContent.Length * 2,
                StorageType = 1, // TEXT
            };

            clipDataEntries.Add(clipData);

            var blobTxt = new BlobTxt
            {
                Id = Guid.NewGuid(),
                ClipDataId = clipData.Id,
                ClipId = clip.Id,
                Data = clip.RtfContent,
            };

            _context.BlobTxt.Add(blobTxt);
        }

        // Handle HTML format (CF_HTML)
        if (!string.IsNullOrEmpty(clip.HtmlContent))
        {
            var clipData = new ClipData
            {
                Id = Guid.NewGuid(),
                ClipId = clip.Id,
                FormatName = Formats.Html.Name,
                Format = Formats.Html.Code,
                Size = clip.HtmlContent.Length * 2,
                StorageType = 1, // TEXT
            };

            clipDataEntries.Add(clipData);

            var blobTxt = new BlobTxt
            {
                Id = Guid.NewGuid(),
                ClipDataId = clipData.Id,
                ClipId = clip.Id,
                Data = clip.HtmlContent,
            };

            _context.BlobTxt.Add(blobTxt);
        }

        // Handle image format - detect and store in original format
        if (clip.ImageData is { Length: > 0 })
        {
            var imageFormat = DetectImageFormat(clip.ImageData);
            var storageType = imageFormat == ImageFormat.Jpeg
                ? 2
                : 3; // 2=JPEG, 3=PNG

            var clipData = new ClipData
            {
                Id = Guid.NewGuid(),
                ClipId = clip.Id,
                FormatName = Formats.Dib.Name,
                Format = Formats.Dib.Code,
                Size = clip.ImageData.Length,
                StorageType = storageType,
            };

            clipDataEntries.Add(clipData);

            // Store in appropriate blob table based on detected format
            if (imageFormat == ImageFormat.Jpeg)
            {
                var blobJpg = new BlobJpg
                {
                    Id = Guid.NewGuid(),
                    ClipDataId = clipData.Id,
                    ClipId = clip.Id,
                    Data = clip.ImageData,
                };

                _context.BlobJpg.Add(blobJpg);
            }
            else // PNG or fallback
            {
                var blobPng = new BlobPng
                {
                    Id = Guid.NewGuid(),
                    ClipDataId = clipData.Id,
                    ClipId = clip.Id,
                    Data = clip.ImageData,
                };

                _context.BlobPng.Add(blobPng);
            }
        }

        // Handle files format (CF_HDROP = 15)
        if (!string.IsNullOrEmpty(clip.FilePathsJson))
        {
            var clipData = new ClipData
            {
                Id = Guid.NewGuid(),
                ClipId = clip.Id,
                FormatName = Formats.HDrop.Name,
                Format = Formats.HDrop.Code,
                Size = clip.FilePathsJson.Length * 2,
                StorageType = 4, // BLOB (generic)
            };

            clipDataEntries.Add(clipData);

            var blobBlob = new BlobBlob
            {
                Id = Guid.NewGuid(),
                ClipDataId = clipData.Id,
                ClipId = clip.Id,
                Data = Encoding.UTF8.GetBytes(clip.FilePathsJson),
            };

            _context.BlobBlob.Add(blobBlob);
        }

        // Add all ClipData entries
        if (clipDataEntries.Count > 0)
            await _context.ClipData.AddRangeAsync(clipDataEntries, cancellationToken);
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

    public async Task<Clip> AddAsync(Clip clip, CancellationToken cancellationToken = default) => await CreateAsync(clip, cancellationToken);

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

    public async Task<long> GetCountAsync(CancellationToken cancellationToken = default) => await _context.Clips.Where(c => !c.Del).LongCountAsync(cancellationToken);

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

                item.IconGlyph = icons.Count > 0
                    ? string.Join("", icons)
                    : "❓";

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
                        item.IconGlyph = "📄";

                        break;
                    case ClipType.Image:
                        item.HasBitmap = true;
                        item.IconGlyph = "🖼";

                        break;
                    case ClipType.Files:
                        item.HasFiles = true;
                        item.IconGlyph = "📁";

                        break;
                    default:
                        item.IconGlyph = "❓";

                        break;
                }
            }
        }
    }

    /// <summary>
    /// Detects image format from byte array by examining magic bytes.
    /// </summary>
    private static ImageFormat DetectImageFormat(byte[] imageData)
    {
        if (imageData.Length < 4)
            return ImageFormat.Unknown;

        // Check PNG signature: 89 50 4E 47 (‰PNG)
        if (imageData.Length >= 8 &&
            imageData[0] == 0x89 &&
            imageData[1] == 0x50 &&
            imageData[2] == 0x4E &&
            imageData[3] == 0x47 &&
            imageData[4] == 0x0D &&
            imageData[5] == 0x0A &&
            imageData[6] == 0x1A &&
            imageData[7] == 0x0A)
            return ImageFormat.Png;

        // Check JPEG signature: FF D8 FF
        if (imageData.Length >= 3 &&
            imageData[0] == 0xFF &&
            imageData[1] == 0xD8 &&
            imageData[2] == 0xFF)
            return ImageFormat.Jpeg;

        // Check GIF signature: GIF87a or GIF89a
        if (imageData.Length >= 6 &&
            imageData[0] == 0x47 && // G
            imageData[1] == 0x49 && // I
            imageData[2] == 0x46 && // F
            imageData[3] == 0x38 && // 8
            (imageData[4] == 0x37 || imageData[4] == 0x39) && // 7 or 9
            imageData[5] == 0x61) // a
            return ImageFormat.Gif;

        // Check BMP signature: BM
        if (imageData.Length >= 2 &&
            imageData[0] == 0x42 && // B
            imageData[1] == 0x4D) // M
            return ImageFormat.Bmp;

        return ImageFormat.Unknown;
    }

    private enum ImageFormat
    {
        Unknown,
        Png,
        Jpeg,
        Gif,
        Bmp,
    }
}
