using System.Text;
using ClipMate.Core.Models;
using ClipMate.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Repositories;

/// <summary>
///     Entity Framework Core implementation of the clip repository.
///     Handles ClipMate storage architecture: Clip metadata + ClipData formats + BLOB content.
/// </summary>
public class ClipRepository : IClipRepository
{
    private readonly ClipMateDbContext _context;
    private readonly ILogger<ClipRepository> _logger;

    public ClipRepository(ClipMateDbContext context, ILogger<ClipRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Clip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var clip = await _context.Clips
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (clip != null)
        {
            await LoadFormatFlagsAsync([clip], cancellationToken);
        }

        return clip;
    }

    public async Task<IReadOnlyList<Clip>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        // Single query: fetch clips with their format flags in one database round-trip
        var clipsWithFormats = await _context.Clips
            .Where(p => !p.Del) // Exclude soft-deleted clips
            .OrderByDescending(p => p.CapturedAt)
            .Take(count)
            .GroupJoin(
                _context.ClipData,
                clip => clip.Id,
                clipData => clipData.ClipId,
                (clip, clipDataGroup) => new
                {
                    Clip = clip,
                    HasText = clipDataGroup.Any(p => p.Format == 1 || p.Format == 13 || p.FormatName == "CF_TEXT" || p.FormatName == "CF_UNICODETEXT"),
                    HasRtf = clipDataGroup.Any(p => p.FormatName == "CF_RTF" || p.Format == 0x0082),
                    HasHtml = clipDataGroup.Any(p => p.FormatName == "HTML Format" || p.Format == 0x0080),
                    HasBitmap = clipDataGroup.Any(p => p.Format == 2 || p.Format == 8 || p.FormatName == "CF_BITMAP" || p.FormatName == "CF_DIB"),
                    HasFiles = clipDataGroup.Any(p => p.Format == 15 || p.FormatName == "CF_HDROP"),
                    FormatNames = string.Join(", ", clipDataGroup.Select(cd => cd.FormatName))
                })
            .ToListAsync(cancellationToken);

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
            {
                icons.Add("🖼");
            }
            if (item.HasRtf)
            {
                icons.Add("🅰");
            }
            if (item.HasHtml)
            {
                icons.Add("🌐");
            }
            if (item.HasFiles)
            {
                icons.Add("📁");
            }
            if (item.HasText)
            {
                icons.Add("📄");
            }

            clip.IconGlyph = icons.Count > 0 ? string.Join("", icons) : "❓";

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
        var clips = await _context.Clips
            .Where(p => p.CollectionId == collectionId && !p.Del)
            .OrderByDescending(p => p.CapturedAt)
            .ToListAsync(cancellationToken);

        // Load format flags from ClipData table (not actual content)
        await LoadFormatFlagsAsync(clips, cancellationToken);

