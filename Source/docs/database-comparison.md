# ClipMate Database Structure Comparison

## Executive Summary

This document compares the ClipMate 7.5 database structure with the new ClipMate implementation to ensure **exact feature parity**. The goal is to replicate ClipMate 7.5's functionality exactly before adding any new features.

## Table Comparison

### 1. CLIP Table (ClipMate 7.5)

**ClipMate 7.5 Fields:**
```sql
ID (AutoInc) - Primary key
COLLECTION_ID (INTEGER) - Which collection owns this clip
TITLE (CHAR(60)) - Custom title or first line
CREATOR (CHAR(60)) - Username/workstation that created it
TIMESTAMP (DATE) - When captured
SORTKEY (INTEGER) - Manual sort order
SOURCEURL (CHAR(250)) - URL if from browser
CUSTOMTITLE (BOOLEAN) - Whether user customized the title
LOCALE (INTEGER) - Language/locale setting
WRAPCHECK (BOOLEAN) - Text wrapping preference
ENCRYPTED (BOOLEAN) - Whether content is encrypted
ICONS (INTEGER) - Icon index for display
DEL (BOOLEAN) - Soft delete flag
SIZE (INTEGER) - Total size in bytes
DELDATE (DATE) - When deleted (for trash)
USER_ID (INTEGER) - FK to users table
CHECKSUM (INTEGER) - For duplicate detection
VIEWTAB (INTEGER) - Which tab to show (text/rtf/html)
MACRO (BOOLEAN) - Whether this is a keystroke macro
CLIP_GUID (CHAR(38)) - GUID for cross-database sync
COLL_GUID (CHAR(38)) - Collection GUID
LASTMODIFIED (DATE) - Last modification time
```

**Current Implementation (Clip.cs):**
```csharp
✓ Id (Guid) - Primary key [Maps to CLIP_GUID internally]
✓ Type (ClipType enum) - Content type [Derived from formats]
✓ TextContent (string?) - Plain text
✓ RtfContent (string?) - Rich text
✓ HtmlContent (string?) - HTML
✓ ImageData (byte[]?) - Binary image
✓ FilePathsJson (string?) - File paths as JSON
✓ ContentHash (string) - SHA256 hash [Maps to CHECKSUM]
✓ SourceApplicationName (string?) - Process name
✓ SourceApplicationTitle (string?) - Window title
✓ CapturedAt (DateTime) - When captured [Maps to TIMESTAMP]
✓ ModifiedAt (DateTime?) - Last modification [Maps to LASTMODIFIED]
✗ LastAccessedAt (DateTime?) - NOT IN ORIGINAL (remove)
✓ PasteCount (int) - Usage statistics
✓ IsFavorite (bool) - Favorite flag
✓ Label (string?) - User label/tag
✓ CollectionId (Guid?) - FK to collection [Maps to COLLECTION_ID]
✓ FolderId (Guid?) - FK to folder
✗ DisplayTitle (computed) - NOT IN ORIGINAL (use TITLE field instead)
```

### Required Changes to Match ClipMate 7.5

