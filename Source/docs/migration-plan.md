# ClipMate 7.5 Schema Migration

## Migration Name: `ClipMate75Schema`

This is a **fresh migration** that creates the complete ClipMate 7.5 compatible database schema from scratch.

## What This Migration Creates

### 1. Clips Table (Enhanced)
**New fields added for ClipMate 7.5 compatibility:**
- `Title` (nvarchar(60)) - Custom or auto-generated title
- `Creator` (nvarchar(60)) - Username/workstation
- `SortKey` (int) - Manual sort order
- `SourceUrl` (nvarchar(250)) - URL from browser
- `CustomTitle` (bit) - User customized title flag
- `Locale` (int) - Language/locale code
- `WrapCheck` (bit) - Text wrapping preference
- `Encrypted` (bit) - Encryption flag
- `Icons` (int) - Icon index
- `Del` (bit) - Soft delete flag
- `Size` (int) - Total bytes
- `DelDate` (datetime2) - Deletion timestamp
- `UserId` (int, nullable) - FK to Users
- `Checksum` (int) - For duplicate detection
- `ViewTab` (int) - Preferred view (0=Text, 1=RichText, 2=HTML)
- `Macro` (bit) - Keystroke macro flag
- `LastModified` (datetime2) - Last modification

**Existing fields kept:**
- Id, CollectionId, FolderId, Type, TextContent, RtfContent, HtmlContent
- ImageData, FilePathsJson, ContentHash, SourceApplicationName
- SourceApplicationTitle, CapturedAt, PasteCount, IsFavorite, Label

**New indexes:**
- IX_Clips_Del (soft delete queries)
- IX_Clips_SortKey (manual ordering)
- IX_Clips_Checksum (duplicate detection)

### 2. Collections Table (Unified with Folders)
**ClipMate 7.5 fields added:**
- `ParentId` (uniqueidentifier, nullable) - Hierarchy
- `ParentGuid` (uniqueidentifier, nullable) - Denormalized parent
- `Title` (nvarchar(60)) - Display name (was Name)
- `LmType` (int) - 0=collection, 1=virtual, 2=folder
- `ListType` (int) - 0=normal, 1=virtual, 3=SQL
- `SortKey` (int) - Display order (was SortOrder)
- `IlIndex` (int) - Icon list index
- `RetentionLimit` (int) - Max clips to keep
- `NewClipsGo` (int) - 1=accepts new clips, 0=doesn't
- `AcceptNewClips` (bit) - Whether to accept new clips
- `ReadOnly` (bit) - Read-only flag
- `AcceptDuplicates` (bit) - Allow duplicates
- `SortColumn` (int) - Default sort column (-2=date, -3=custom)
- `SortAscending` (bit) - Sort direction
- `Encrypted` (bit) - Encryption flag
- `Favorite` (bit) - Favorite flag
- `LastUserId` (int, nullable) - Last user who modified
- `LastUpdateTime` (datetime2) - Last modification (was ModifiedAt)
- `LastKnownCount` (int, nullable) - Cached clip count
- `Sql` (nvarchar(256)) - SQL query for virtual collections

**Compatibility mappings:**
- `Name` → maps to `Title`
- `SortOrder` → maps to `SortKey`
- `ModifiedAt` → maps to `LastUpdateTime`
- `IsActive` → computed from `NewClipsGo`

**New indexes:**
- IX_Collections_ParentId (hierarchy queries)
- IX_Collections_LmType (filter by type)
- IX_Collections_NewClipsGo (find active collection)
- IX_Collections_Favorite (quick access)

### 3. ClipData Table (NEW)
Stores clipboard format metadata:
- `Id` (uniqueidentifier, PK)
- `ClipId` (uniqueidentifier, FK to Clips) - Required
- `FormatName` (nvarchar(60)) - "CF_TEXT", "CF_BITMAP", etc.
- `Format` (int) - Windows clipboard format code
- `Size` (int) - Size in bytes
- `StorageType` (int) - 1=TXT, 2=JPG, 3=PNG, 4=BLOB