        return clips;
    }

    public async Task<IReadOnlyList<Clip>> GetByFolderAsync(Guid folderId, CancellationToken cancellationToken = default)
    {
        var clips = await _context.Clips
            .Where(p => p.FolderId == folderId && !p.Del)
            .OrderByDescending(p => p.CapturedAt)
            .ToListAsync(cancellationToken);

        // Load format flags from ClipData table (not actual content)
        await LoadFormatFlagsAsync(clips, cancellationToken);

        return clips;
    }

    public async Task<IReadOnlyList<Clip>> GetFavoritesAsync(CancellationToken cancellationToken = default)
    {
        var clips = await _context.Clips
            .Where(p => p.IsFavorite && !p.Del)
            .OrderByDescending(p => p.CapturedAt)
            .ToListAsync(cancellationToken);

        // Load format flags from ClipData table (not actual content)
        await LoadFormatFlagsAsync(clips, cancellationToken);

        return clips;
    }

    public async Task<Clip?> GetByContentHashAsync(string contentHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentHash))
        {
            return null;
        }

        var clip = await _context.Clips
            .FirstOrDefaultAsync(p => p.ContentHash == contentHash, cancellationToken);

        if (clip != null)
        {
            await LoadFormatFlagsAsync([clip], cancellationToken);
        }

        return clip;
    }

    public async Task<IReadOnlyList<Clip>> SearchAsync(string searchText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return new List<Clip>();
        }

        var clips = await _context.Clips
            .Where(p => !p.Del && EF.Functions.Like(p.TextContent ?? "", $"%{searchText}%"))
            .OrderByDescending(p => p.CapturedAt)
            .ToListAsync(cancellationToken);

        // Load format flags from ClipData table (not actual content)
        await LoadFormatFlagsAsync(clips, cancellationToken);

        return clips;
    }

    public async Task<Clip> CreateAsync(Clip clip, CancellationToken cancellationToken = default)
    {
        if (clip == null)
        {
            throw new ArgumentNullException(nameof(clip));
        }

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
        if (clip == null)
        {
            throw new ArgumentNullException(nameof(clip));
        }

        _context.Clips.Update(clip);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var clip = await _context.Clips.FindAsync([id], cancellationToken);

        if (clip == null)
        {
            return false;
        }

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
        var clipsToDelete = await _context.Clips
            .Where(p => p.CapturedAt < cutoffDate && !p.IsFavorite)
            .ToListAsync(cancellationToken);

        foreach (var clip in clipsToDelete)
        {
            clip.Del = true;
            clip.DelDate = DateTime.UtcNow;
            await DeleteClipBlobsAsync(clip.Id, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return clipsToDelete.Count;
    }

    public async Task<IReadOnlyList<ClipData>> GetClipFormatsAsync(Guid clipId, CancellationToken cancellationToken = default)
    {
        return await _context.ClipData
            .Where(cd => cd.ClipId == clipId)
            .OrderBy(cd => cd.FormatName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Clip>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var clips = await _context.Clips
            .OrderByDescending(p => p.CapturedAt)
            .ToListAsync(cancellationToken);

        await LoadFormatFlagsAsync(clips, cancellationToken);

        return clips;
    }

    public async Task<IEnumerable<Clip>> GetByTypeAsync(ClipType type, CancellationToken cancellationToken = default)
    {
        var clips = await _context.Clips
            .Where(p => p.Type == type)
            .OrderByDescending(p => p.CapturedAt)
            .ToListAsync(cancellationToken);

        await LoadFormatFlagsAsync(clips, cancellationToken);

        return clips;
    }

    public async Task<IReadOnlyList<Clip>> GetPinnedAsync(CancellationToken cancellationToken = default)
    {
        var clips = await _context.Clips
            .Where(p => p.Label == "Pinned" && !p.Del)
            .OrderByDescending(p => p.CapturedAt)
            .ToListAsync(cancellationToken);

        // Load format flags from ClipData table (not actual content)
        await LoadFormatFlagsAsync(clips, cancellationToken);

        return clips;
    }

    /// <summary>
    ///     Stores clip content in the appropriate BLOB tables based on available formats.
    ///     Creates ClipData entries for each format and stores actual content separately.
    ///     Transient properties (TextContent, ImageData, etc.) are NOT persisted to Clips table.
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
                FormatName = "CF_UNICODETEXT",
                Format = 13, // CF_UNICODETEXT
                Size = clip.TextContent.Length * 2, // Unicode bytes
                StorageType = 1 // TEXT
            };

            clipDataEntries.Add(clipData);

            // Store in BlobTxt table
            var blobTxt = new BlobTxt
            {
                Id = Guid.NewGuid(),
                ClipDataId = clipData.Id,
                ClipId = clip.Id, // Denormalized for performance
                Data = clip.TextContent
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
                Format = RegisterClipboardFormat("Rich Text Format"), // ~0x0082
                Size = clip.RtfContent.Length * 2,
                StorageType = 1 // TEXT
            };

            clipDataEntries.Add(clipData);

            var blobTxt = new BlobTxt
            {
                Id = Guid.NewGuid(),
                ClipDataId = clipData.Id,
                ClipId = clip.Id,
                Data = clip.RtfContent
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
                FormatName = "HTML Format",
                Format = RegisterClipboardFormat("HTML Format"), // ~0x0080
                Size = clip.HtmlContent.Length * 2,
                StorageType = 1 // TEXT
            };

            clipDataEntries.Add(clipData);

            var blobTxt = new BlobTxt
            {
                Id = Guid.NewGuid(),
                ClipDataId = clipData.Id,
                ClipId = clip.Id,
                Data = clip.HtmlContent
            };

            _context.BlobTxt.Add(blobTxt);
        }

        // Handle image format (CF_BITMAP = 2, CF_DIB = 8)
        if (clip.ImageData is { Length: > 0 })
        {
            var clipData = new ClipData
            {
                Id = Guid.NewGuid(),
                ClipId = clip.Id,
                FormatName = "CF_DIB",
                Format = 8, // CF_DIB
                Size = clip.ImageData.Length,
                StorageType = 3 // PNG (we store as PNG)
            };

            clipDataEntries.Add(clipData);

            // Store in BlobPng table (we converted to PNG in ClipboardService)
            var blobPng = new BlobPng
            {
                Id = Guid.NewGuid(),
                ClipDataId = clipData.Id,
                ClipId = clip.Id,
                Data = clip.ImageData
            };

            _context.BlobPng.Add(blobPng);
        }

        // Handle files format (CF_HDROP = 15)
        if (!string.IsNullOrEmpty(clip.FilePathsJson))
        {
            var clipData = new ClipData
            {
                Id = Guid.NewGuid(),
                ClipId = clip.Id,
                FormatName = "CF_HDROP",
                Format = 15, // CF_HDROP
                Size = clip.FilePathsJson.Length * 2,
                StorageType = 4 // BLOB (generic)
            };

            clipDataEntries.Add(clipData);

            var blobBlob = new BlobBlob
            {
                Id = Guid.NewGuid(),
                ClipDataId = clipData.Id,
                ClipId = clip.Id,
                Data = Encoding.UTF8.GetBytes(clip.FilePathsJson)
            };

            _context.BlobBlob.Add(blobBlob);
        }

        // Add all ClipData entries
        if (clipDataEntries.Count > 0)
        {
            await _context.ClipData.AddRangeAsync(clipDataEntries, cancellationToken);
        }
    }

    /// <summary>
    ///     Helper to get registered clipboard format IDs (approximation for now).
    ///     In real implementation, would call Win32 RegisterClipboardFormat.
    /// </summary>
    private static int RegisterClipboardFormat(string formatName)
    {
        // Standard format codes (approximation)
        return formatName switch
        {
            "Rich Text Format" => 0x0082,
            "HTML Format" => 0x0080,
            _ => 0x00FF // Custom format
        };
    }

    public async Task<Clip> AddAsync(Clip clip, CancellationToken cancellationToken = default) => await CreateAsync(clip, cancellationToken);

    /// <summary>
    ///     Deletes all BLOB data associated with a clip (cascades to ClipData and all BLOB tables).
    /// </summary>
    private async Task DeleteClipBlobsAsync(Guid clipId, CancellationToken cancellationToken)
    {
        // EF Core will cascade delete BLOBs when we delete ClipData (configured in OnModelCreating)
        var clipDataEntries = await _context.ClipData
            .Where(cd => cd.ClipId == clipId)
            .ToListAsync(cancellationToken);

        _context.ClipData.RemoveRange(clipDataEntries);
    }

    public async Task<long> GetCountAsync(CancellationToken cancellationToken = default) => await _context.Clips.Where(c => !c.Del).LongCountAsync(cancellationToken);

    /// <summary>
    ///     Loads format availability flags for a list of clips by checking ClipData table.
    ///     Directly queries and aggregates format information to populate transient properties.
    ///     This is much faster than loading actual content from BLOB tables.
    /// </summary>
    private async Task LoadFormatFlagsAsync(IEnumerable<Clip> clips, CancellationToken cancellationToken)
    {
        var clipsList = clips.ToList();

        if (!clipsList.Any())
        {
            return;
        }

        var clipIds = clipsList.Select(p => p.Id).ToList();

        // Query ClipData and aggregate format flags by ClipId in a single database query
        var formatFlags = await _context.ClipData
            .Where(p => clipIds.Contains(p.ClipId))
            .GroupBy(p => p.ClipId)
            .Select(p => new
            {
                ClipId = p.Key,
                HasText = p.Any(cd => cd.Format == 1 || cd.Format == 13 || cd.FormatName == "CF_TEXT" || cd.FormatName == "CF_UNICODETEXT"),
                HasRtf = p.Any(cd => cd.FormatName == "CF_RTF" || cd.Format == 0x0082),
                HasHtml = p.Any(cd => cd.FormatName == "HTML Format" || cd.Format == 0x0080),
                HasBitmap = p.Any(cd => cd.Format == 2 || cd.Format == 8 || cd.FormatName == "CF_BITMAP" || cd.FormatName == "CF_DIB"),
                HasFiles = p.Any(cd => cd.Format == 15 || cd.FormatName == "CF_HDROP"),
                FormatNames = string.Join(", ", p.Select(cd => cd.FormatName))
            })
            .ToListAsync(cancellationToken);

        _logger.LogDebug("LoadFormatFlagsAsync: Loaded format flags for {ClipCount} clips from ClipData", formatFlags.Count);

        var formatFlagsDict = formatFlags.ToDictionary(p => p.ClipId);

        // Apply format flags to clips
        foreach (var clip in clipsList)
        {
            if (formatFlagsDict.TryGetValue(clip.Id, out var flags))
            {
                // Set format flags directly
                clip.HasText = flags.HasText;
                clip.HasRtf = flags.HasRtf;
                clip.HasHtml = flags.HasHtml;
                clip.HasBitmap = flags.HasBitmap;
                clip.HasFiles = flags.HasFiles;

                // Pre-compute and cache the icon string
                var icons = new List<string>();
                if (flags.HasBitmap)
                {
                    icons.Add("🖼");
                }

                if (flags.HasRtf)
                {
                    icons.Add("🅰");
                }

                if (flags.HasHtml)
                {
                    icons.Add("🌐");
                }

                if (flags.HasFiles)
                {
                    icons.Add("📁");
                }

                if (flags.HasText)
                {
                    icons.Add("📄");
                }

                clip.IconGlyph = icons.Count > 0
                    ? string.Join("", icons)
                    : "❓";

                _logger.LogDebug("Clip {ClipId}: Formats loaded - {FormatNames}, Icon: {Icon}", clip.Id, flags.FormatNames, clip.IconGlyph);
            }
            else
            {
                _logger.LogDebug("Clip {ClipId}: No ClipData found, using Type={ClipType} fallback", clip.Id, clip.Type);

                // Fallback for clips created before ClipData implementation
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
    }
}
