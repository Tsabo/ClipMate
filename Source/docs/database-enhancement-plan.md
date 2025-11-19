# ClipMate 7.5 Database Compatibility Implementation Plan

## Goal
Create an **exact replica** of ClipMate 7.5's database schema and functionality. No enhancements, no modern conveniences - just feature parity.

## Phase 1: Core Schema Implementation

### 1.1 Update Clip Model to Match ClipMate 7.5

**File:** `src/ClipMate.Core/Models/Clip.cs`

**Required Changes:**
```csharp
public class Clip
{
    // Keep existing
    public Guid Id { get; set; }
    public Guid? CollectionId { get; set; }
    public Guid? FolderId { get; set; }
    public DateTime CapturedAt { get; set; }
    public string? TextContent { get; set; }
    public string? RtfContent { get; set; }
    public string? HtmlContent { get; set; }
    public byte[]? ImageData { get; set; }
    public string? FilePathsJson { get; set; }
    public string ContentHash { get; set; } = "";
    public string? SourceApplicationName { get; set; }
    public string? SourceApplicationTitle { get; set; }
    public int PasteCount { get; set; }
    public bool IsFavorite { get; set; }
    public string? Label { get; set; }
    public ClipType Type { get; set; }
    
    // ADD - ClipMate 7.5 required fields
    public string? Title { get; set; }              // TITLE - 60 chars max
    public string? Creator { get; set; }            // CREATOR - username/workstation
    public int SortKey { get; set; }                // SORTKEY - manual sort order
    public string? SourceUrl { get; set; }          // SOURCEURL - 250 chars max
    public bool CustomTitle { get; set; }           // CUSTOMTITLE - user edited flag
    public int Locale { get; set; }                 // LOCALE - language code
    public bool WrapCheck { get; set; }             // WRAPCHECK - text wrapping
    public bool Encrypted { get; set; }             // ENCRYPTED
    public int Icons { get; set; }                  // ICONS - icon index
    public bool Del { get; set; }                   // DEL - soft delete flag
    public int Size { get; set; }                   // SIZE - total bytes
    public DateTime? DelDate { get; set; }          // DELDATE - deletion timestamp
    public int? UserId { get; set; }                // USER_ID
    public int Checksum { get; set; }               // CHECKSUM (in addition to hash)
    public int ViewTab { get; set; }                // VIEWTAB - preferred view
    public bool Macro { get; set; }                 // MACRO - keystroke macro flag
    public DateTime? LastModified { get; set; }     // LASTMODIFIED
    
    // REMOVE (not in ClipMate 7.5)
    // public DateTime? LastAccessedAt { get; set; }  // DELETE THIS
    // public DateTime? ModifiedAt { get; set; }      // DELETE THIS (use LastModified)
    // public string DisplayTitle { get; ... }         // DELETE THIS (use Title)
}
```

### 1.2 Create ClipData Model

**New File:** `src/ClipMate.Core/Models/ClipData.cs`

```csharp
namespace ClipMate.Core.Models;

/// <summary>
/// Represents metadata for a single clipboard format within a clip.
/// Matches ClipMate 7.5 ClipData table structure.
/// </summary>
public class ClipData
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// FK to parent Clip.
    /// </summary>
    public Guid ClipId { get; set; }
    
    /// <summary>
    /// Format name (e.g., "CF_TEXT", "CF_BITMAP", "CF_HTML").
    /// </summary>
    public string FormatName { get; set; } = string.Empty;
    
    /// <summary>
    /// Windows clipboard format code (e.g., 1=CF_TEXT, 2=CF_BITMAP, 13=CF_UNICODETEXT).
    /// </summary>
    public int Format { get; set; }
    
    /// <summary>
    /// Size of this format's data in bytes.
    /// </summary>
    public int Size { get; set; }
    
    /// <summary>
    /// Storage type indicator:
    /// 1 = BLOBTXT (text formats)
    /// 2 = BLOBJPG (JPEG images)
    /// 3 = BLOBPNG (PNG images)
    /// 4 = BLOBBLOB (other binary)
    /// </summary>
    public int StorageType { get; set; }
    
    // Navigation
    public Clip? Clip { get; set; }
}
```

### 1.3 Create BLOB Storage Models

**New File:** `src/ClipMate.Core/Models/BlobTxt.cs`
```csharp
namespace ClipMate.Core.Models;

public class BlobTxt
{
    public Guid Id { get; set; }
    public Guid ClipDataId { get; set; }
    public Guid ClipId { get; set; }        // Denormalized for performance
    public string Data { get; set; } = string.Empty;
    
    public ClipData? ClipData { get; set; }
}
```

