# Service Contracts: ClipMate Clipboard Manager

**Date**: 2025-11-11  
**Feature**: 001-clipboard-manager  
**Phase**: Phase 1 - Design & Contracts

## Overview

This document defines the service interfaces (contracts) for ClipMate's core business logic. These interfaces enable dependency injection, testability, and clean separation of concerns per the constitution.

---

## IClipboardService

Manages Windows clipboard monitoring and capture operations.

```csharp
public interface IClipboardService : IDisposable
{
    /// <summary>
    /// Raised when a new clip is captured from the clipboard
    /// </summary>
    event EventHandler<ClipCapturedEventArgs> ClipCaptured;
    
    /// <summary>
    /// Raised when clipboard capture fails
    /// </summary>
    event EventHandler<ClipCaptureErrorEventArgs> CaptureError;
    
    /// <summary>
    /// Starts monitoring the Windows clipboard for changes
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StartMonitoringAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stops monitoring the clipboard
    /// </summary>
    Task StopMonitoringAsync();
    
    /// <summary>
    /// Gets whether clipboard monitoring is currently active
    /// </summary>
    bool IsMonitoring { get; }
    
    /// <summary>
    /// Sets the clipboard content from a clip (for pasting)
    /// </summary>
    /// <param name="clip">Clip to set as clipboard content</param>
    Task SetClipboardAsync(Clip clip);
    
    /// <summary>
    /// Retrieves current clipboard content without saving it
    /// </summary>
    /// <returns>Clip representing current clipboard, or null if empty</returns>
    Task<Clip?> GetCurrentClipboardContentAsync();
}

public class ClipCapturedEventArgs : EventArgs
{
    public Clip Clip { get; set; }
    public bool IsDuplicate { get; set; }
}

public class ClipCaptureErrorEventArgs : EventArgs
{
    public Exception Exception { get; set; }
    public string ErrorMessage { get; set; }
}
```

**Implementation Notes**:
- Uses Win32 `RegisterClipboardFormatListener` for monitoring
- Processes captures on background thread to avoid UI blocking
- Implements 50ms debouncing to handle multiple clipboard sets
- Checks application filters before raising ClipCaptured event
- Detects duplicates via ContentHash before event raise

---

## ISearchService

Provides full-text search across clipboard history with advanced filtering.

```csharp
public interface ISearchService
{
    /// <summary>
    /// Searches clips by content with optional filters
    /// </summary>
    /// <param name="query">Search query text</param>
    /// <param name="options">Search options and filters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Matching clips ordered by relevance</returns>
    Task<SearchResult> SearchAsync(
        string query,
        SearchOptions options,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Performs regex pattern search
    /// </summary>
    Task<SearchResult> SearchRegexAsync(
        string pattern,
        SearchOptions options,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets recently accessed clips (PowerPaste menu source)
    /// </summary>
    /// <param name="count">Number of recent clips to return</param>
    /// <param name="collectionId">Optional collection filter</param>
    Task<IEnumerable<Clip>> GetRecentClipsAsync(
        int count = 20,
        Guid? collectionId = null);
    
    /// <summary>
    /// Executes a saved search query
    /// </summary>
    Task<SearchResult> ExecuteSavedSearchAsync(
        Guid searchQueryId,
        CancellationToken cancellationToken = default);
}

public class SearchOptions
{
    public SearchScope Scope { get; set; } = SearchScope.CurrentCollection;
    public Guid? ScopeId { get; set; }
    public ClipContentType? ContentTypeFilter { get; set; }
    public DateTime? DateRangeStart { get; set; }
    public DateTime? DateRangeEnd { get; set; }
    public string? SourceAppFilter { get; set; }
    public int MaxResults { get; set; } = 1000;
    public bool IncludeThumbnails { get; set; } = false;
}

public class SearchResult
{
    public IEnumerable<Clip> Clips { get; set; }
    public int TotalCount { get; set; }
    public TimeSpan SearchDuration { get; set; }
    public bool IsTruncated => Clips.Count() < TotalCount;
}
```

**Performance Requirements**:
- Search must complete within 50ms per constitution
- Results ordered by relevance: recent access > timestamp > usage count
- Lazy load clip content (only metadata initially)
- Cancel previous search when new search initiated

---

## ICollectionService

Manages collection lifecycle and operations.

