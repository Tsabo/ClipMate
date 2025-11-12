# Data Model: ClipMate Clipboard Manager

**Date**: 2025-11-11  
**Feature**: 001-clipboard-manager  
**Phase**: Phase 1 - Design & Contracts

## Overview

This document defines the core data entities for ClipMate, their relationships, and validation rules. All entities are designed for storage in LiteDB with appropriate indexing for performance.

---

## Core Entities

### Clip

A Clip represents a single captured clipboard entry with its content and metadata.

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Id | Guid | Yes | Unique identifier for the clip |
| Timestamp | DateTime | Yes | When the clip was captured (UTC) |
| CollectionId | Guid | Yes | Parent collection identifier |
| FolderId | Guid? | No | Optional folder within collection |
| ContentType | ClipContentType | Yes | Type of content (Text, Image, FileList, RichText) |
| ContentText | string? | No | Text content for text-based clips (indexed for search) |
| ContentBinary | byte[]? | No | Binary content for images or serialized data |
| ContentHash | string | Yes | SHA256 hash for duplicate detection |
| Format | string | Yes | Original clipboard format (e.g., "CF_UNICODETEXT", "CF_DIB") |
| SourceApplication | string? | No | Name of application that set the clipboard |
| SourceProcessId | int? | No | Process ID of source application |
| Title | string? | No | User-assigned or auto-generated title |
| Tags | List\<string\> | No | User-assigned tags for categorization |
| IsFavorite | bool | No | User-marked as favorite (default: false) |
| Size | long | Yes | Content size in bytes |
| ThumbnailData | byte[]? | No | Cached thumbnail for images (max 200x200) |
| LastAccessedUtc | DateTime? | No | Last time clip was viewed or pasted |
| AccessCount | int | No | Number of times clip has been accessed |
| Created | DateTime | Yes | Creation timestamp (UTC) |
| Modified | DateTime | Yes | Last modification timestamp (UTC) |

**Relationships**:
- Many-to-One: Clip → Collection
- Many-to-One: Clip → Folder (optional)

**Validation Rules**:
- `ContentType` must be valid enum value
- `ContentHash` must be unique within collection (duplicate detection)
- `Size` must be > 0 and <= 52,428,800 (50MB max per clip)
- `ContentText` required when ContentType is Text or RichText
- `ContentBinary` required when ContentType is Image or FileList
- `SourceApplication` max length 255 characters
- `Title` max length 500 characters
- `Tags` max 50 tags per clip, each tag max 50 characters

**Indexes**:
```csharp
collection.EnsureIndex(x => x.Timestamp, false);      // Date range queries, DESC order
collection.EnsureIndex(x => x.ContentHash);           // Duplicate detection
collection.EnsureIndex(x => x.CollectionId);          // Collection filtering
collection.EnsureIndex(x => x.FolderId);              // Folder navigation
collection.EnsureIndex(x => x.SourceApplication);     // Application filtering
collection.EnsureIndex(x => x.IsFavorite);            // Favorites view
collection.EnsureIndex("$.ContentText");              // Full-text search
```

---

### Collection

A Collection represents a separate database/container for organizing clips (e.g., Work, Personal, Temp).

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Id | Guid | Yes | Unique identifier for the collection |
| Name | string | Yes | Display name for the collection |
| Description | string? | No | Optional description |
| FilePath | string | Yes | Absolute path to the LiteDB file |
| IconName | string? | No | Icon identifier for UI display |
| ColorCode | string? | No | Hex color code for visual identification |
| IsDefault | bool | No | Whether this is the default collection (default: false) |
| RetentionPolicy | RetentionPolicy | Yes | How clips should be retained/deleted |
| MaxClipCount | int? | No | Maximum number of clips (null = unlimited) |
| MaxTotalSize | long? | No | Maximum total size in bytes (null = unlimited) |
| MaxClipAge | TimeSpan? | No | Maximum age before auto-deletion (null = no age limit) |
| AutoCategorize | bool | No | Enable automatic folder assignment (default: false) |
| ClipCount | int | No | Cached count of clips in collection |
| TotalSize | long | No | Cached total size of all clips |
| LastAccessedUtc | DateTime? | No | Last time collection was viewed |
| Created | DateTime | Yes | Creation timestamp (UTC) |
| Modified | DateTime | Yes | Last modification timestamp (UTC) |

