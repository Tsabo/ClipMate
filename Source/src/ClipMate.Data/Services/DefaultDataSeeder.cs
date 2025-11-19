using ClipMate.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClipMate.Data.Services;

/// <summary>
/// Service to seed default ClipMate 7.5 collections and virtual collections.
/// Runs outside of EF Migrations to allow easy database switching.
/// </summary>
public class DefaultDataSeeder
{
    private readonly ClipMateDbContext _context;
    private readonly ILogger<DefaultDataSeeder>? _logger;

    public DefaultDataSeeder(ClipMateDbContext context, ILogger<DefaultDataSeeder>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger;
    }

    /// <summary>
    /// Seeds the database with default ClipMate 7.5 collection structure if it doesn't exist.
    /// </summary>
    public async Task SeedDefaultDataAsync()
    {
        try
        {
            // Check if collections already exist
            var existingCollections = await _context.Collections.AnyAsync();
            if (existingCollections)
            {
                _logger?.LogInformation("Collections already exist, skipping default data seeding");
                return;
            }

            _logger?.LogInformation("Seeding default ClipMate 7.5 collection structure");

            var now = DateTime.UtcNow;
            var emptyGuid = Guid.Parse("00000000-0000-0000-0000-000000000000");

            // Create root collections
            var collections = new List<Collection>
            {
                // InBox - Where new clips go by default
                new Collection
                {
                    Id = Guid.Parse("E21B62F2-4CFA-4913-9B79-4F955F4F202D"),
                    ParentId = null,
                    ParentGuid = emptyGuid,
                    Title = "InBox",
                    LmType = 0,      // Normal collection
                    ListType = 0,    // Normal list
                    SortKey = 100,
                    IlIndex = 7,     // Icon index
                    RetentionLimit = 200,
                    NewClipsGo = 1,  // Accept new clips
                    AcceptNewClips = true,
                    ReadOnly = false,
                    AcceptDuplicates = false,
                    SortColumn = -2, // Sort by date
                    SortAscending = false,
                    Encrypted = false,
                    Favorite = false,
                    LastUserId = 1,
                    LastUpdateTime = now,
                    LastKnownCount = 0,
                    Sql = null,
                    CreatedAt = now
                },

                // Safe - For important clips (folder type)
                new Collection
                {
                    Id = Guid.Parse("C297C388-B07F-40B8-9E81-FB668F1562AD"),
                    ParentId = null,
                    ParentGuid = emptyGuid,
                    Title = "Safe",
                    LmType = 2,      // Folder type
                    ListType = 0,
                    SortKey = 200,
                    IlIndex = 9,
                    RetentionLimit = 0, // Unlimited
                    NewClipsGo = 1,
                    AcceptNewClips = false,
                    ReadOnly = false,
                    AcceptDuplicates = false,
                    SortColumn = -2,
                    SortAscending = false,
                    Encrypted = false,
                    Favorite = false,
                    LastUserId = 1,
                    LastUpdateTime = now,
                    LastKnownCount = 0,
                    Sql = null,
                    CreatedAt = now
                },

                // Overflow - Where clips go when InBox is full
                new Collection
                {
                    Id = Guid.Parse("A4FF1FD1-2E7E-426C-8C1D-715E54D1ABC6"),
                    ParentId = null,
                    ParentGuid = emptyGuid,
                    Title = "Overflow",
                    LmType = 0,
                    ListType = 0,
                    SortKey = 300,
                    IlIndex = 8,
                    RetentionLimit = 800,
                    NewClipsGo = 1,
                    AcceptNewClips = false,
                    ReadOnly = false,
                    AcceptDuplicates = false,
                    SortColumn = -2,
                    SortAscending = false,
                    Encrypted = false,
                    Favorite = false,
                    LastUserId = 1,
                    LastUpdateTime = now,
                    LastKnownCount = 0,
                    Sql = null,
                    CreatedAt = now
                },

                // Samples - Sample clips folder
                new Collection
                {
                    Id = Guid.Parse("253CB828-DF18-4833-ACF8-304DF0511122"),
                    ParentId = null,
                    ParentGuid = emptyGuid,
                    Title = "Samples",
                    LmType = 2,      // Folder type
                    ListType = 0,
                    SortKey = 350,
                    IlIndex = 40,
                    RetentionLimit = 200,
                    NewClipsGo = 1,
                    AcceptNewClips = false,
                    ReadOnly = false,
                    AcceptDuplicates = false,
                    SortColumn = -2,
                    SortAscending = false,
                    Encrypted = false,
                    Favorite = false,
                    LastUserId = 1,
                    LastUpdateTime = now,
                    LastKnownCount = 0,
                    Sql = null,
                    CreatedAt = now
                },

                // Virtual - Parent folder for virtual/smart collections
                new Collection
                {
                    Id = Guid.Parse("A82DA2A6-86AA-4FC6-A660-2543E7FE900D"),
                    ParentId = null,
                    ParentGuid = emptyGuid,
                    Title = "Virtual",
                    LmType = 2,      // Folder type
                    ListType = 1,    // Virtual collection
                    SortKey = 400,
                    IlIndex = 50,
                    RetentionLimit = 250,
                    NewClipsGo = 1,
                    AcceptNewClips = false,
                    ReadOnly = false,
                    AcceptDuplicates = true,
                    SortColumn = 1,
                    SortAscending = false,
                    Encrypted = false,
                    Favorite = false,
                    LastUserId = 1,
                    LastUpdateTime = null,
                    LastKnownCount = 0,
                    Sql = "xxx", // Placeholder
                    CreatedAt = now
                }
            };

            await _context.Collections.AddRangeAsync(collections);
            await _context.SaveChangesAsync();

            _logger?.LogInformation("Created {Count} root collections", collections.Count);

            // Create virtual collections (children of "Virtual" folder)
            var virtualParentId = Guid.Parse("A82DA2A6-86AA-4FC6-A660-2543E7FE900D");
            var virtualCollections = new List<Collection>
            {
                // Today - Clips captured today
                new Collection
                {
                    Id = Guid.Parse("27EBB8C8-FE43-4199-BD92-C953717C4066"),
                    ParentId = virtualParentId,
                    ParentGuid = virtualParentId,
                    Title = "Today",
                    LmType = 1,      // Virtual collection
                    ListType = 3,    // SQL-based
                    SortKey = 500,
                    IlIndex = 74,
                    RetentionLimit = 1,
                    NewClipsGo = 1,
                    AcceptNewClips = true,
                    ReadOnly = false,
                    AcceptDuplicates = true,
                    SortColumn = -3, // Custom sort
                    SortAscending = false,
                    Encrypted = false,
                    Favorite = false,
                    LastUserId = 1,
                    LastUpdateTime = null,
                    LastKnownCount = 0,
                    Sql = "Select clip.*, shortcut.nickname as Nickname from clip left outer join shortcut on shortcut.clip_GUID = clip.CLIP_GUID where Clip.TimeStamp >= '#DATE#' and del = false order by ID;",
                    CreatedAt = now
                },

                // This Week
                new Collection
                {
                    Id = Guid.Parse("962983D5-9C1D-43FA-9B70-D258F5AE54E6"),
                    ParentId = virtualParentId,
                    ParentGuid = virtualParentId,
                    Title = "This Week",
                    LmType = 1,
                    ListType = 3,
                    SortKey = 510,
                    IlIndex = 75,
                    RetentionLimit = 7,
                    NewClipsGo = 1,
                    AcceptNewClips = true,
                    ReadOnly = false,
                    AcceptDuplicates = true,
                    SortColumn = -3,
                    SortAscending = false,
                    Encrypted = false,
                    Favorite = false,
                    LastUserId = 1,
                    LastUpdateTime = null,
                    LastKnownCount = 0,
                    Sql = "Select clip.*, shortcut.nickname as Nickname from clip left outer join shortcut on shortcut.clip_GUID = clip.CLIP_GUID where Clip.TimeStamp >= '#DATEMINUSLIMIT#' and del = false order by ID;",
                    CreatedAt = now
                },

                // This Month
                new Collection
                {
                    Id = Guid.Parse("360D9460-6A7F-48C7-9554-D8E8D36FBFE9"),
                    ParentId = virtualParentId,
                    ParentGuid = virtualParentId,
                    Title = "This Month",
                    LmType = 1,
                    ListType = 3,
                    SortKey = 520,
                    IlIndex = 77,
                    RetentionLimit = 31,
                    NewClipsGo = 1,
                    AcceptNewClips = true,
                    ReadOnly = false,
                    AcceptDuplicates = true,
                    SortColumn = -3,
                    SortAscending = false,
                    Encrypted = false,
                    Favorite = false,
                    LastUserId = 1,
                    LastUpdateTime = null,
                    LastKnownCount = 0,
                    Sql = "Select clip.*, shortcut.nickname as Nickname from clip left outer join shortcut on shortcut.clip_GUID = clip.CLIP_GUID where Clip.TimeStamp >= '#DATEMINUSLIMIT#' and del = false order by ID;",
                    CreatedAt = now
                },

                // Everything - All clips
                new Collection
                {
                    Id = Guid.Parse("36418363-48C2-4B71-8157-2000553ACABC"),
                    ParentId = virtualParentId,
                    ParentGuid = virtualParentId,
                    Title = "Everything",
                    LmType = 1,
                    ListType = 3,
                    SortKey = 530,
                    IlIndex = 20,
                    RetentionLimit = 9999,
                    NewClipsGo = 1,
                    AcceptNewClips = true,
                    ReadOnly = false,
                    AcceptDuplicates = true,
                    SortColumn = -3,
                    SortAscending = false,
                    Encrypted = false,
                    Favorite = false,
                    LastUserId = 1,
                    LastUpdateTime = null,
                    LastKnownCount = 0,
                    Sql = "Select clip.*, shortcut.nickname as Nickname from clip left outer join shortcut on shortcut.clip_GUID = clip.CLIP_GUID order by ID;",
                    CreatedAt = now
                },

                // Since Last Import
                new Collection
                {
                    Id = Guid.Parse("09FB405E-6AC9-4500-9384-F7A801AB231C"),
                    ParentId = virtualParentId,
                    ParentGuid = virtualParentId,
                    Title = "Since Last Import",
                    LmType = 1,
                    ListType = 3,
                    SortKey = 540,
                    IlIndex = 48,
                    RetentionLimit = 0,
                    NewClipsGo = 1,
                    AcceptNewClips = true,
                    ReadOnly = false,
                    AcceptDuplicates = true,
                    SortColumn = -3,
                    SortAscending = false,
                    Encrypted = false,
                    Favorite = false,
                    LastUserId = 1,
                    LastUpdateTime = null,
                    LastKnownCount = 0,
                    Sql = "Select clip.*, shortcut.nickname as Nickname from clip left outer join shortcut on shortcut.clip_GUID = clip.CLIP_GUID where Clip.LastModified >= '#DATELASTIMPORT#' and del = false order by ID;",
                    CreatedAt = now
                },

                // Since Last Export
                new Collection
                {
                    Id = Guid.Parse("BCB43DAE-6ACC-4FED-B4E3-49E70F192BF7"),
                    ParentId = virtualParentId,
                    ParentGuid = virtualParentId,
                    Title = "Since Last Export",
                    LmType = 1,
                    ListType = 3,
                    SortKey = 550,
                    IlIndex = 48,
                    RetentionLimit = 31,
                    NewClipsGo = 1,
                    AcceptNewClips = true,
                    ReadOnly = false,
                    AcceptDuplicates = true,
                    SortColumn = -3,
                    SortAscending = false,
                    Encrypted = false,
                    Favorite = false,
                    LastUserId = 1,
                    LastUpdateTime = null,
                    LastKnownCount = 0,
                    Sql = "Select clip.*, shortcut.nickname as Nickname from clip left outer join shortcut on shortcut.clip_GUID = clip.CLIP_GUID where Clip.LastModified >= '#DATELASTEXPORT#' and del = false order by ID;",
                    CreatedAt = now
                },

                // Bitmaps - Only bitmap clips
                new Collection
                {
                    Id = Guid.Parse("A0FBA33A-D501-411D-BCE4-AB1522F6A141"),
                    ParentId = virtualParentId,
                    ParentGuid = virtualParentId,
                    Title = "Bitmaps",
                    LmType = 1,
                    ListType = 3,
                    SortKey = 610,
                    IlIndex = 30,
                    RetentionLimit = 9999,
                    NewClipsGo = 1,
                    AcceptNewClips = true,
                    ReadOnly = false,
                    AcceptDuplicates = true,
                    SortColumn = -3,
                    SortAscending = false,
                    Encrypted = false,
                    Favorite = false,
                    LastUserId = 1,
                    LastUpdateTime = null,
                    LastKnownCount = 0,
                    Sql = "Select clip.*, shortcut.nickname as Nickname from clip left outer join shortcut on shortcut.clip_GUID = clip.CLIP_GUID where del = false and clip.id in (select clip_id from clipdata where ClipData.Format = 2)",
                    CreatedAt = now
                },

                // Keystroke Macros
                new Collection
                {
                    Id = Guid.Parse("1B9F6564-2A21-4500-B46D-7B3A4A40C554"),
                    ParentId = virtualParentId,
                    ParentGuid = virtualParentId,
                    Title = "Keystroke Macros",
                    LmType = 1,
                    ListType = 3,
                    SortKey = 620,
                    IlIndex = 38,
                    RetentionLimit = 9999,
                    NewClipsGo = 1,
                    AcceptNewClips = true,
                    ReadOnly = false,
                    AcceptDuplicates = true,
                    SortColumn = -3,
                    SortAscending = false,
                    Encrypted = false,
                    Favorite = false,
                    LastUserId = 1,
                    LastUpdateTime = null,
                    LastKnownCount = 0,
                    Sql = "Select clip.*, shortcut.nickname as Nickname from clip left outer join shortcut on shortcut.clip_GUID = clip.CLIP_GUID where clip.macro = true",
                    CreatedAt = now
                }
            };

            await _context.Collections.AddRangeAsync(virtualCollections);
            await _context.SaveChangesAsync();

            _logger?.LogInformation("Created {Count} virtual collections", virtualCollections.Count);
            _logger?.LogInformation("Default ClipMate 7.5 collection structure seeded successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to seed default data");
            throw;
        }
    }
}