| ClipMate 7.5 Field | Current Status | Action Required |
|--------------------|----------------|-----------------|
| **COLLECTION_ID** | ✓ `CollectionId` | Keep as Guid (internal), map to integer externally |
| **TITLE** | ❌ Missing | **ADD** `Title` field (string, 60 chars) - stores first line or custom title |
| **CREATOR** | ❌ Missing | **ADD** `Creator` field (string, 60 chars) - username/workstation |
| **TIMESTAMP** | ✓ `CapturedAt` | Keep |
| **SORTKEY** | ❌ Missing | **ADD** `SortKey` field (int) - for manual sorting |
| **SOURCEURL** | ❌ Missing | **ADD** `SourceUrl` field (string, 250 chars) |
| **CUSTOMTITLE** | ❌ Missing | **ADD** `CustomTitle` field (bool) - whether user set title |
| **LOCALE** | ❌ Missing | **ADD** `Locale` field (int) - language/locale code |
| **WRAPCHECK** | ❌ Missing | **ADD** `WrapCheck` field (bool) - text wrapping preference |
| **ENCRYPTED** | ❌ Missing | **ADD** `Encrypted` field (bool) |
| **ICONS** | ❌ Missing | **ADD** `Icons` field (int) - icon index |
| **DEL** | ❌ Missing | **ADD** `Del` field (bool) - soft delete flag |
| **SIZE** | ❌ Missing | **ADD** `Size` field (int) - total bytes |
| **DELDATE** | ❌ Missing | **ADD** `DelDate` field (DateTime?) - deletion timestamp |
| **USER_ID** | ❌ Missing | **ADD** `UserId` field (int?) - FK to users table |
| **CHECKSUM** | ✓ `ContentHash` | Keep (more robust than int checksum) |
| **VIEWTAB** | ❌ Missing | **ADD** `ViewTab` field (int) - which tab to show |
| **MACRO** | ❌ Missing | **ADD** `Macro` field (bool) - keystroke macro flag |
| **CLIP_GUID** | ✓ `Id` | Keep as Guid |
| **COLL_GUID** | ✓ `CollectionId` | Keep as Guid |
| **LASTMODIFIED** | ✓ `ModifiedAt` | Keep |
| **LastAccessedAt** | ❌ NOT IN ORIGINAL | **REMOVE** - not in ClipMate 7.5 |

---

## 2. ClipData Table (ClipMate 7.5)

**Purpose:** Stores metadata about different clipboard formats for each clip

```sql
ID (AutoInc) - Primary key
CLIP_ID (INTEGER) - FK to CLIP
FORMAT_NAME (CHAR(60)) - "CF_TEXT", "CF_BITMAP", "CF_HTML", etc.
FORMAT (INTEGER) - Windows clipboard format code (1=CF_TEXT, 2=CF_BITMAP, etc.)
SIZE (INTEGER) - Size of this format in bytes
STORAGE_TYPE (INTEGER) - Which BLOB table stores the data (1=TXT, 2=JPG, 3=PNG, 4=BLOB)
```

**Current Implementation:**
- ❌ **MISSING TABLE ENTIRELY**
- Currently: All formats stored inline in Clip table
- Problem: Cannot track multiple clipboard formats per clip

**Action Required:**
```csharp
// ADD NEW MODEL
public class ClipData
{
    public Guid Id { get; set; }
    public Guid ClipId { get; set; }        // FK to Clip
    public string FormatName { get; set; }   // "CF_TEXT", "CF_BITMAP", etc.
    public int Format { get; set; }          // Windows clipboard format code
    public int Size { get; set; }            // Size in bytes
    public int StorageType { get; set; }     // 1=TXT, 2=JPG, 3=PNG, 4=BLOB
    
    // Navigation
    public Clip? Clip { get; set; }
}
```

---

## 3. BLOB Tables (ClipMate 7.5)

**Tables:** BLOBTXT, BLOBJPG, BLOBPNG, BLOBBLOB

**Purpose:** Store actual clipboard data, separated by type for performance

**BLOBTXT:**
```sql
ID (AutoInc)
CLIPDATA_ID (INTEGER) - FK to ClipData
CLIP_ID (INTEGER) - FK to CLIP (denormalized)
DATA (MEMO) - Text content
```

**BLOBJPG, BLOBPNG, BLOBBLOB:**
```sql
ID (AutoInc)
CLIPDATA_ID (INTEGER) - FK to ClipData
CLIP_ID (INTEGER) - FK to CLIP (denormalized)
DATA (BLOB) - Binary content
```

**Current Implementation:**
- Stored inline in Clip table (`TextContent`, `ImageData`, etc.)

**Action Required:**
```csharp
// ADD NEW MODELS
public class BlobTxt
{
    public Guid Id { get; set; }
    public Guid ClipDataId { get; set; }
    public Guid ClipId { get; set; }      // Denormalized for query performance
    public string Data { get; set; }       // Text content
}

public class BlobJpg
{
    public Guid Id { get; set; }
    public Guid ClipDataId { get; set; }
    public Guid ClipId { get; set; }
    public byte[] Data { get; set; }       // JPEG binary
}

public class BlobPng
{
    public Guid Id { get; set; }
    public Guid ClipDataId { get; set; }
    public Guid ClipId { get; set; }
    public byte[] Data { get; set; }       // PNG binary
}

public class BlobBlob
{
    public Guid Id { get; set; }
    public Guid ClipDataId { get; set; }
    public Guid ClipId { get; set; }
    public byte[] Data { get; set; }       // Generic binary
}
```