```csharp
public interface ICollectionService
{
    /// <summary>
    /// Gets all available collections
    /// </summary>
    Task<IEnumerable<Collection>> GetCollectionsAsync();
    
    /// <summary>
    /// Gets the currently active collection
    /// </summary>
    Task<Collection> GetActiveCollectionAsync();
    
    /// <summary>
    /// Sets the active collection
    /// </summary>
    Task SetActiveCollectionAsync(Guid collectionId);
    
    /// <summary>
    /// Creates a new collection
    /// </summary>
    Task<Collection> CreateCollectionAsync(string name, string? description = null);
    
    /// <summary>
    /// Updates collection properties
    /// </summary>
    Task UpdateCollectionAsync(Collection collection);
    
    /// <summary>
    /// Deletes a collection and its database file
    /// </summary>
    /// <param name="collectionId">Collection to delete</param>
    /// <param name="deleteFile">Whether to delete the database file</param>
    Task DeleteCollectionAsync(Guid collectionId, bool deleteFile = true);
    
    /// <summary>
    /// Applies retention policy to collection (cleanup old clips)
    /// </summary>
    Task ApplyRetentionPolicyAsync(Guid collectionId);
    
    /// <summary>
    /// Exports collection to file for backup
    /// </summary>
    Task<string> ExportCollectionAsync(Guid collectionId, string exportPath);
    
    /// <summary>
    /// Imports collection from backup file
    /// </summary>
    Task<Collection> ImportCollectionAsync(string importPath, string? newName = null);
    
    /// <summary>
    /// Gets collection statistics
    /// </summary>
    Task<CollectionStatistics> GetStatisticsAsync(Guid collectionId);
}

public class CollectionStatistics
{
    public int ClipCount { get; set; }
    public long TotalSize { get; set; }
    public int FolderCount { get; set; }
    public DateTime? OldestClip { get; set; }
    public DateTime? NewestClip { get; set; }
    public Dictionary<ClipContentType, int> CountByType { get; set; }
}
```

---

## IFolderService

Manages folder hierarchy within collections.

```csharp
public interface IFolderService
{
    /// <summary>
    /// Gets root folders for a collection
    /// </summary>
    Task<IEnumerable<Folder>> GetRootFoldersAsync(Guid collectionId);
    
    /// <summary>
    /// Gets child folders of a parent folder
    /// </summary>
    Task<IEnumerable<Folder>> GetChildFoldersAsync(Guid parentFolderId);
    
    /// <summary>
    /// Creates a new folder
    /// </summary>
    Task<Folder> CreateFolderAsync(
        Guid collectionId,
        string name,
        Guid? parentFolderId = null);
    
    /// <summary>
    /// Updates folder properties
    /// </summary>
    Task UpdateFolderAsync(Folder folder);
    
    /// <summary>
    /// Moves a folder to a new parent
    /// </summary>
    Task MoveFolderAsync(Guid folderId, Guid? newParentFolderId);
    
    /// <summary>
    /// Deletes a folder and optionally its clips
    /// </summary>
    /// <param name="deleteClips">If true, delete clips; if false, move to parent</param>
    Task DeleteFolderAsync(Guid folderId, bool deleteClips = false);
    
    /// <summary>
    /// Gets full folder path (breadcrumb trail)
    /// </summary>
    Task<IEnumerable<Folder>> GetFolderPathAsync(Guid folderId);
}
```

---

## IClipService

Manages individual clip operations (CRUD + organization).

```csharp
public interface IClipService
{
    /// <summary>
    /// Gets a clip by ID with optional content loading
    /// </summary>
    Task<Clip?> GetClipAsync(Guid clipId, bool loadContent = false);
    
    /// <summary>
    /// Gets clips in a collection or folder
    /// </summary>
    Task<IEnumerable<Clip>> GetClipsAsync(
        Guid collectionId,
        Guid? folderId = null,
        int skip = 0,
        int take = 100);
    
    /// <summary>
    /// Creates a new clip (used by ClipboardService)
    /// </summary>
    Task<Clip> CreateClipAsync(Clip clip);
    
    /// <summary>
    /// Updates clip metadata (title, tags, favorite)
    /// </summary>
    Task UpdateClipAsync(Clip clip);
    
    /// <summary>
    /// Moves clip to different folder or collection
    /// </summary>
    Task MoveClipAsync(Guid clipId, Guid? targetFolderId, Guid? targetCollectionId = null);
    
    /// <summary>
    /// Deletes a clip
    /// </summary>
    Task DeleteClipAsync(Guid clipId);
    
    /// <summary>
    /// Deletes multiple clips
    /// </summary>
    Task DeleteClipsAsync(IEnumerable<Guid> clipIds);
    
    /// <summary>
    /// Marks clip as accessed (updates LastAccessedUtc, increments AccessCount)
    /// </summary>
    Task RecordAccessAsync(Guid clipId);
    
    /// <summary>
    /// Checks if clip is duplicate based on ContentHash
    /// </summary>
    Task<bool> IsDuplicateAsync(string contentHash, Guid collectionId);
    
    /// <summary>
    /// Gets favorite clips across collections
    /// </summary>
    Task<IEnumerable<Clip>> GetFavoriteClipsAsync();
}
```