**Indexes:**
- IX_ClipData_ClipId
- IX_ClipData_Format
- IX_ClipData_StorageType

**Foreign Keys:**
- FK_ClipData_Clips_ClipId (CASCADE DELETE)

### 4. BlobTxt Table (NEW)
Text content storage:
- `Id` (uniqueidentifier, PK)
- `ClipDataId` (uniqueidentifier, FK to ClipData) - Required
- `ClipId` (uniqueidentifier) - Denormalized for performance
- `Data` (nvarchar(max)) - Text content

**Indexes:**
- IX_BlobTxt_ClipId
- IX_BlobTxt_ClipDataId

**Foreign Keys:**
- FK_BlobTxt_ClipData_ClipDataId (CASCADE DELETE)

### 5. BlobJpg Table (NEW)
JPEG image storage:
- `Id` (uniqueidentifier, PK)
- `ClipDataId` (uniqueidentifier, FK to ClipData) - Required
- `ClipId` (uniqueidentifier) - Denormalized
- `Data` (varbinary(max)) - JPEG binary

**Indexes:**
- IX_BlobJpg_ClipId
- IX_BlobJpg_ClipDataId

**Foreign Keys:**
- FK_BlobJpg_ClipData_ClipDataId (CASCADE DELETE)

### 6. BlobPng Table (NEW)
PNG image storage:
- `Id` (uniqueidentifier, PK)
- `ClipDataId` (uniqueidentifier, FK to ClipData) - Required
- `ClipId` (uniqueidentifier) - Denormalized
- `Data` (varbinary(max)) - PNG binary

**Indexes:**
- IX_BlobPng_ClipId
- IX_BlobPng_ClipDataId

**Foreign Keys:**
- FK_BlobPng_ClipData_ClipDataId (CASCADE DELETE)

### 7. BlobBlob Table (NEW)
Generic binary storage:
- `Id` (uniqueidentifier, PK)
- `ClipDataId` (uniqueidentifier, FK to ClipData) - Required
- `ClipId` (uniqueidentifier) - Denormalized
- `Data` (varbinary(max)) - Binary data

**Indexes:**
- IX_BlobBlob_ClipId
- IX_BlobBlob_ClipDataId

**Foreign Keys:**
- FK_BlobBlob_ClipData_ClipDataId (CASCADE DELETE)

### 8. Shortcuts Table (NEW)
PowerPaste nicknames:
- `Id` (uniqueidentifier, PK)
- `ClipId` (uniqueidentifier, FK to Clips) - Required
- `Nickname` (nvarchar(64)) - Required, Unique
- `ClipGuid` (uniqueidentifier) - Denormalized

**Indexes:**
- IX_Shortcuts_Nickname (UNIQUE) - PowerPaste lookup
- IX_Shortcuts_ClipId

**Foreign Keys:**
- FK_Shortcuts_Clips_ClipId (CASCADE DELETE)

### 9. Users Table (NEW)
Multi-user tracking:
- `Id` (uniqueidentifier, PK)
- `Username` (nvarchar(50)) - Required
- `Workstation` (nvarchar(50)) - Required
- `LastDate` (datetime2) - Required

**Indexes:**
- IX_Users_Username
- IX_Users_Workstation

### 10. Templates Table (EXISTING)
No changes - already compatible

### 11. SearchQueries Table (EXISTING)
No changes - already compatible

### 12. ApplicationFilters Table (EXISTING)
No changes - already compatible

### 13. SoundEvents Table (EXISTING)
No changes - already compatible

## Migration Commands

### Create Migration (after reset)
```bash
cd src/ClipMate.Data
dotnet ef migrations add ClipMate75Schema
```

### Apply Migration (automatic on app start)
The migration will be applied automatically via:
- `DatabaseInitializationHostedService`
- Calls `context.Database.EnsureCreatedAsync()`