---

## 4. COLL Table (ClipMate 7.5)

**ClipMate 7.5 Fields:**
```sql
ID (AutoInc)
PARENT_ID (INTEGER) - For hierarchical collections
TITLE (CHAR(60)) - Collection name
LMTYPE (INTEGER) - List mode type (0=normal, 1=virtual, 2=folder)
LISTTYPE (INTEGER) - List type (0=normal, 1=smart/virtual, 3=SQL-based)
SORTKEY (INTEGER) - Display order
ILINDEX (INTEGER) - Icon list index
RETENTIONLIMIT (INTEGER) - Max clips to keep
NEWCLIPSGO (INTEGER) - Where new clips go (1=here, 0=elsewhere)
ACCEPTNEWCLIPS (BOOLEAN) - Whether to accept new clips
READONLY (BOOLEAN) - Read-only flag
ACCEPTDUPLICATES (BOOLEAN) - Allow duplicate clips
SORTCOLUMN (INTEGER) - Default sort column (-2=date, -3=custom)
SORTASCENDING (BOOLEAN) - Sort direction
ENCRYPTED (BOOLEAN) - Encryption flag
FAVORITE (BOOLEAN) - Favorite flag
LASTUSER_ID (INTEGER) - Last user who modified
LAST_UPDATE_TIME (DATE) - Last modification
LAST_KNOWN_COUNT (INTEGER) - Cached clip count
SQL (CHAR(256)) - SQL query for virtual collections
COLL_GUID (CHAR(38)) - Collection GUID
PARENT_GUID (CHAR(38)) - Parent collection GUID
```

**Current Implementation:**
```csharp
// Collection.cs
✓ Id (Guid) - Maps to COLL_GUID
✓ Name (string) - Maps to TITLE
✓ Description (string?) - NOT IN ORIGINAL
✓ SortOrder (int) - Maps to SORTKEY
✓ IsActive (bool) - Maps to NEWCLIPSGO
✓ CreatedAt (DateTime) - NOT IN ORIGINAL
✓ ModifiedAt (DateTime?) - Maps to LAST_UPDATE_TIME

// Folder.cs (partial mapping)
✓ Id (Guid)
✓ Name (string)
✓ CollectionId (Guid)
✓ ParentFolderId (Guid?) - Maps to PARENT_ID
✓ SortOrder (int) - Maps to SORTKEY
✓ CreatedAt (DateTime) - NOT IN ORIGINAL
✓ ModifiedAt (DateTime?) - NOT IN ORIGINAL
✓ IsSystemFolder (bool)
✓ FolderType (FolderType enum)
✓ IconName (string?) - Maps to ILINDEX
```

### Required Changes

**Collections and Folders are MERGED in ClipMate 7.5** - they use the same COLL table!

| ClipMate 7.5 Field | Action Required |
|--------------------|-----------------|
| **PARENT_ID** | **ADD** to Collection (Guid?) - for hierarchical structure |
| **TITLE** | ✓ Covered by `Name` |
| **LMTYPE** | **ADD** `LmType` (int) - list mode type |
| **LISTTYPE** | **ADD** `ListType` (int) - 0=normal, 1=virtual, 3=SQL |
| **SORTKEY** | ✓ Covered by `SortOrder` |
| **ILINDEX** | **ADD** `IlIndex` (int) - icon list index |
| **RETENTIONLIMIT** | **ADD** `RetentionLimit` (int) |
| **NEWCLIPSGO** | **ADD** `NewClipsGo` (int) |
| **ACCEPTNEWCLIPS** | **ADD** `AcceptNewClips` (bool) |
| **READONLY** | **ADD** `ReadOnly` (bool) |
| **ACCEPTDUPLICATES** | **ADD** `AcceptDuplicates` (bool) |
| **SORTCOLUMN** | **ADD** `SortColumn` (int) |
| **SORTASCENDING** | **ADD** `SortAscending` (bool) |
| **ENCRYPTED** | **ADD** `Encrypted` (bool) |
| **FAVORITE** | **ADD** `Favorite` (bool) |
| **LASTUSER_ID** | **ADD** `LastUserId` (int?) |
| **LAST_UPDATE_TIME** | ✓ Covered by `ModifiedAt` |
| **LAST_KNOWN_COUNT** | **ADD** `LastKnownCount` (int?) |
| **SQL** | **ADD** `Sql` (string, 256 chars) |
| **COLL_GUID** | ✓ Covered by `Id` |
| **PARENT_GUID** | **ADD** `ParentGuid` (Guid?) |