**Validation Rules**:
- `Name` required, max length 100 characters, must be unique
- `FilePath` must be valid absolute path, must be unique
- `MaxClipCount` if set, must be >= 10 and <= 1,000,000
- `MaxTotalSize` if set, must be >= 10MB and <= 100GB
- `MaxClipAge` if set, must be >= 1 hour
- Only one collection can have `IsDefault = true`
- `ColorCode` must be valid hex format (#RRGGBB)

**Indexes**:
```csharp
collection.EnsureIndex(x => x.Name, true);  // Unique index
collection.EnsureIndex(x => x.IsDefault);   // Quick default lookup
```

---

### Folder

A Folder provides hierarchical organization within a Collection.

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Id | Guid | Yes | Unique identifier for the folder |
| CollectionId | Guid | Yes | Parent collection identifier |
| ParentFolderId | Guid? | No | Parent folder for nesting (null = root level) |
| Name | string | Yes | Display name for the folder |
| IconName | string? | No | Icon identifier for UI display |
| ColorCode | string? | No | Hex color code for visual identification |
| SortOrder | int | No | Display order within parent (default: 0) |
| AutoCategorizeRules | List\<AutoCategorizeRule\>? | No | Rules for automatic clip assignment |
| ClipCount | int | No | Cached count of clips in folder |
| Created | DateTime | Yes | Creation timestamp (UTC) |
| Modified | DateTime | Yes | Last modification timestamp (UTC) |

**Relationships**:
- Many-to-One: Folder → Collection
- Many-to-One: Folder → Folder (parent, optional for nesting)
- One-to-Many: Folder → Clip

**Validation Rules**:
- `Name` required, max length 100 characters
- `Name` must be unique within parent folder scope
- `ParentFolderId` must exist within same collection (prevent orphans)
- Circular parent references not allowed (must be acyclic tree)
- Maximum nesting depth: 10 levels
- `ColorCode` must be valid hex format (#RRGGBB)

**Indexes**:
```csharp
collection.EnsureIndex(x => x.CollectionId);     // Collection filtering
collection.EnsureIndex(x => x.ParentFolderId);   // Tree navigation
collection.EnsureIndex(x => x.SortOrder);        // Ordering
```

---

### Template

A Template represents reusable text content with variable placeholders.

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Id | Guid | Yes | Unique identifier for the template |
| Name | string | Yes | Display name for the template |
| Content | string | Yes | Template content with variable placeholders |
| Category | string? | No | Optional category for organization |
| Description | string? | No | Optional description |
| Variables | List\<TemplateVariable\> | No | Defined variables with defaults |
| Hotkey | string? | No | Optional keyboard shortcut |
| UsageCount | int | No | Number of times template has been used |
| LastUsedUtc | DateTime? | No | Last time template was used |
| Created | DateTime | Yes | Creation timestamp (UTC) |
| Modified | DateTime | Yes | Last modification timestamp (UTC) |

**Validation Rules**:
- `Name` required, max length 200 characters, must be unique
- `Content` required, max length 50,000 characters
- `Category` max length 100 characters
- `Description` max length 500 characters
- Variable placeholders must be valid format: `{VARNAME}` or `{PROMPT:Label}`
- All variables in content must be defined in Variables list
- `Hotkey` must be valid key combination if specified

**Indexes**:
```csharp
collection.EnsureIndex(x => x.Name, true);       // Unique index
collection.EnsureIndex(x => x.Category);         // Category filtering
collection.EnsureIndex(x => x.UsageCount);       // Most used templates
```

---

### SearchQuery

A SearchQuery represents a saved search for quick recall.

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Id | Guid | Yes | Unique identifier for the search query |
| Name | string | Yes | Display name for saved search |
| QueryText | string | Yes | Search text/pattern |
| IsRegex | bool | No | Whether query is regex pattern (default: false) |
| Scope | SearchScope | Yes | Where to search (CurrentCollection, AllCollections, SpecificFolder) |
| ScopeId | Guid? | No | Collection or Folder ID when scope is specific |
| ContentTypeFilter | ClipContentType? | No | Filter by content type (null = all types) |
| DateRangeStart | DateTime? | No | Filter by start date (null = no start limit) |
| DateRangeEnd | DateTime? | No | Filter by end date (null = no end limit) |
| SourceAppFilter | string? | No | Filter by source application name |
| UsageCount | int | No | Number of times search has been executed |
| LastUsedUtc | DateTime? | No | Last time search was executed |
| Created | DateTime | Yes | Creation timestamp (UTC) |

**Validation Rules**:
- `Name` required, max length 100 characters, must be unique
- `QueryText` required, max length 1000 characters
- If `IsRegex = true`, QueryText must be valid regex pattern
- `ScopeId` required when Scope is SpecificFolder
- `DateRangeEnd` must be >= DateRangeStart if both specified

**Indexes**:
```csharp
collection.EnsureIndex(x => x.Name, true);       // Unique index
collection.EnsureIndex(x => x.UsageCount);       // Most used searches
```

---

### ApplicationFilter

An ApplicationFilter defines rules for excluding clipboard captures from specific applications.

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Id | Guid | Yes | Unique identifier for the filter |
| FilterType | FilterType | Yes | How to match (ProcessName, WindowTitle, WindowClass) |
| Pattern | string | Yes | Pattern to match (exact or wildcard) |
| IsEnabled | bool | No | Whether filter is active (default: true) |
| Description | string? | No | Optional description |
| Created | DateTime | Yes | Creation timestamp (UTC) |

**Validation Rules**:
- `Pattern` required, max length 500 characters
- `Pattern` cannot be empty or whitespace
- Duplicate patterns of same FilterType not allowed
- `Description` max length 500 characters

**Indexes**:
```csharp
collection.EnsureIndex(x => x.IsEnabled);        // Active filters
collection.EnsureIndex(x => x.FilterType);       // Filter type lookup
```

---

### SoundEvent

A SoundEvent maps application events to audio files for sound feedback.

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Id | Guid | Yes | Unique identifier for the sound event |
| EventType | SoundEventType | Yes | Type of event (ClipCaptured, PowerPasteOpened, SearchCompleted, Error) |
| SoundFilePath | string? | No | Path to WAV file (null = no sound for this event) |
| Volume | float | No | Volume level 0.0-1.0 (default: 0.5) |
| IsEnabled | bool | No | Whether sound is enabled for this event (default: true) |
| Modified | DateTime | Yes | Last modification timestamp (UTC) |

**Validation Rules**:
- `EventType` must be valid enum value and unique (one row per event type)
- `SoundFilePath` if specified, must exist and be .wav file
- `Volume` must be >= 0.0 and <= 1.0
- Maximum 4KB file size limit for validation (checked on load, not enforced in DB)

**Indexes**:
```csharp
collection.EnsureIndex(x => x.EventType, true);  // Unique index
```

---

## Enumerations

### ClipContentType
```csharp
public enum ClipContentType
{
    Text = 0,         // Plain text content
    RichText = 1,     // RTF or HTML formatted text
    Image = 2,        // PNG, JPEG, BMP, GIF images
    FileList = 3      // List of file paths from Explorer
}
```

### SearchScope
```csharp
public enum SearchScope
{
    CurrentCollection = 0,
    AllCollections = 1,
    SpecificFolder = 2
}
```

### FilterType
```csharp
public enum FilterType
{
    ProcessName = 0,   // Match against process name (e.g., "notepad.exe")
    WindowTitle = 1,   // Match against window title (e.g., "*Password*")
    WindowClass = 2    // Match against window class name
}
```

### SoundEventType
```csharp
public enum SoundEventType
{
    ClipCaptured = 0,
    PowerPasteOpened = 1,
    SearchCompleted = 2,
    Error = 3
}
```

### RetentionPolicy
```csharp
public enum RetentionPolicy
{
    KeepAll = 0,              // Never auto-delete
    LimitByCount = 1,         // Delete oldest when MaxClipCount exceeded
    LimitBySize = 2,          // Delete oldest when MaxTotalSize exceeded
    LimitByAge = 3,           // Delete clips older than MaxClipAge
    LimitByCountAndAge = 4    // Apply both count and age limits
}
```

---

## Nested Types

### TemplateVariable
```csharp
public class TemplateVariable
{
    public string Name { get; set; }           // Variable name (e.g., "DATE")
    public string Type { get; set; }           // Type: "Date", "Time", "Prompt", "System"
    public string? Format { get; set; }        // Format string (e.g., "yyyy-MM-dd")
    public string? DefaultValue { get; set; }  // Default value if not user-provided
    public string? PromptLabel { get; set; }   // Label for prompt dialog (PROMPT type)
}
```

### AutoCategorizeRule
```csharp
public class AutoCategorizeRule
{
    public string Condition { get; set; }      // Condition type: "ContentType", "SourceApp", "ContentPattern"
    public string Pattern { get; set; }        // Pattern to match
    public Guid TargetFolderId { get; set; }   // Destination folder
}
```

---

## State Transitions

### Clip Lifecycle
1. **Created**: Clip is captured from clipboard → Status: Active
2. **Accessed**: User views or pastes clip → Update `LastAccessedUtc`, increment `AccessCount`
3. **Modified**: User edits title, tags, or moves to folder → Update `Modified`
4. **Favorited**: User marks as favorite → Set `IsFavorite = true`
5. **Deleted**: User deletes clip or retention policy triggers → Removed from database

### Collection Lifecycle
1. **Created**: User creates new collection → Database file created
2. **Activated**: User switches to collection → Set as active in UI state
3. **Modified**: Settings changed (retention policy, name, etc.) → Update `Modified`
4. **Backed Up**: User exports collection → Copy database file
5. **Deleted**: User deletes collection → Database file deleted (with confirmation)

---

## Database Organization

### File Structure
```
%APPDATA%/ClipMate/
├── collections/
│   ├── default.db              # Default collection database
│   ├── work.db                 # Work collection database
│   └── personal.db             # Personal collection database
├── settings.db                 # Application settings, templates, saved searches
└── logs/
    └── app.log                 # Application logs
```

### LiteDB Collections (Tables)
Each collection database contains:
- `clips` - Clip entities
- `folders` - Folder entities
- `metadata` - Database version, created date, statistics

Settings database contains:
- `collections` - Collection metadata
- `templates` - Template entities
- `searches` - SavedQuery entities
- `filters` - ApplicationFilter entities
- `sounds` - SoundEvent entities
- `app_settings` - Key-value configuration

---

## Performance Considerations

### Query Patterns
```csharp
// Fast: Uses Timestamp index (DESC)
var recentClips = clipCollection
    .Find(Query.All(nameof(Clip.Timestamp), Query.Descending), limit: 100);

// Fast: Uses ContentHash index
var duplicate = clipCollection
    .FindOne(x => x.ContentHash == hash);

// Fast: Uses full-text index on ContentText
var searchResults = clipCollection
    .Find(Query.Contains(nameof(Clip.ContentText), searchTerm));

// Optimized: Multiple indexes with AND condition
var filtered = clipCollection
    .Find(x => x.CollectionId == collectionId && x.FolderId == folderId);
```

### Lazy Loading Strategy
- Load only metadata initially (Id, Timestamp, Title, Size, ContentType)
- Load `ContentBinary` only when clip is selected/viewed
- Generate thumbnails on demand and cache in `ThumbnailData`
- Batch load operations for UI virtualization (load 100 items per scroll)

---

## Migration Strategy

Database schema versions will be tracked in metadata collection. Migrations will handle:
- Adding new fields (with default values)
- Adding new indexes
- Renaming fields (copy data, drop old, rename new)
- Data transformations (e.g., hash algorithm changes)

```csharp
public interface IDatabaseMigration
{
    int FromVersion { get; }
    int ToVersion { get; }
    Task MigrateAsync(LiteDatabase db);
}
```

---

## Data Model Summary

The data model supports all functional requirements with:
- **7 core entities**: Clip, Collection, Folder, Template, SearchQuery, ApplicationFilter, SoundEvent
- **Proper indexing**: All performance-critical queries optimized
- **Relationships**: Clean hierarchies (Collection → Folder → Clip)
- **Validation**: Comprehensive rules prevent data corruption
- **Scalability**: Designed for 100k+ clips per collection
- **Maintainability**: Clear separation, easy to extend

Ready to proceed with contract definitions (API/service interfaces).