**New File:** `src/ClipMate.Core/Models/BlobJpg.cs`
```csharp
namespace ClipMate.Core.Models;

public class BlobJpg
{
    public Guid Id { get; set; }
    public Guid ClipDataId { get; set; }
    public Guid ClipId { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    
    public ClipData? ClipData { get; set; }
}
```

**New File:** `src/ClipMate.Core/Models/BlobPng.cs`
```csharp
namespace ClipMate.Core.Models;

public class BlobPng
{
    public Guid Id { get; set; }
    public Guid ClipDataId { get; set; }
    public Guid ClipId { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    
    public ClipData? ClipData { get; set; }
}
```

**New File:** `src/ClipMate.Core/Models/BlobBlob.cs`
```csharp
namespace ClipMate.Core.Models;

public class BlobBlob
{
    public Guid Id { get; set; }
    public Guid ClipDataId { get; set; }
    public Guid ClipId { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    
    public ClipData? ClipData { get; set; }
}
```

### 1.4 Merge Collection and Folder Models

ClipMate 7.5 uses a **single COLL table** for both collections and folders, differentiated by `LMTYPE`.

**File:** `src/ClipMate.Core/Models/Collection.cs`

```csharp
namespace ClipMate.Core.Models;

/// <summary>
/// Represents both collections and folders in ClipMate 7.5 architecture.
/// The LMTYPE field determines if this is a collection or folder.
/// </summary>
public class Collection
{
    // Primary Key
    public Guid Id { get; set; }                    // COLL_GUID
    
    // Hierarchy
    public Guid? ParentId { get; set; }             // PARENT_ID
    public Guid? ParentGuid { get; set; }           // PARENT_GUID
    
    // ClipMate 7.5 Fields
    public string Title { get; set; } = string.Empty;  // TITLE (60 chars)
    public int LmType { get; set; }                 // 0=normal, 1=virtual, 2=folder
    public int ListType { get; set; }               // 0=normal, 1=virtual, 3=SQL
    public int SortKey { get; set; }                // SORTKEY
    public int IlIndex { get; set; }                // ILINDEX - icon index
    public int RetentionLimit { get; set; }         // RETENTIONLIMIT
    public int NewClipsGo { get; set; }             // 1=accept new clips
    public bool AcceptNewClips { get; set; }        // ACCEPTNEWCLIPS
    public bool ReadOnly { get; set; }              // READONLY
    public bool AcceptDuplicates { get; set; }      // ACCEPTDUPLICATES
    public int SortColumn { get; set; }             // -2=date, -3=custom
    public bool SortAscending { get; set; }         // SORTASCENDING
    public bool Encrypted { get; set; }             // ENCRYPTED
    public bool Favorite { get; set; }              // FAVORITE
    public int? LastUserId { get; set; }            // LASTUSER_ID
    public DateTime? LastUpdateTime { get; set; }   // LAST_UPDATE_TIME
    public int? LastKnownCount { get; set; }        // LAST_KNOWN_COUNT
    public string? Sql { get; set; }                // SQL (256 chars) - for virtual collections
    
    // Navigation
    public Collection? Parent { get; set; }
    public ICollection<Collection> Children { get; set; } = new List<Collection>();
    
    // Helper properties
    public bool IsFolder => LmType == 2;
    public bool IsVirtual => LmType == 1 || ListType == 1 || ListType == 3;
    public bool IsActive => NewClipsGo == 1;
}
```

**Action:** Delete `src/ClipMate.Core/Models/Folder.cs` - no longer needed

### 1.5 Create Shortcut Model

**New File:** `src/ClipMate.Core/Models/Shortcut.cs`

```csharp
namespace ClipMate.Core.Models;

/// <summary>
/// PowerPaste shortcut/nickname for quick clip access.
/// Matches ClipMate 7.5 shortcut table structure.
/// </summary>
public class Shortcut
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// FK to Clip.
    /// </summary>
    public Guid ClipId { get; set; }
    
    /// <summary>
    /// Nickname (e.g., ".sig", ".addr") - 64 chars max.
    /// </summary>
    public string Nickname { get; set; } = string.Empty;
    
    /// <summary>
    /// Denormalized Clip GUID for performance.
    /// </summary>
    public Guid ClipGuid { get; set; }
    
    // Navigation
    public Clip? Clip { get; set; }
}
```

### 1.6 Create User Model

**New File:** `src/ClipMate.Core/Models/User.cs`

```csharp
namespace ClipMate.Core.Models;

/// <summary>
/// Represents a user/workstation in multi-user scenarios.
/// Matches ClipMate 7.5 users table structure.
/// </summary>
public class User
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Username - 50 chars max.
    /// </summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// Workstation name - 50 chars max.
    /// </summary>
    public string Workstation { get; set; } = string.Empty;
    
    /// <summary>
    /// Last activity timestamp.
    /// </summary>
    public DateTime LastDate { get; set; }
}
```