**Architecture Decision:** Merge Collection and Folder into single entity to match ClipMate 7.5

---

## 5. shortcut Table (ClipMate 7.5)

**Purpose:** PowerPaste shortcuts (nicknames) for quick clip access

```sql
ID (AutoInc)
CLIP_ID (INTEGER) - FK to CLIP
NICKNAME (CHAR(64)) - ".s.welcome", ".sig", etc.
CLIP_GUID (CHAR(38)) - Clip GUID (denormalized)
```

**Current Implementation:**
- ❌ **MISSING TABLE ENTIRELY**

**Action Required:**
```csharp
// ADD NEW MODEL
public class Shortcut
{
    public Guid Id { get; set; }
    public Guid ClipId { get; set; }       // FK to Clip
    public string Nickname { get; set; }    // ".sig", ".addr", max 64 chars
    public Guid ClipGuid { get; set; }      // Denormalized clip GUID
    
    // Navigation
    public Clip? Clip { get; set; }
}
```

---

## 6. users Table (ClipMate 7.5)

**Purpose:** Track users and workstations

```sql
ID (AutoInc)
USERNAME (CHAR(50))
WORKSTATION (CHAR(50))
LASTDATE (DATE)
```

**Current Implementation:**
- ❌ **MISSING TABLE ENTIRELY**

**Action Required:**
```csharp
// ADD NEW MODEL
public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; }      // Max 50 chars
    public string Workstation { get; set; }   // Max 50 chars
    public DateTime LastDate { get; set; }
}
```

---

## Exact Clip Model for ClipMate 7.5 Compatibility

```csharp
public class Clip
{
    // Primary Key
    public Guid Id { get; set; }                      // CLIP_GUID
    
    // Foreign Keys
    public Guid? CollectionId { get; set; }           // COLL_GUID
    public Guid? FolderId { get; set; }               // For hierarchy
    public int? UserId { get; set; }                  // USER_ID
    
    // ClipMate 7.5 Fields - EXACT MATCH
    public string? Title { get; set; }                // TITLE (60 chars)
    public string? Creator { get; set; }              // CREATOR (60 chars)
    public DateTime CapturedAt { get; set; }          // TIMESTAMP
    public int SortKey { get; set; }                  // SORTKEY
    public string? SourceUrl { get; set; }            // SOURCEURL (250 chars)
    public bool CustomTitle { get; set; }             // CUSTOMTITLE
    public int Locale { get; set; }                   // LOCALE
    public bool WrapCheck { get; set; }               // WRAPCHECK
    public bool Encrypted { get; set; }               // ENCRYPTED
    public int Icons { get; set; }                    // ICONS
    public bool Del { get; set; }                     // DEL (soft delete)
    public int Size { get; set; }                     // SIZE
    public DateTime? DelDate { get; set; }            // DELDATE
    public int Checksum { get; set; }                 // CHECKSUM
    public int ViewTab { get; set; }                  // VIEWTAB
    public bool Macro { get; set; }                   // MACRO
    public DateTime? LastModified { get; set; }       // LASTMODIFIED
    
    // Content Storage (stored in BLOB tables in ClipMate 7.5)
    public string? TextContent { get; set; }          // From BLOBTXT
    public string? RtfContent { get; set; }           // From BLOBTXT
    public string? HtmlContent { get; set; }          // From BLOBTXT
    public byte[]? ImageData { get; set; }            // From BLOBJPG/BLOBPNG/BLOBBLOB
    public string? FilePathsJson { get; set; }        // From BLOBTXT
    
    // Additional tracking (minimal, not in ClipMate 7.5)
    public string ContentHash { get; set; } = "";    // Better than int checksum
    public string? SourceApplicationName { get; set; }
    public string? SourceApplicationTitle { get; set; }
    public int PasteCount { get; set; }               // Usage statistics
    public bool IsFavorite { get; set; }
    public string? Label { get; set; }
    
    // Type detection (derived from ClipData formats)
    public ClipType Type { get; set; }
}
```

