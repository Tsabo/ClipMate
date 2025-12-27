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
    /// Seeds the database with default ClipMate 7.5 collection structure.
    /// Should only be called for newly created databases.
    /// </summary>
    /// <param name="force">If true, seeds even if collections exist. Use with caution.</param>
    public async Task SeedDefaultDataAsync(bool force = false)
    {
        try
        {
            // Check if collections already exist (unless forced)
            if (!force)
            {
                var existingCollections = await _context.Collections.AnyAsync();
                if (existingCollections)
                {
                    _logger?.LogInformation("Collections already exist, skipping default data seeding");
                    return;
                }
            }

            _logger?.LogInformation("Seeding default ClipMate 7.5 collection structure");

            var now = DateTime.UtcNow;
            var emptyGuid = Guid.Parse("00000000-0000-0000-0000-000000000000");

            // Create root collections
            var collections = new List<Collection>
            {
                // Inbox - Where new clips go by default
                new()
                {
                    Id = Guid.Parse("E21B62F2-4CFA-4913-9B79-4F955F4F202D"),
                    ParentId = null,
                    ParentGuid = emptyGuid,
                    Title = "Inbox",
                    Icon = "📥",
                    LmType = CollectionLmType.Normal,
                    ListType = CollectionListType.Normal,
                    SortKey = 100,
                    IlIndex = 7, // Icon index
                    RetentionLimit = 200,
                    NewClipsGo = 1, // Accept new clips
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
                    CreatedAt = now,
                    Role = CollectionRole.None,
                    MaxAgeDays = 0,
                    MaxBytes = 0,
                    MaxClips = 200,
                },

                // Safe - For important clips (folder type)
                new()
                {
                    Id = Guid.Parse("C297C388-B07F-40B8-9E81-FB668F1562AD"),
                    ParentId = null,
                    ParentGuid = emptyGuid,
                    Title = "Safe",
                    Icon = "🔒",
                    LmType = CollectionLmType.Folder,
                    ListType = CollectionListType.Normal,
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
                    CreatedAt = now,
                    Role = CollectionRole.None,
                    MaxAgeDays = 0,
                    MaxBytes = 0,
                    MaxClips = 200,
                },

                // Overflow - Where clips go when Inbox is full
                new()
                {
                    Id = Guid.Parse("A4FF1FD1-2E7E-426C-8C1D-715E54D1ABC6"),
                    ParentId = null,
                    ParentGuid = emptyGuid,
                    Title = "Overflow",
                    Icon = "📤",
                    LmType = CollectionLmType.Normal,
                    ListType = CollectionListType.Normal,
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
                    CreatedAt = now,
                    Role = CollectionRole.Overflow,
                    MaxAgeDays = 0,
                    MaxBytes = 0,
                    MaxClips = 200,
                },

                // Samples - Sample clips folder
                new()
                {
                    Id = Guid.Parse("253CB828-DF18-4833-ACF8-304DF0511122"),
                    ParentId = null,
                    ParentGuid = emptyGuid,
                    Title = "Samples",
                    Icon = "📋",
                    LmType = CollectionLmType.Folder,
                    ListType = CollectionListType.Normal,
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
                    CreatedAt = now,
                    Role = CollectionRole.None,
                    MaxAgeDays = 0,
                    MaxBytes = 0,
                    MaxClips = 200,
                },

                // Virtual - Parent folder for virtual/smart collections
                new()
                {
                    Id = Guid.Parse("A82DA2A6-86AA-4FC6-A660-2543E7FE900D"),
                    ParentId = null,
                    ParentGuid = emptyGuid,
                    Title = "Virtual",
                    Icon = "✨",
                    LmType = CollectionLmType.Folder,
                    ListType = CollectionListType.Smart,
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
                    CreatedAt = now,
                    Role = CollectionRole.None,
                    MaxAgeDays = 0,
                    MaxBytes = 0,
                    MaxClips = 200,
                },
            };

            await _context.Collections.AddRangeAsync(collections);
            await _context.SaveChangesAsync();

            _logger?.LogInformation("Created {Count} root collections", collections.Count);

            // Create virtual collections (children of "Virtual" folder)
            var virtualParentId = Guid.Parse("A82DA2A6-86AA-4FC6-A660-2543E7FE900D");
            var virtualCollections = new List<Collection>
            {
                // Today - Clips captured today
                new()
                {
                    Id = Guid.Parse("27EBB8C8-FE43-4199-BD92-C953717C4066"),
                    ParentId = virtualParentId,
                    ParentGuid = virtualParentId,
                    Title = "Today",
                    Icon = "📅",
                    LmType = CollectionLmType.Virtual,
                    ListType = CollectionListType.SqlBased,
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
                    Sql = "Select Clips.*, ShortCut.Nickname from Clips left outer join ShortCut on ShortCut.ClipId = Clips.ID where Clips.TimeStamp >= '#DATE#' and Del = false order by ID;",
                    CreatedAt = now,
                    Role = CollectionRole.None,
                    MaxAgeDays = 0,
                    MaxBytes = 0,
                    MaxClips = 200,
                },

                // This Week
                new()
                {
                    Id = Guid.Parse("962983D5-9C1D-43FA-9B70-D258F5AE54E6"),
                    ParentId = virtualParentId,
                    ParentGuid = virtualParentId,
                    Title = "This Week",
                    Icon = "🗓️",
                    LmType = CollectionLmType.Virtual,
                    ListType = CollectionListType.SqlBased,
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
                    Sql = "Select Clips.*, ShortCut.Nickname from Clips left outer join ShortCut on ShortCut.ClipId = Clips.ID where Clips.TimeStamp >= '#DATEMINUSLIMIT#' and Del = false order by ID;",
                    CreatedAt = now,
                    Role = CollectionRole.None,
                    MaxAgeDays = 0,
                    MaxBytes = 0,
                    MaxClips = 200,
                },

                // This Month
                new()
                {
                    Id = Guid.Parse("360D9460-6A7F-48C7-9554-D8E8D36FBFE9"),
                    ParentId = virtualParentId,
                    ParentGuid = virtualParentId,
                    Title = "This Month",
                    Icon = "📆",
                    LmType = CollectionLmType.Virtual,
                    ListType = CollectionListType.SqlBased,
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
                    Sql = "Select Clips.*, ShortCut.Nickname from Clips left outer join ShortCut on ShortCut.ClipId = Clips.ID where Clips.TimeStamp >= '#DATEMINUSLIMIT#' and Del = false order by ID;",
                    CreatedAt = now,
                    Role = CollectionRole.None,
                    MaxAgeDays = 0,
                    MaxBytes = 0,
                    MaxClips = 200,
                },

                // Everything - All clips
                new()
                {
                    Id = Guid.Parse("36418363-48C2-4B71-8157-2000553ACABC"),
                    ParentId = virtualParentId,
                    ParentGuid = virtualParentId,
                    Title = "Everything",
                    Icon = "🌐",
                    LmType = CollectionLmType.Virtual,
                    ListType = CollectionListType.SqlBased,
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
                    Sql = "Select Clips.*, ShortCut.Nickname from Clips left outer join ShortCut on ShortCut.ClipId = Clips.ID order by ID;",
                    CreatedAt = now,
                    Role = CollectionRole.None,
                    MaxAgeDays = 0,
                    MaxBytes = 0,
                    MaxClips = 200,
                },

                // Since Last Import
                new()
                {
                    Id = Guid.Parse("09FB405E-6AC9-4500-9384-F7A801AB231C"),
                    ParentId = virtualParentId,
                    ParentGuid = virtualParentId,
                    Title = "Since Last Import",
                    Icon = "📥",
                    LmType = CollectionLmType.Virtual,
                    ListType = CollectionListType.SqlBased,
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
                    Sql = "Select Clips.*, ShortCut.Nickname from Clips left outer join ShortCut on ShortCut.ClipId = Clips.ID where Clips.LastModified >= '#DATELASTIMPORT#' and Del = false order by ID;",
                    CreatedAt = now,
                    Role = CollectionRole.None,
                    MaxAgeDays = 0,
                    MaxBytes = 0,
                    MaxClips = 200,
                },

                // Since Last Export
                new()
                {
                    Id = Guid.Parse("BCB43DAE-6ACC-4FED-B4E3-49E70F192BF7"),
                    ParentId = virtualParentId,
                    ParentGuid = virtualParentId,
                    Title = "Since Last Export",
                    Icon = "📤",
                    LmType = CollectionLmType.Virtual,
                    ListType = CollectionListType.SqlBased,
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
                    Sql = "Select Clips.*, ShortCut.Nickname from Clips left outer join ShortCut on ShortCut.ClipId = Clips.ID where Clips.LastModified >= '#DATELASTEXPORT#' and Del = false order by ID;",
                    CreatedAt = now,
                    Role = CollectionRole.None,
                    MaxAgeDays = 0,
                    MaxBytes = 0,
                    MaxClips = 200,
                },

                // Bitmaps - Only bitmap clips
                new()
                {
                    Id = Guid.Parse("A0FBA33A-D501-411D-BCE4-AB1522F6A141"),
                    ParentId = virtualParentId,
                    ParentGuid = virtualParentId,
                    Title = "Images",
                    Icon = "🖼️",
                    LmType = CollectionLmType.Virtual,
                    ListType = CollectionListType.SqlBased,
                    SortKey = 530,
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
                    Sql = "Select Clips.*, ShortCut.Nickname from Clips left outer join ShortCut on ShortCut.ClipId = Clips.ID where Del = false and Clips.ID in (select ClipId from ClipData where ClipData.Format = 2)",
                    CreatedAt = now,
                    Role = CollectionRole.None,
                    MaxAgeDays = 0,
                    MaxBytes = 0,
                    MaxClips = 200,
                },

                // Keystroke Macros
                new()
                {
                    Id = Guid.Parse("1B9F6564-2A21-4500-B46D-7B3A4A40C554"),
                    ParentId = virtualParentId,
                    ParentGuid = virtualParentId,
                    Title = "Keystroke Macros",
                    Icon = "⌨️",
                    LmType = CollectionLmType.Virtual,
                    ListType = CollectionListType.SqlBased,
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
                    Sql = "Select Clips.*, ShortCut.Nickname from Clips left outer join ShortCut on ShortCut.ClipId = Clips.ID where Clips.Macro = true",
                    CreatedAt = now,
                    Role = CollectionRole.None,
                    MaxAgeDays = 0,
                    MaxBytes = 0,
                    MaxClips = 200,
                },
            };

            await _context.Collections.AddRangeAsync(virtualCollections);
            await _context.SaveChangesAsync();

            _logger?.LogInformation("Created {Count} virtual collections", virtualCollections.Count);

            // Create sample clips
            var inboxId = Guid.Parse("E21B62F2-4CFA-4913-9B79-4F955F4F202D");
            var samplesId = Guid.Parse("253CB828-DF18-4833-ACF8-304DF0511122");

            var sampleClips = new List<Clip>
            {
                // Welcome clip - goes in Inbox
                new()
                {
                    Id = Guid.Parse("56DF8FED-5E03-4533-B769-6CA26C22A6CC"),
                    CollectionId = inboxId,
                    Title = "Welcome to ClipMate 7! (Sample Clip)",
                    Creator = "ClipMate - Manually Created",
                    CapturedAt = now,
                    SortKey = 100,
                    SourceUrl = "http://www.thornsoft.com",
                    CustomTitle = false,
                    Locale = 0,
                    WrapCheck = false,
                    Encrypted = false,
                    Del = false,
                    Size = 561,
                    DelDate = null,
                    UserId = 0,
                    Checksum = 0,
                    ViewTab = 0,
                    Macro = false,
                    LastModified = now,
                    Type = ClipType.Text,
                    TextContent =
                        "Welcome to ClipMate 7!\r\n\r\nThis is a test clip, which you can paste anywhere, \r\nuse it for editing practice, spelling praktice (intentionally \r\nmisspelled - right-click to correct), or discard. \r\n\r\nYou can paste this clip into any application that can paste TEXT from the clipboard.  \r\nOf course, ClipMate can also work with Bitmaps, HTML, Rich Text, and other formats.  \r\nCopy some data from anywhere, and you'll see it show up here.\r\n\r\nI hope you enjoy ClipMate 7!\r\n\r\nSincerely,\r\n\r\nChris Thornton, President\r\nThornsoft Development, Inc.\r\nhttp://www.thornsoft.com\r\n",
                    ContentHash = "",
                },

                // Demo showing @ symbol - Macro clip in Samples
                new()
                {
                    Id = Guid.Parse("D656DC27-ABF7-4C3C-9FC0-35E3D42F7BC6"),
                    CollectionId = samplesId,
                    Title = "Demo showing @ symbol",
                    Creator = "CLIPMATE",
                    CapturedAt = new DateTime(2008, 1, 28, 11, 17, 8),
                    SortKey = 200,
                    SourceUrl = null,
                    CustomTitle = false,
                    Locale = 1033,
                    WrapCheck = false,
                    Encrypted = false,
                    Del = false,
                    Size = 428,
                    DelDate = null,
                    UserId = 0,
                    Checksum = 0,
                    ViewTab = 0,
                    Macro = true,
                    LastModified = new DateTime(2008, 1, 28, 11, 17, 36),
                    Type = ClipType.Text,
                    TextContent =
                        "Demo showing {@} symbol:\r\n#PAUSE#, Pauses, Left/Right navigation:\r\n#PAUSE#{ENTER}E-Mail Address:\r\n#PAUSE# me{@}mydomain.com\r\n#PAUSE#{TAB}MyPassword{ENTER}\r\nFor More Information, See:{ENTER}\r\nhttp://www.thornsoft.com/HTML_help/7/ue_macro_pasting.htm #PAUSE# On The Web!\r\n#PAUSE#{BACKSPACE}\r\n#PAUSE#{BACKSPACE}\r\n#PAUSE#{BACKSPACE}W\r\n#PAUSE#W\r\n#PAUSE#!{ENTER}\r\nThat is all\r\n#PAUSE#{LEFT 7}\r\n#PAUSE#{DEL 2}\r\n#PAUSE#'\r\n#PAUSE#{RIGHT 5}\r\n#PAUSE# Folks!{ENTER}",
                    ContentHash = "",
                },

                // HTML Clip in Samples
                new()
                {
                    Id = Guid.Parse("F6B8F72A-B671-445C-A9FB-198D4EE336DB"),
                    CollectionId = samplesId,
                    Title = "HTML Clip (will contact internet to retrieve graphic)",
                    Creator = "FIREFOX",
                    CapturedAt = new DateTime(2006, 6, 29, 14, 5, 44),
                    SortKey = 300,
                    SourceUrl = "http://www.thornsoft.com/trophy_case.htm",
                    CustomTitle = false,
                    Locale = 1033,
                    WrapCheck = false,
                    Encrypted = false,
                    Del = false,
                    Size = 936,
                    DelDate = null,
                    UserId = 0,
                    Checksum = 0,
                    ViewTab = 5,
                    Macro = false,
                    LastModified = new DateTime(2008, 1, 28, 11, 19, 34),
                    Type = ClipType.Html,
                    TextContent =
                        "E-Commerce Times\r\n\"its power and utility remains unmatched in its category\" \t\"ClipMate is being taken by Thornsoft into domains far beyond those of earlier, simpler clipboard extenders.\"\r\nJohn P. Mello Jr., E-Commerce Times(March 8, 2006)",
                    ContentHash = "",
                },

                // Password Demo - Macro clip in Samples
                new()
                {
                    Id = Guid.Parse("6138B5B6-5FBC-4C3E-84F1-9E7369DECBEB"),
                    CollectionId = samplesId,
                    Title = "Password Demo",
                    Creator = "ClipMate - Manually Created",
                    CapturedAt = new DateTime(2006, 6, 29, 14, 2, 41),
                    SortKey = 400,
                    SourceUrl = null,
                    CustomTitle = false,
                    Locale = 0,
                    WrapCheck = false,
                    Encrypted = false,
                    Del = false,
                    Size = 30,
                    DelDate = null,
                    UserId = 0,
                    Checksum = 0,
                    ViewTab = 0,
                    Macro = true,
                    LastModified = new DateTime(2008, 1, 28, 11, 19, 34),
                    Type = ClipType.Text,
                    TextContent = "MyUserID{TAB}MyPassword{ENTER}",
                    ContentHash = "",
                },

                // Macro clip demonstration in Samples
                new()
                {
                    Id = Guid.Parse("897CE1B4-7400-4BA4-9568-B705DB4BA9B5"),
                    CollectionId = samplesId,
                    Title = "Here is a \"Macro\" clip",
                    Creator = "Manually Created",
                    CapturedAt = new DateTime(2006, 6, 29, 13, 57, 36),
                    SortKey = 500,
                    SourceUrl = "http://www.thornsoft.com",
                    CustomTitle = false,
                    Locale = 0,
                    WrapCheck = false,
                    Encrypted = false,
                    Del = false,
                    Size = 739,
                    DelDate = null,
                    UserId = 0,
                    Checksum = 0,
                    ViewTab = 0,
                    Macro = true,
                    LastModified = new DateTime(2008, 1, 28, 11, 19, 34),
                    Type = ClipType.Text,
                    TextContent =
                        "Here is a \"Macro\" clip, which contains special commands.{ENTER}The commands will be pasted as plain text unless this clip is tagged as a \"macro\" clip,{ENTER}\r\nif you paste it with QuickPaste. You can set it as a macro in the Clip{ENTER}\r\nProperties dialog, or you can add the \"toggle macro\" button to the editor toolbar.{ENTER}\r\nHere are some of the macro commands that you can use:{ENTER}\r\n{ENTER}\r\nClip Capture Date: #DATE#{TAB}#TIME#{ENTER}\r\nSource \"creator\" app: #CREATOR#{ENTER}\r\nClip Title: #TITLE#{ENTER}\r\nURL: #URL#{ENTER}\r\nRight now: #CURRENTDATE#{TAB}#CURRENTTIME#{ENTER}\r\nSequence number: #SEQUENCE#{ENTER}\r\nTabs: [1t{TAB}][2t{TAB}{TAB}][3t{TAB}{TAB}{TAB}]{ENTER}\r\nEnter Key: {ENTER}\r\nPause: #PAUSE#{ENTER}\r\nAll Done!{ENTER}\r\n",
                    ContentHash = "",
                },
            };

            await _context.Clips.AddRangeAsync(sampleClips);
            await _context.SaveChangesAsync();

            _logger?.LogInformation("Created {Count} sample clips", sampleClips.Count);

            // Create ClipData entries for the sample clips
            var clipData1 = Guid.NewGuid();
            var clipData2 = Guid.NewGuid();
            var clipData3 = Guid.NewGuid();
            var clipData4 = Guid.NewGuid();
            var clipData5 = Guid.NewGuid();
            var clipData6 = Guid.NewGuid();

            var clipDataEntries = new List<ClipData>
            {
                // Welcome clip - TEXT format
                new()
                {
                    Id = clipData1,
                    ClipId = Guid.Parse("56DF8FED-5E03-4533-B769-6CA26C22A6CC"),
                    FormatName = "TEXT",
                    Format = 1,
                    Size = 561,
                    StorageType = 0,
                },

                // Demo showing @ symbol - TEXT format
                new()
                {
                    Id = clipData2,
                    ClipId = Guid.Parse("D656DC27-ABF7-4C3C-9FC0-35E3D42F7BC6"),
                    FormatName = "TEXT",
                    Format = 1,
                    Size = 428,
                    StorageType = 0,
                },

                // HTML Clip - HTML Format
                new()
                {
                    Id = clipData3,
                    ClipId = Guid.Parse("F6B8F72A-B671-445C-A9FB-198D4EE336DB"),
                    FormatName = "HTML Format",
                    Format = -3,
                    Size = 936,
                    StorageType = 0,
                },

                // HTML Clip - TEXT format
                new()
                {
                    Id = clipData4,
                    ClipId = Guid.Parse("F6B8F72A-B671-445C-A9FB-198D4EE336DB"),
                    FormatName = "TEXT",
                    Format = 1,
                    Size = 240,
                    StorageType = 0,
                },

                // Password Demo - TEXT format
                new()
                {
                    Id = clipData5,
                    ClipId = Guid.Parse("6138B5B6-5FBC-4C3E-84F1-9E7369DECBEB"),
                    FormatName = "TEXT",
                    Format = 1,
                    Size = 30,
                    StorageType = 0,
                },

                // Macro clip - TEXT format
                new()
                {
                    Id = clipData6,
                    ClipId = Guid.Parse("897CE1B4-7400-4BA4-9568-B705DB4BA9B5"),
                    FormatName = "TEXT",
                    Format = 1,
                    Size = 739,
                    StorageType = 0,
                },
            };

            await _context.ClipData.AddRangeAsync(clipDataEntries);
            await _context.SaveChangesAsync();

            _logger?.LogInformation("Created {Count} ClipData entries", clipDataEntries.Count);

            // Create BlobBlob entry for HTML content
            var htmlClipId = Guid.Parse("F6B8F72A-B671-445C-A9FB-198D4EE336DB");
            var blobBlob = new BlobBlob
            {
                Id = Guid.NewGuid(),
                ClipDataId = clipData3,
                ClipId = htmlClipId,
                Data =
                    "Version:0.9\r\nStartHTML:00000149\r\nEndHTML:00000903\r\nStartFragment:00000183\r\nEndFragment:00000867\r\nSourceURL:http://www.thornsoft.com/trophy_case.htm\r\n<html><body>\r\n<!--StartFragment--><td bordercolorlight=\"#FFFFFF\" bordercolordark=\"#000000\" align=\"center\" height=\"90\"><img src=\"http://www.thornsoft.com/images/news.gif\" border=\"0\" height=\"32\" width=\"32\"><br>\r\nE-Commerce Times<br>\r\n<b>\"</b><span name=\"intelliTxt\" id=\"intelliTxt\">its power and utility \r\nremains unmatched in its category</span><b>\"</b></td>\r\n<td bordercolorlight=\"#FFFFFF\" bordercolordark=\"#000000\" height=\"90\"><i>\"ClipMate \r\nis being taken by Thornsoft into domains far beyond those of earlier, simpler \r\nclipboard extenders.\"</i><br>\r\nJohn P. Mello Jr.<em style=\"font-style: normal;\">,\r\n<a href=\"http://www.ecommercetimes.com/story/reviews/49185.html\">E-Commerce \r\nTimes</a>(March 8, 2006)</em></td><!--EndFragment-->\r\n</body>\r\n</html> "u8
                        .ToArray(),
            };

            await _context.BlobBlob.AddAsync(blobBlob);
            await _context.SaveChangesAsync();

            _logger?.LogInformation("Created BlobBlob entry for HTML content");

            // Create BlobTxt entries for all text content
            var blobTxtEntries = new List<BlobTxt>
            {
                // Welcome clip
                new()
                {
                    Id = Guid.NewGuid(),
                    ClipDataId = clipData1,
                    ClipId = Guid.Parse("56DF8FED-5E03-4533-B769-6CA26C22A6CC"),
                    Data =
                        "Welcome to ClipMate 7!\r\n\r\nThis is a test clip, which you can paste anywhere, \r\nuse it for editing practice, spelling praktice (intentionally \r\nmisspelled - right-click to correct), or discard. \r\n\r\nYou can paste this clip into any application that can paste TEXT from the clipboard.  \r\nOf course, ClipMate can also work with Bitmaps, HTML, Rich Text, and other formats.  \r\nCopy some data from anywhere, and you'll see it show up here.\r\n\r\nI hope you enjoy ClipMate 7!\r\n\r\nSincerely,\r\n\r\nChris Thornton, President\r\nThornsoft Development, Inc.\r\nhttp://www.thornsoft.com\r\n",
                },

                // Demo showing @ symbol
                new()
                {
                    Id = Guid.NewGuid(),
                    ClipDataId = clipData2,
                    ClipId = Guid.Parse("D656DC27-ABF7-4C3C-9FC0-35E3D42F7BC6"),
                    Data =
                        "Demo showing {@} symbol:\r\n#PAUSE#, Pauses, Left/Right naviagation:\r\n#PAUSE#{ENTER}E-Mail Address:\r\n#PAUSE# me{@}mydomain.com\r\n#PAUSE#{TAB}MyPassword{ENTER}\r\nFor More Information, See:{ENTER}\r\nhttp://www.thornsoft.com/HTML_help/7/ue_macro_pasting.htm #PAUSE# On The Web!\r\n#PAUSE#{BACKSPACE}\r\n#PAUSE#{BACKSPACE}\r\n#PAUSE#{BACKSPACE}W\r\n#PAUSE#W\r\n#PAUSE#!{ENTER}\r\nThat is all\r\n#PAUSE#{LEFT 7}\r\n#PAUSE#{DEL 2}\r\n#PAUSE#'\r\n#PAUSE#{RIGHT 5}\r\n#PAUSE# Folks!{ENTER}",
                },

                // HTML clip text format
                new()
                {
                    Id = Guid.NewGuid(),
                    ClipDataId = clipData4,
                    ClipId = Guid.Parse("F6B8F72A-B671-445C-A9FB-198D4EE336DB"),
                    Data =
                        "E-Commerce Times\r\n\"its power and utility remains unmatched in its category\" \t\"ClipMate is being taken by Thornsoft into domains far beyond those of earlier, simpler clipboard extenders.\"\r\nJohn P. Mello Jr., E-Commerce Times(March 8, 2006)",
                },

                // Password Demo
                new()
                {
                    Id = Guid.NewGuid(),
                    ClipDataId = clipData5,
                    ClipId = Guid.Parse("6138B5B6-5FBC-4C3E-84F1-9E7369DECBEB"),
                    Data = "MyUserID{TAB}MyPassword{ENTER}",
                },

                // Macro clip
                new()
                {
                    Id = Guid.NewGuid(),
                    ClipDataId = clipData6,
                    ClipId = Guid.Parse("897CE1B4-7400-4BA4-9568-B705DB4BA9B5"),
                    Data =
                        "Here is a \"Macro\" clip, which contains special commands.{ENTER}The commands will be pasted as plain text unless this clip is tagged as a \"macro\" clip,{ENTER}\r\nif you paste it with QuickPaste. You can set it as a macro in the Clip{ENTER}\r\nProperties dialog, or you can add the \"toggle macro\" button to the editor toolbar.{ENTER}\r\nHere are some of the macro commands that you can use:{ENTER}\r\n{ENTER}\r\nClip Capture Date: #DATE#{TAB}#TIME#{ENTER}\r\nSource \"creator\" app: #CREATOR#{ENTER}\r\nClip Title: #TITLE#{ENTER}\r\nURL: #URL#{ENTER}\r\nRight now: #CURRENTDATE#{TAB}#CURRENTTIME#{ENTER}\r\nSequence number: #SEQUENCE#{ENTER}\r\nTabs: [1t{TAB}][2t{TAB}{TAB}][3t{TAB}{TAB}{TAB}]{ENTER}\r\nEnter Key: {ENTER}\r\nPause: #PAUSE#{ENTER}\r\nAll Done!{ENTER}\r\n",
                },
            };

            await _context.BlobTxt.AddRangeAsync(blobTxtEntries);
            await _context.SaveChangesAsync();

            _logger?.LogInformation("Created {Count} BlobTxt entries", blobTxtEntries.Count);

            // Create shortcut for Welcome clip
            var welcomeClipId = Guid.Parse("56DF8FED-5E03-4533-B769-6CA26C22A6CC");
            var shortcut = new Shortcut
            {
                Id = Guid.NewGuid(),
                ClipId = welcomeClipId,
                Nickname = ".s.welcome",
                ClipGuid = welcomeClipId,
            };

            await _context.Shortcuts.AddAsync(shortcut);
            await _context.SaveChangesAsync();

            _logger?.LogInformation("Created shortcut for Welcome clip");
            _logger?.LogInformation("Default ClipMate 7.5 collection structure and sample data seeded successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to seed default data");

            throw;
        }
    }
}
