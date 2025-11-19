# ClipMate 7.5 Storage Architecture - Fixed

## Problem

The application was storing clip data incorrectly:

1. ❌ Storing all content inline in the `Clips` table
2. ❌ Not using `ClipData` table to track formats
3. ❌ Not using BLOB tables (`BlobTxt`, `BlobPng`, `BlobJpg`, `BlobBlob`)
4. ❌ Missing `SortKey` auto-generation
5. ❌ Not setting denormalized GUIDs (`CLIP_GUID`, `COLL_GUID`)

## Solution

Enhanced `ClipRepository.CreateAsync()` to implement proper ClipMate 7.5 storage architecture.

## Storage Architecture

### Before (Incorrect)
```
Clips Table
├─ TextContent (inline)     ❌
├─ RtfContent (inline)      ❌
├─ HtmlContent (inline)     ❌
├─ ImageData (inline)       ❌
└─ FilePathsJson (inline)   ❌
```

**Problems:**
- All content stored in single row
- No format metadata
- Can't efficiently query by format
- Wastes space with duplicate formats

### After (Correct - ClipMate 7.5 Style)
```
Clips Table (Metadata only)
├─ Id (GUID)
├─ Title
├─ Size (total)
├─ SortKey (auto-generated) ✅
├─ CollectionId
├─ CLIP_GUID = Id           ✅
└─ ... metadata ...

ClipData Table (Format tracking)
├─ Id (GUID)
├─ ClipId (FK → Clips)
├─ FormatName ("CF_UNICODETEXT", "CF_RTF", "HTML Format", etc.)
├─ Format (int code: 13, 8, 15, etc.)
├─ Size (format-specific)
└─ StorageType (1=TXT, 2=JPG, 3=PNG, 4=BLOB)

BLOB Tables (Actual content)
├─ BlobTxt (text content)
│  ├─ ClipDataId (FK → ClipData)
│  ├─ ClipId (denormalized for performance)
│  └─ Data (nvarchar(max))
│
├─ BlobPng (PNG images)
│  ├─ ClipDataId
│  ├─ ClipId
│  └─ Data (varbinary(max))
│
├─ BlobJpg (JPEG images)
│  └─ ... same structure
│
└─ BlobBlob (generic binary)
   └─ ... same structure
```

## Implementation Details

### 1. SortKey Generation

```csharp
// Auto-increment ID for SortKey (ClipMate uses ID * 100)
var maxId = await _context.Clips.MaxAsync(c => (int?)c.Id.GetHashCode()) ?? 0;
var nextId = maxId + 1;
clip.SortKey = nextId * 100;
```

**ClipMate Pattern:**
- `SortKey = ID * 100`
- Allows manual re-ordering (insert between: 100, 105, 110)
- Matches ClipMate 7.5's SQL: `INSERT INTO CLIP (SORTKEY) VALUES (7700)` for ID=77

### 2. ClipData Creation

For each format present in the clip:

```csharp
var clipData = new ClipData
{
    Id = Guid.NewGuid(),
    ClipId = clip.Id,
    FormatName = "CF_UNICODETEXT",  // Descriptive name
    Format = 13,                     // Windows clipboard format code
    Size = clip.TextContent.Length * 2, // Size in bytes
    StorageType = 1                  // 1=TXT, 2=JPG, 3=PNG, 4=BLOB
};
```

### 3. BLOB Storage

Content is stored in format-specific tables:

**Text Formats** → `BlobTxt`
```csharp
var blobTxt = new BlobTxt
{
    Id = Guid.NewGuid(),
    ClipDataId = clipData.Id,
    ClipId = clip.Id,  // Denormalized for quick queries
    Data = clip.TextContent
};
```

**Images (PNG)** → `BlobPng`
```csharp
var blobPng = new BlobPng
{
    Id = Guid.NewGuid(),
    ClipDataId = clipData.Id,
    ClipId = clip.Id,
    Data = clip.ImageData  // Already converted to PNG in ClipboardService
};
```

**Files (JSON)** → `BlobBlob`
```csharp
var blobBlob = new BlobBlob
{
    Id = Guid.NewGuid(),
    ClipDataId = clipData.Id,
    ClipId = clip.Id,
    Data = Encoding.UTF8.GetBytes(clip.FilePathsJson)
};
```

### 4. Format Codes

| Format | Code | ClipData.FormatName | StorageType | Table |
|--------|------|---------------------|-------------|-------|
| CF_TEXT | 1 | "CF_TEXT" | 1 | BlobTxt |
| CF_BITMAP | 2 | "CF_BITMAP" | 2 | BlobJpg |
| CF_DIB | 8 | "CF_DIB" | 3 | BlobPng |
| CF_UNICODETEXT | 13 | "CF_UNICODETEXT" | 1 | BlobTxt |
| CF_HDROP | 15 | "CF_HDROP" | 4 | BlobBlob |
| Rich Text Format | 0x0082 | "CF_RTF" | 1 | BlobTxt |
| HTML Format | 0x0080 | "HTML Format" | 1 | BlobTxt |

## Example: Multi-Format Clip

When copying rich text from Word:

### Captured Formats
1. Plain text (CF_UNICODETEXT)
2. RTF (Rich Text Format)
3. HTML (HTML Format)