## Phase 2: Update Database Context

**File:** `src/ClipMate.Data/ClipMateDbContext.cs`

```csharp
public class ClipMateDbContext : DbContext
{
    // Existing
    public DbSet<Clip> Clips => Set<Clip>();
    public DbSet<Collection> Collections => Set<Collection>();
    
    // NEW - Add ClipMate 7.5 tables
    public DbSet<ClipData> ClipData => Set<ClipData>();
    public DbSet<BlobTxt> BlobTxt => Set<BlobTxt>();
    public DbSet<BlobJpg> BlobJpg => Set<BlobJpg>();
    public DbSet<BlobPng> BlobPng => Set<BlobPng>();
    public DbSet<BlobBlob> BlobBlob => Set<BlobBlob>();
    public DbSet<Shortcut> Shortcuts => Set<Shortcut>();
    public DbSet<User> Users => Set<User>();
    
    // REMOVE - No longer needed (merged into Collection)
    // public DbSet<Folder> Folders => Set<Folder>();
    
    // Keep existing
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<SearchQuery> SearchQueries => Set<SearchQuery>();
    public DbSet<ApplicationFilter> ApplicationFilters => Set<ApplicationFilter>();
    public DbSet<SoundEvent> SoundEvents => Set<SoundEvent>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureClip(modelBuilder);
        ConfigureCollection(modelBuilder);
        ConfigureClipData(modelBuilder);
        ConfigureBlobTables(modelBuilder);
        ConfigureShortcut(modelBuilder);
        ConfigureUser(modelBuilder);
        // ... existing configurations
    }
    
    private static void ConfigureClipData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClipData>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FormatName).IsRequired().HasMaxLength(60);
            entity.HasIndex(e => e.ClipId).HasDatabaseName("IX_ClipData_ClipId");
            entity.HasIndex(e => e.Format).HasDatabaseName("IX_ClipData_Format");
        });
    }
    
    private static void ConfigureBlobTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BlobTxt>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ClipId).HasDatabaseName("IX_BlobTxt_ClipId");
            entity.HasIndex(e => e.ClipDataId).HasDatabaseName("IX_BlobTxt_ClipDataId");
        });
        
        // Similar for BlobJpg, BlobPng, BlobBlob
    }
    
    private static void ConfigureShortcut(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Shortcut>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nickname).IsRequired().HasMaxLength(64);
            entity.HasIndex(e => e.Nickname).IsUnique().HasDatabaseName("IX_Shortcuts_Nickname");
            entity.HasIndex(e => e.ClipId).HasDatabaseName("IX_Shortcuts_ClipId");
        });
    }
}
```

## Phase 3: Create Repositories

### 3.1 ClipData Repository

**New File:** `src/ClipMate.Core/Repositories/IClipDataRepository.cs`
**New File:** `src/ClipMate.Data/Repositories/ClipDataRepository.cs`

### 3.2 BLOB Repositories

**New File:** `src/ClipMate.Core/Repositories/IBlobRepository.cs`
**New File:** `src/ClipMate.Data/Repositories/BlobRepository.cs`

### 3.3 Shortcut Repository

**New File:** `src/ClipMate.Core/Repositories/IShortcutRepository.cs`
**New File:** `src/ClipMate.Data/Repositories/ShortcutRepository.cs`

### 3.4 User Repository

**New File:** `src/ClipMate.Core/Repositories/IUserRepository.cs`
**New File:** `src/ClipMate.Data/Repositories/UserRepository.cs`

## Phase 4: Update Services

### 4.1 Update ClipboardCoordinator

Must now:
- Populate `ClipData` table with format metadata
- Store content in appropriate BLOB tables
- Set `Title` field (first line or custom)
- Calculate `Size` field
- Set `Checksum` field
- Populate all ClipMate 7.5 fields

### 4.2 Create ShortcutService

**New File:** `src/ClipMate.Core/Services/IShortcutService.cs`
**New File:** `src/ClipMate.Data/Services/ShortcutService.cs`

Must support PowerPaste nickname lookup.

## Phase 5: Database Migration