---

## IHotkeyService

Manages global hotkey registration and handling.

```csharp
public interface IHotkeyService : IDisposable
{
    /// <summary>
    /// Raised when a registered hotkey is pressed
    /// </summary>
    event EventHandler<HotkeyPressedEventArgs> HotkeyPressed;
    
    /// <summary>
    /// Registers a global hotkey
    /// </summary>
    /// <param name="id">Unique identifier for this hotkey</param>
    /// <param name="modifiers">Modifier keys (Ctrl, Alt, Shift, Win)</param>
    /// <param name="key">Main key</param>
    /// <returns>True if successful, false if already registered by another app</returns>
    Task<bool> RegisterHotkeyAsync(string id, ModifierKeys modifiers, Key key);
    
    /// <summary>
    /// Unregisters a hotkey
    /// </summary>
    Task UnregisterHotkeyAsync(string id);
    
    /// <summary>
    /// Unregisters all hotkeys
    /// </summary>
    Task UnregisterAllAsync();
    
    /// <summary>
    /// Checks if a hotkey combination is available
    /// </summary>
    Task<bool> IsHotkeyAvailableAsync(ModifierKeys modifiers, Key key);
    
    /// <summary>
    /// Gets all registered hotkeys
    /// </summary>
    Dictionary<string, HotkeyRegistration> GetRegisteredHotkeys();
}

public class HotkeyPressedEventArgs : EventArgs
{
    public string HotkeyId { get; set; }
    public ModifierKeys Modifiers { get; set; }
    public Key Key { get; set; }
}

public class HotkeyRegistration
{
    public string Id { get; set; }
    public ModifierKeys Modifiers { get; set; }
    public Key Key { get; set; }
    public DateTime RegisteredAt { get; set; }
}
```

**Default Hotkeys**:
- PowerPaste: Ctrl+Shift+V
- Main Window: Ctrl+Shift+C
- Search: Ctrl+Shift+F (when window focused)

---

## ITemplateService

Manages text templates with variable expansion.

```csharp
public interface ITemplateService
{
    /// <summary>
    /// Gets all templates, optionally filtered by category
    /// </summary>
    Task<IEnumerable<Template>> GetTemplatesAsync(string? category = null);
    
    /// <summary>
    /// Gets a template by ID
    /// </summary>
    Task<Template?> GetTemplateAsync(Guid templateId);
    
    /// <summary>
    /// Creates a new template
    /// </summary>
    Task<Template> CreateTemplateAsync(Template template);
    
    /// <summary>
    /// Updates a template
    /// </summary>
    Task UpdateTemplateAsync(Template template);
    
    /// <summary>
    /// Deletes a template
    /// </summary>
    Task DeleteTemplateAsync(Guid templateId);
    
    /// <summary>
    /// Expands template variables to produce final text
    /// </summary>
    /// <param name="templateId">Template to expand</param>
    /// <param name="promptValues">User-provided values for PROMPT variables</param>
    /// <returns>Expanded text with all variables replaced</returns>
    Task<string> ExpandTemplateAsync(
        Guid templateId,
        Dictionary<string, string>? promptValues = null);
    
    /// <summary>
    /// Validates template syntax (checks for undefined variables, invalid format)
    /// </summary>
    Task<TemplateValidationResult> ValidateTemplateAsync(string content);
    
    /// <summary>
    /// Exports templates to file for sharing
    /// </summary>
    Task ExportTemplatesAsync(IEnumerable<Guid> templateIds, string filePath);
    
    /// <summary>
    /// Imports templates from file
    /// </summary>
    Task<IEnumerable<Template>> ImportTemplatesAsync(string filePath);
}

public class TemplateValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> UndefinedVariables { get; set; } = new();
}
```

**Variable Expansion Rules**:
- `{DATE}` → Current date in system format
- `{DATE:yyyy-MM-dd}` → Current date with custom format
- `{TIME}` → Current time in system format
- `{TIME:HH:mm}` → Current time with custom format
- `{USERNAME}` → Current Windows username
- `{COMPUTERNAME}` → Current computer name
- `{PROMPT:Label}` → Prompt user for input with dialog