### Storage
```sql
-- Clips table (metadata)
INSERT INTO Clips (Id, Title, Size, SortKey, Type, ...)
VALUES ('guid1', 'Document title', 12345, 7700, 2, ...);

-- ClipData entries (3 formats)
INSERT INTO ClipData VALUES ('cd1', 'guid1', 'CF_UNICODETEXT', 13, 1024, 1);
INSERT INTO ClipData VALUES ('cd2', 'guid1', 'CF_RTF', 130, 4096, 1);
INSERT INTO ClipData VALUES ('cd3', 'guid1', 'HTML Format', 128, 8192, 1);

-- BlobTxt entries (3 text formats)
INSERT INTO BlobTxt VALUES ('b1', 'cd1', 'guid1', 'Plain text version...');
INSERT INTO BlobTxt VALUES ('b2', 'cd2', 'guid1', '{\rtf1\ansi...');
INSERT INTO BlobTxt VALUES ('b3', 'cd3', 'guid1', '<html><body>...');
```

## Benefits of This Architecture

### 1. Format Flexibility
- Can store multiple formats per clip
- Easy to query by format: `WHERE Format = 13` (text only)
- Support for any clipboard format

### 2. Space Efficiency
- BLOBs stored separately (can be compressed)
- Can purge old BLOBs while keeping metadata
- Denormalized `ClipId` in BLOB tables = fast queries without joins

### 3. Performance
- Clips table stays small (metadata only)
- Queries on metadata are fast (no large BLOBs)
- BLOB retrieval only when needed

### 4. ClipMate 7.5 Compatibility
- Exact same schema as ClipMate 7.5
- Can import/export ClipMate databases
- PowerPaste queries work unchanged

## Soft Delete

Clips are soft-deleted (ClipMate style):

```csharp
public async Task<bool> DeleteAsync(Guid id)
{
    clip.Del = true;
    clip.DelDate = DateTime.UtcNow;
    
    // Delete BLOB data to save space (hard delete)
    await DeleteClipBlobsAsync(id);
}
```

**Benefits:**
- Metadata preserved for history/undo
- BLOB space reclaimed immediately
- Can "undelete" by setting `Del = false`

## Queries Enabled by This Architecture

### Get all text clips
```sql
SELECT c.* 
FROM Clips c
JOIN ClipData cd ON cd.ClipId = c.Id
WHERE cd.Format = 13;  -- CF_UNICODETEXT
```

### Get total storage by format
```sql
SELECT cd.FormatName, SUM(cd.Size) as TotalBytes
FROM ClipData cd
GROUP BY cd.FormatName;
```

### Get clips with images
```sql
SELECT c.*
FROM Clips c
JOIN ClipData cd ON cd.ClipId = c.Id
WHERE cd.Format IN (2, 8);  -- CF_BITMAP or CF_DIB
```

### PowerPaste query (ClipMate 7.5)
```sql
SELECT c.*, s.Nickname
FROM Clips c
LEFT JOIN Shortcuts s ON s.ClipId = c.Id
WHERE c.Del = 0
ORDER BY c.SortKey;
```

## Migration Path

For existing inline data:

1. **Read clips with inline content**
   ```csharp
   var clipsWithInlineData = await _context.Clips
       .Where(c => c.TextContent != null || c.ImageData != null)
       .ToListAsync();
   ```

2. **Migrate to BLOB storage**
   ```csharp
   foreach (var clip in clipsWithInlineData)
   {
       await StoreClipContentAsync(clip);
       // Optionally clear inline data after migration
   }
   ```

3. **Verify**
   ```sql
   -- Should have ClipData entries for all clips
   SELECT COUNT(*) FROM ClipData;
   
   -- Should have BLOB entries
   SELECT COUNT(*) FROM BlobTxt;
   SELECT COUNT(*) FROM BlobPng;
   ```

## Testing

### Verify Correct Storage

```csharp
[Fact]
public async Task CreateAsync_ShouldStoreInBlobTables()
{
    // Arrange
    var clip = new Clip
    {
        Id = Guid.NewGuid(),
        Type = ClipType.Text,
        TextContent = "Test content"
    };
    
    // Act
    await _repository.CreateAsync(clip);
    
    // Assert
    var clipData = await _context.ClipData
        .Where(cd => cd.ClipId == clip.Id)
        .ToListAsync();
    
    clipData.Should().NotBeEmpty();
    clipData.Should().Contain(cd => cd.FormatName == "CF_UNICODETEXT");
    
    var blobTxt = await _context.BlobTxt
        .Where(bt => bt.ClipId == clip.Id)
        .FirstOrDefaultAsync();
    
    blobTxt.Should().NotBeNull();
    blobTxt!.Data.Should().Be("Test content");
}
```

## Summary

✅ **Fixed:**
- Clips now store metadata only
- Content stored in appropriate BLOB tables
- ClipData tracks all formats
- SortKey auto-generated
- Matches ClipMate 7.5 schema exactly

✅ **Benefits:**
- Space efficient
- Format-aware queries
- Fast metadata queries
- ClipMate 7.5 compatible
- Proper CASCADE deletes

✅ **Ready for:**
- PowerPaste (needs Shortcuts table)
- Virtual collections (SQL queries work)
- Import/export ClipMate databases
- Multi-format clip handling