**New Migration File:** `Migration_ClipMate75Compatibility.cs`

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Add new Clip fields
    migrationBuilder.AddColumn<string>("Title", "Clips", maxLength: 60, nullable: true);
    migrationBuilder.AddColumn<string>("Creator", "Clips", maxLength: 60, nullable: true);
    migrationBuilder.AddColumn<int>("SortKey", "Clips", nullable: false, defaultValue: 0);
    migrationBuilder.AddColumn<string>("SourceUrl", "Clips", maxLength: 250, nullable: true);
    migrationBuilder.AddColumn<bool>("CustomTitle", "Clips", nullable: false, defaultValue: false);
    migrationBuilder.AddColumn<int>("Locale", "Clips", nullable: false, defaultValue: 0);
    migrationBuilder.AddColumn<bool>("WrapCheck", "Clips", nullable: false, defaultValue: false);
    migrationBuilder.AddColumn<bool>("Encrypted", "Clips", nullable: false, defaultValue: false);
    migrationBuilder.AddColumn<int>("Icons", "Clips", nullable: false, defaultValue: 0);
    migrationBuilder.AddColumn<bool>("Del", "Clips", nullable: false, defaultValue: false);
    migrationBuilder.AddColumn<int>("Size", "Clips", nullable: false, defaultValue: 0);
    migrationBuilder.AddColumn<DateTime>("DelDate", "Clips", nullable: true);
    migrationBuilder.AddColumn<int>("UserId", "Clips", nullable: true);
    migrationBuilder.AddColumn<int>("Checksum", "Clips", nullable: false, defaultValue: 0);
    migrationBuilder.AddColumn<int>("ViewTab", "Clips", nullable: false, defaultValue: 0);
    migrationBuilder.AddColumn<bool>("Macro", "Clips", nullable: false, defaultValue: false);
    migrationBuilder.AddColumn<DateTime>("LastModified", "Clips", nullable: true);
    
    // Drop obsolete columns
    migrationBuilder.DropColumn("LastAccessedAt", "Clips");
    migrationBuilder.DropColumn("ModifiedAt", "Clips");
    
    // Create ClipData table
    migrationBuilder.CreateTable(
        name: "ClipData",
        columns: table => new
        {
            Id = table.Column<Guid>(nullable: false),
            ClipId = table.Column<Guid>(nullable: false),
            FormatName = table.Column<string>(maxLength: 60, nullable: false),
            Format = table.Column<int>(nullable: false),
            Size = table.Column<int>(nullable: false),
            StorageType = table.Column<int>(nullable: false)
        });
    
    // Create BLOB tables
    migrationBuilder.CreateTable("BlobTxt", ...);
    migrationBuilder.CreateTable("BlobJpg", ...);
    migrationBuilder.CreateTable("BlobPng", ...);
    migrationBuilder.CreateTable("BlobBlob", ...);
    
    // Create Shortcut table
    migrationBuilder.CreateTable("Shortcuts", ...);
    
    // Create User table
    migrationBuilder.CreateTable("Users", ...);
    
    // Update Collection table (merge Folder functionality)
    migrationBuilder.AddColumn<Guid>("ParentId", "Collections", nullable: true);
    migrationBuilder.AddColumn<Guid>("ParentGuid", "Collections", nullable: true);
    migrationBuilder.AddColumn<int>("LmType", "Collections", nullable: false, defaultValue: 0);
    migrationBuilder.AddColumn<int>("ListType", "Collections", nullable: false, defaultValue: 0);
    // ... add all other ClipMate 7.5 fields
    
    // Drop Folders table (merged into Collections)
    migrationBuilder.DropTable("Folders");
}
```

## Phase 6: Data Migration from ClipMate 7.5

Create utility to import from ClipMate 7.5 .DAT files:

**New File:** `src/ClipMate.Data/Migration/ClipMate75Importer.cs`

Must support:
- Reading DBISAM .DAT files
- Mapping AutoInc IDs to GUIDs
- Migrating all tables with exact field mapping
- Preserving relationships

## Testing Checklist

- [ ] All ClipMate 7.5 fields present in models
- [ ] Database schema matches ClipMate 7.5
- [ ] Can import from real ClipMate 7.5 database
- [ ] All soft delete functionality works
- [ ] PowerPaste shortcuts work
- [ ] Virtual collections execute SQL queries
- [ ] Multi-user tracking works
- [ ] Icon display works
- [ ] Manual sorting works
- [ ] Encryption works (if implemented)

## Success Criteria

✅ **Feature Parity:** Every feature in ClipMate 7.5 works in new ClipMate
✅ **Data Compatibility:** Can import ClipMate 7.5 databases without data loss
✅ **Exact Behavior:** UI/UX matches ClipMate 7.5 expectations
✅ **No Regressions:** Nothing that worked in ClipMate 7.5 is broken

## Anti-Patterns to Avoid

❌ Don't add "improvements" that weren't in ClipMate 7.5
❌ Don't change field names/types from original
❌ Don't skip "legacy" features thinking they're not needed
❌ Don't optimize before achieving parity