### Manual Apply (if needed)
```bash
cd src/ClipMate.Data
dotnet ef database update
```

### Rollback (if needed)
```bash
cd src/ClipMate.Data
dotnet ef database update 0  # Removes all migrations
```

## Default Data Seeding

After migration, `DefaultDataSeeder` will run and create:
- 5 root collections (InBox, Safe, Overflow, Samples, Virtual)
- 8 virtual collections (Today, This Week, This Month, Everything, etc.)

All with **exact ClipMate 7.5 GUIDs** for compatibility.

## Table Changes Summary

| Table | Status | Changes |
|-------|--------|---------|
| **Clips** | Modified | +17 fields for ClipMate 7.5 |
| **Collections** | Modified | +21 fields, merged with Folders |
| **Folders** | REMOVED | Merged into Collections (LmType=2) |
| **ClipData** | NEW | Clipboard format metadata |
| **BlobTxt** | NEW | Text BLOB storage |
| **BlobJpg** | NEW | JPEG BLOB storage |
| **BlobPng** | NEW | PNG BLOB storage |
| **BlobBlob** | NEW | Generic BLOB storage |
| **Shortcuts** | NEW | PowerPaste nicknames |
| **Users** | NEW | Multi-user tracking |
| Templates | Unchanged | Existing functionality |
| SearchQueries | Unchanged | Existing functionality |
| ApplicationFilters | Unchanged | Existing functionality |
| SoundEvents | Unchanged | Existing functionality |

## Index Summary

**Total new indexes:** 19
- Clips: +3 (Del, SortKey, Checksum)
- Collections: +4 (ParentId, LmType, NewClipsGo, Favorite)
- ClipData: 3 (ClipId, Format, StorageType)
- BlobTxt: 2 (ClipId, ClipDataId)
- BlobJpg: 2 (ClipId, ClipDataId)
- BlobPng: 2 (ClipId, ClipDataId)
- BlobBlob: 2 (ClipId, ClipDataId)
- Shortcuts: 2 (Nickname-unique, ClipId)
- Users: 2 (Username, Workstation)

## Foreign Key Summary

**Cascade Delete Chains:**
1. Clip deletion → ClipData deleted → BLOB entries deleted
2. Clip deletion → Shortcuts deleted
3. Collection deletion → Child collections deleted (RESTRICT to prevent accidents)

## Testing the Migration

### 1. Verify Schema
```sql
-- Check all tables exist
SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;

-- Check Clips table structure
PRAGMA table_info(Clips);

-- Check Collections include ClipMate 7.5 fields
PRAGMA table_info(Collections);
```

### 2. Verify Default Data
```sql
-- Should return 13 collections
SELECT COUNT(*) FROM Collections;

-- Should show InBox as active (NewClipsGo = 1)
SELECT Title, NewClipsGo FROM Collections WHERE LmType = 0;

-- Should show 8 virtual collections with SQL
SELECT Title, Sql FROM Collections WHERE LmType = 1;
```

### 3. Test BLOB Storage
```sql
-- Create test clip with ClipData
INSERT INTO Clips (Id, ...) VALUES (...);
INSERT INTO ClipData (Id, ClipId, FormatName, Format, Size, StorageType) 
VALUES (newid(), @clipId, 'CF_TEXT', 1, 100, 1);
INSERT INTO BlobTxt (Id, ClipDataId, ClipId, Data) 
VALUES (newid(), @clipDataId, @clipId, 'Test text');

-- Verify cascade delete
DELETE FROM Clips WHERE Id = @clipId;
-- ClipData and BlobTxt should be auto-deleted
```

## Migration File Size Estimate

Expected migration file: **~1500-2000 lines**
- CreateTable statements for 6 new tables
- AlterTable statements for 2 modified tables
- CreateIndex statements for 19 indexes
- AddForeignKey statements for relationships

---

**Ready to generate migration:** Yes
**Breaking changes:** None (new database)
**Data loss risk:** None (fresh start)
**Rollback available:** Yes (via EF Core)