---

## ISoundService

Manages audio feedback for application events.

```csharp
public interface ISoundService : IDisposable
{
    /// <summary>
    /// Plays sound for an event type
    /// </summary>
    Task PlaySoundAsync(SoundEventType eventType);
    
    /// <summary>
    /// Gets sound configuration for an event
    /// </summary>
    Task<SoundEvent> GetSoundEventAsync(SoundEventType eventType);
    
    /// <summary>
    /// Updates sound configuration
    /// </summary>
    Task UpdateSoundEventAsync(SoundEvent soundEvent);
    
    /// <summary>
    /// Sets master volume for all sounds
    /// </summary>
    Task SetMasterVolumeAsync(float volume);
    
    /// <summary>
    /// Gets master volume
    /// </summary>
    float GetMasterVolume();
    
    /// <summary>
    /// Enables or disables all sound feedback
    /// </summary>
    Task SetSoundsEnabledAsync(bool enabled);
    
    /// <summary>
    /// Gets whether sounds are globally enabled
    /// </summary>
    bool AreSoundsEnabled();
}
```

---

## ISettingsService

Manages application settings and preferences.

```csharp
public interface ISettingsService
{
    /// <summary>
    /// Gets a setting value by key
    /// </summary>
    Task<T?> GetSettingAsync<T>(string key, T? defaultValue = default);
    
    /// <summary>
    /// Sets a setting value
    /// </summary>
    Task SetSettingAsync<T>(string key, T value);
    
    /// <summary>
    /// Deletes a setting
    /// </summary>
    Task DeleteSettingAsync(string key);
    
    /// <summary>
    /// Gets all settings as dictionary
    /// </summary>
    Task<Dictionary<string, object>> GetAllSettingsAsync();
    
    /// <summary>
    /// Exports settings to file for backup
    /// </summary>
    Task ExportSettingsAsync(string filePath);
    
    /// <summary>
    /// Imports settings from file
    /// </summary>
    Task ImportSettingsAsync(string filePath, bool merge = false);
}
```

**Common Settings Keys**:
- `App.StartMinimized` (bool)
- `App.StartWithWindows` (bool)
- `App.CheckForUpdates` (bool)
- `UI.Theme` (string: "Light", "Dark", "System")
- `UI.Language` (string: culture code)
- `Clipboard.MonitoringEnabled` (bool)
- `PowerPaste.RecentItemCount` (int)
- `Search.MaxResults` (int)

---

## IApplicationFilterService

Manages filters for excluding clipboard captures.

```csharp
public interface IApplicationFilterService
{
    /// <summary>
    /// Gets all application filters
    /// </summary>
    Task<IEnumerable<ApplicationFilter>> GetFiltersAsync();
    
    /// <summary>
    /// Gets enabled filters only
    /// </summary>
    Task<IEnumerable<ApplicationFilter>> GetActiveFiltersAsync();
    
    /// <summary>
    /// Creates a new filter
    /// </summary>
    Task<ApplicationFilter> CreateFilterAsync(ApplicationFilter filter);
    
    /// <summary>
    /// Updates a filter
    /// </summary>
    Task UpdateFilterAsync(ApplicationFilter filter);
    
    /// <summary>
    /// Deletes a filter
    /// </summary>
    Task DeleteFilterAsync(Guid filterId);
    
    /// <summary>
    /// Checks if a source should be filtered (excluded from capture)
    /// </summary>
    /// <param name="processName">Process name (e.g., "notepad.exe")</param>
    /// <param name="windowTitle">Window title</param>
    /// <param name="windowClass">Window class name</param>
    Task<bool> ShouldFilterAsync(
        string processName,
        string? windowTitle = null,
        string? windowClass = null);
}
```

---

## Service Contracts Summary

The service contracts provide:
- **10 core services**: Clipboard, Search, Collection, Folder, Clip, Hotkey, Template, Sound, Settings, ApplicationFilter
- **Clean interfaces**: All services behind interfaces for DI and testing
- **Async operations**: All I/O operations use async/await pattern
- **Event-driven**: Services raise events for UI reactivity
- **Cancellation support**: Long operations support CancellationToken
- **Type safety**: Strong typing with enums and DTOs

These contracts will be implemented in `ClipMate.Core` project and consumed by ViewModels in `ClipMate.App`.