---

## Exact Collection Model for ClipMate 7.5 Compatibility

```csharp
public class Collection
{
    // Primary Key
    public Guid Id { get; set; }                      // COLL_GUID
    
    // Hierarchy
    public Guid? ParentId { get; set; }               // PARENT_ID (for folders)
    public Guid? ParentGuid { get; set; }             // PARENT_GUID
    
    // ClipMate 7.5 Fields - EXACT MATCH
    public string Title { get; set; } = "";           // TITLE (60 chars)
    public int LmType { get; set; }                   // LMTYPE (0=normal, 1=virtual, 2=folder)
    public int ListType { get; set; }                 // LISTTYPE (0=normal, 1=virtual, 3=SQL)
    public int SortKey { get; set; }                  // SORTKEY
    public int IlIndex { get; set; }                  // ILINDEX (icon)
    public int RetentionLimit { get; set; }           // RETENTIONLIMIT
    public int NewClipsGo { get; set; }               // NEWCLIPSGO
    public bool AcceptNewClips { get; set; }          // ACCEPTNEWCLIPS
    public bool ReadOnly { get; set; }                // READONLY
    public bool AcceptDuplicates { get; set; }        // ACCEPTDUPLICATES
    public int SortColumn { get; set; }               // SORTCOLUMN
    public bool SortAscending { get; set; }           // SORTASCENDING
    public bool Encrypted { get; set; }               // ENCRYPTED
    public bool Favorite { get; set; }                // FAVORITE
    public int? LastUserId { get; set; }              // LASTUSER_ID
    public DateTime? LastUpdateTime { get; set; }     // LAST_UPDATE_TIME
    public int? LastKnownCount { get; set; }          // LAST_KNOWN_COUNT
    public string? Sql { get; set; }                  // SQL (256 chars)
    
    // Navigation
    public Collection? Parent { get; set; }
    public ICollection<Collection> Children { get; set; } = new List<Collection>();
}
```

---

## Priority Action Items

### Phase 1: Core Schema Match (CRITICAL)

1. **Clip Table**
   - Add ALL missing fields from ClipMate 7.5
   - Remove `LastAccessedAt` (not in original)
   - Remove `DisplayTitle` computed property (use `Title` field instead)

2. **ClipData Table**
   - Create new `ClipData` model
   - Add repository and service

3. **BLOB Tables**
   - Create `BlobTxt`, `BlobJpg`, `BlobPng`, `BlobBlob` models
   - Move content storage from Clip to BLOB tables

4. **Collection Table**
   - Merge Collection and Folder into single model (match ClipMate 7.5 architecture)
   - Add ALL missing fields
   - Remove modern conveniences not in original

5. **Shortcut Table**
   - Create `Shortcut` model
   - Add repository and service
   - Integrate with PowerPaste

6. **Users Table**
   - Create `User` model
   - Add repository and service

### Phase 2: Data Migration & Testing

7. **Create migration utility** to import from ClipMate 7.5 .DAT files
8. **Test with real ClipMate 7.5 databases** to ensure compatibility
9. **Validate all features** work exactly like ClipMate 7.5

---

## Architecture Notes

**IMPORTANT:** ClipMate 7.5 uses a **unified COLL table** for both Collections and Folders. They differentiate using the `LMTYPE` field:
- `LMTYPE = 0`: Normal collection/folder
- `LMTYPE = 1`: Virtual collection (SQL-based)
- `LMTYPE = 2`: Folder within a collection

Our current separate Collection and Folder models should be merged to match this architecture.

**Storage Strategy:** ClipMate 7.5 separates content into BLOB tables by type for performance. We should replicate this rather than storing everything inline.

**GUIDs vs AutoInc:** We can use GUIDs internally for modern benefits, but must maintain integer ID compatibility for import/export and any external integrations.
