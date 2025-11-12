# Research: ClipMate Clipboard Manager

**Date**: 2025-11-11  
**Feature**: 001-clipboard-manager  
**Phase**: Phase 0 - Outline & Research

## Research Tasks

This document consolidates research findings for technical decisions needed to implement ClipMate.

---

## 1. Windows Clipboard Monitoring Strategy

**Decision**: Use Win32 `RegisterClipboardFormatListener` API with background thread processing

**Rationale**: 
- `RegisterClipboardFormatListener` is the modern clipboard monitoring approach (Windows Vista+)
- Automatically receives `WM_CLIPBOARDUPDATE` messages without polling
- More reliable than the legacy `SetClipboardViewer` chain which can break
- Supports all clipboard formats including custom application formats
- Integrates cleanly with WPF message pump via `HwndSource`

**Alternatives Considered**:
- **SetClipboardViewer chain**: Legacy approach, fragile clipboard viewer chain that breaks if any app crashes
- **Polling with timer**: High CPU usage, misses rapid clipboard changes, poor user experience
- **.NET Clipboard.GetDataObject() polling**: Same issues as timer polling, not recommended

**Implementation Notes**:
- Create hidden window handle specifically for clipboard monitoring
- Use `HwndSource.FromHwnd()` to hook Win32 messages into WPF
- Process `WM_CLIPBOARDUPDATE` on background thread to avoid blocking UI
- Implement debouncing (50ms window) to handle apps that set clipboard multiple times
- Handle clipboard access contention with retry logic (other apps may lock clipboard)

---

## 2. Global Hotkey Implementation

**Decision**: Custom Win32 `RegisterHotKey` implementation with conflict detection

**Rationale**:
- Win32 `RegisterHotKey` is the standard Windows API for system-wide hotkeys
- Works across all applications including elevated processes
- Supports all modifier combinations (Ctrl, Alt, Shift, Win key)
- Provides clear success/failure indication for conflict detection
- WPF ComponentDispatcher can intercept hotkey messages cleanly

**Alternatives Considered**:
- **GlobalHotKeys NuGet package**: Adds dependency, simple wrapper around RegisterHotKey, minimal value
- **Low-level keyboard hook**: Requires elevated privileges, more complex, can cause system-wide lag
- **WPF KeyBinding**: Only works when application has focus, doesn't meet requirements

**Implementation Notes**:
- Register hotkeys when main window initializes, unregister on close
- Detect registration failures (ERROR_HOTKEY_ALREADY_REGISTERED) and prompt user for alternative
- Store hotkey preferences in user settings with sensible defaults (Ctrl+Shift+V for PowerPaste)
- Use `ComponentDispatcher.ThreadFilterMessage` to intercept `WM_HOTKEY` messages
- Provide UI for users to customize hotkey combinations with real-time conflict detection

---

## 3. LiteDB Schema Design and Indexing Strategy

**Decision**: LiteDB 5.0+ with separate database files per collection, comprehensive indexing on search fields

**Rationale**:
- LiteDB is embedded NoSQL database, no separate server process needed
- LINQ query support makes development faster and more maintainable
- Excellent performance for read-heavy workloads (clipboard browsing/search)
- Document-oriented model fits clipboard data naturally (varying formats per clip)
- Built-in binary storage (BsonBinary) for efficient image/file storage
- File-per-collection enables easy backup, export, and corruption isolation

**Alternatives Considered**:
- **SQLite**: Requires more schema management, less natural fit for varying clip formats, still excellent option
- **Entity Framework with SQLite**: Too heavy for embedded scenario, LINQ overhead not justified
- **JSON file storage**: Poor search performance, no indexing, manual concurrency handling
- **Single database file for all collections**: Corruption affects all data, harder to backup individual collections

**Indexing Strategy**:
```csharp
// Primary indexes for performance requirements
clipCollection.EnsureIndex(x => x.Timestamp, false);        // Date range queries
clipCollection.EnsureIndex(x => x.ContentHash);             // Duplicate detection
clipCollection.EnsureIndex(x => x.CollectionId);            // Collection filtering
clipCollection.EnsureIndex(x => x.FolderId);                // Folder navigation
clipCollection.EnsureIndex(x => x.SourceApplication);       // Application filtering
clipCollection.EnsureIndex("$.ContentText");                // Full-text search on text content
```

**Performance Validation**:
- Indexes reduce query time from O(n) scan to O(log n) btree lookup
- Full-text index enables <50ms search even with 100k+ clips
- ContentHash index makes duplicate detection O(1) operation
- Test with 100k clip dataset to validate <25ms query performance

---

## 4. WPF MVVM Architecture with CommunityToolkit.Mvvm

**Decision**: Use CommunityToolkit.Mvvm (formerly Microsoft.Toolkit.Mvvm) for MVVM implementation

**Rationale**:
- Official Microsoft MVVM toolkit with excellent performance
- Source generators eliminate boilerplate (ObservableProperty, RelayCommand attributes)
- Built-in messenger for loosely coupled component communication
- Supports async commands out of the box
- Widely adopted, well documented, active maintenance
- Integrates perfectly with Microsoft.Extensions.DependencyInjection

**Alternatives Considered**:
- **Prism**: More opinionated framework, adds complexity, overkill for desktop app
- **MVVMLight**: Legacy library, no longer maintained (last update 2018)
- **ReactiveUI**: Reactive programming paradigm, steep learning curve, different mental model
- **Manual MVVM implementation**: Too much boilerplate, error-prone property notification

**Architecture Pattern**:
```csharp
// ViewModel example with CommunityToolkit.Mvvm
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<ClipViewModel> _clips;
    
    [ObservableProperty]
    private string _searchText;
    
    [RelayCommand]
    private async Task SearchAsync(string query)
    {
        // Implementation with proper async/await
    }
}
```

**Dependency Injection**:
- Use `Microsoft.Extensions.DependencyInjection` for IoC container
- Register services in Startup.cs: `services.AddSingleton<IClipboardService, ClipboardService>()`
- ViewModels registered as transient: `services.AddTransient<MainWindowViewModel>()`
- Views resolve ViewModels via constructor injection or DataContext binding

---

## 5. UI Virtualization for Large Lists

**Decision**: WPF VirtualizingStackPanel with data virtualization for clip lists

**Rationale**:
- WPF ListView with VirtualizingStackPanel only renders visible items
- Reduces memory from O(n) to O(visible items) for large collections
- Maintains 60fps scrolling even with 100k+ items
- Built into WPF, no additional dependencies needed
- Supports smooth scrolling with pixel-based virtualization

**Implementation Requirements**:
```xaml
<ListView VirtualizingPanel.IsVirtualizing="True"
          VirtualizingPanel.VirtualizationMode="Recycling"
          VirtualizingPanel.ScrollUnit="Pixel">
```

**Additional Optimizations**:
- Lazy load clip content: Only load binary data when item is selected/visible
- Image thumbnail caching: Generate and cache thumbnails for image clips
- Async data loading: Use `async Task LoadClipsAsync()` to prevent UI blocking
- Paging strategy for search results: Load results in batches of 100

---

## 6. Async/Await Patterns for UI Responsiveness

**Decision**: Comprehensive async/await usage with proper ConfigureAwait and cancellation

**Rationale**:
- All I/O operations (database, file system, clipboard) must be async to prevent UI blocking
- WPF Dispatcher requires careful context management for UI updates
- Constitution requires 60fps (16ms frame time) - no blocking allowed
- Proper cancellation token usage enables responsive cancel operations

**Best Practices**:
```csharp
// Service layer uses ConfigureAwait(false) to avoid unnecessary context switches
public async Task<IEnumerable<Clip>> SearchClipsAsync(string query, CancellationToken ct)
{
    var results = await _repository
        .SearchAsync(query, ct)
        .ConfigureAwait(false);
    return results;
}

// ViewModel does NOT use ConfigureAwait - needs UI context for property updates
public async Task SearchCommandAsync(string query)
{
    IsLoading = true;  // Needs UI thread
    try
    {
        var results = await _searchService.SearchClipsAsync(query, _cts.Token);
        Clips = new ObservableCollection<ClipViewModel>(results.Select(ToViewModel));
    }
    finally
    {
        IsLoading = false;  // Needs UI thread
    }
}
```

**Cancellation Strategy**:
- Each long-running operation gets a CancellationTokenSource
- Cancel previous search when new search starts
- Cancel clipboard monitoring on application shutdown
- Proper disposal of CancellationTokenSource in Dispose methods

---

## 7. Memory Management for Large Clipboard Data

**Decision**: Multi-tiered caching with LRU eviction and configurable size limits

**Rationale**:
- Constitution requires <50MB baseline, <200MB with 10k+ clips
- Large images (50MB) and text (10MB) cannot all fit in memory
- Need intelligent caching to balance performance and memory usage
- LRU (Least Recently Used) eviction matches user access patterns

**Memory Management Strategy**:

1. **Database Storage (Persistent)**:
   - All clips stored in LiteDB with binary data
   - Images stored as compressed BsonBinary
   - Lazy loading: Only metadata loaded initially

2. **L1 Cache (Hot Data - Max 50MB)**:
   - Recently accessed clip content in memory
   - LRU eviction when limit reached
   - Full content including images

3. **L2 Cache (Thumbnails - Max 20MB)**:
   - Image thumbnails (200x200 max)
   - Never evicted during session
   - Generated on demand and cached

4. **UI Virtualization**:
   - ListView only renders visible items
   - Unload content when scrolled out of view

**Implementation**:
```csharp
public class ClipContentCache
{
    private readonly LruCache<Guid, byte[]> _contentCache;  // Max 50MB
    private readonly LruCache<Guid, BitmapImage> _thumbnailCache;  // Max 20MB
    
    public async Task<byte[]> GetContentAsync(Guid clipId)
    {
        if (_contentCache.TryGetValue(clipId, out var content))
            return content;
            
        content = await _repository.LoadContentAsync(clipId);
        _contentCache.AddOrUpdate(clipId, content);
        return content;
    }
}
```

---

## 8. Sound System with NAudio

**Decision**: NAudio 2.2+ for WAV file playback with volume control

**Rationale**:
- NAudio is mature, widely-used .NET audio library
- Supports WAV files natively (user customization requirement)
- Low latency playback suitable for UI feedback sounds
- Volume control per sound without affecting system volume
- Cross-thread playback safe with proper synchronization

**Alternatives Considered**:
- **System.Media.SoundPlayer**: Limited to WAV, no volume control, blocking playback
- **MediaPlayer (WPF)**: Too heavy for simple sound effects, designed for media apps
- **Direct Win32 PlaySound**: Requires P/Invoke, less control, no volume management

**Implementation Approach**:
```csharp
public class SoundService : ISoundService
{
    private readonly WaveOutEvent _waveOut;
    private float _volume = 0.5f;
    
    public async Task PlaySoundAsync(SoundEvent eventType)
    {
        if (!_settings.SoundsEnabled) return;
        
        var soundFile = _settings.GetSoundFile(eventType);
        using var audioFile = new AudioFileReader(soundFile);
        audioFile.Volume = _volume;
        
        await Task.Run(() => 
        {
            _waveOut.Init(audioFile);
            _waveOut.Play();
            while (_waveOut.PlaybackState == PlaybackState.Playing)
                Thread.Sleep(10);
        });
    }
}
```

---

## 9. High DPI Support and Theming

**Decision**: Per-monitor DPI awareness v2 with WPF automatic scaling

**Rationale**:
- Modern Windows systems commonly use 125%, 150%, or 200% scaling
- Per-monitor DPI v2 enables sharp rendering on mixed-DPI setups
- WPF handles DPI scaling automatically when properly configured
- Vector icons (XAML paths) scale perfectly at any DPI

**Configuration**:
```xml
<!-- app.manifest -->
<application xmlns="urn:schemas-microsoft-com:asm.v3">
  <windowsSettings>
    <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/PM</dpiAware>
    <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
  </windowsSettings>
</application>
```

**Theme Integration**:
- Detect Windows theme (light/dark) via registry monitoring
- Provide theme override in settings (light/dark/system)
- Use WPF resource dictionaries for theme switching
- System tray icon with multiple versions (light/dark background)

---

## 10. Testing Strategy

**Decision**: Three-tier testing approach - Unit (Core), Integration (Data), UI Automation (App)

**Rationale**:
- Constitution requires 90%+ code coverage
- Different testing strategies needed for different layers
- Unit tests for business logic (fast, isolated)
- Integration tests for database (slower, real LiteDB)
- UI automation for critical user workflows (slowest, end-to-end)

**Testing Stack**:
- **xUnit 2.6+**: Modern test framework with excellent async support
- **FluentAssertions**: Readable assertions: `result.Should().BeGreaterThan(100)`
- **Moq**: Mocking framework for interfaces
- **Bogus**: Fake data generation for test fixtures
- **FakeItEasy** (alternative to Moq): More fluent API for complex mocking scenarios

**Coverage Targets**:
- ClipMate.Core: 95%+ coverage (pure business logic)
- ClipMate.Data: 85%+ coverage (integration tests)
- ClipMate.Platform: 70%+ coverage (Win32 wrappers harder to test)
- ClipMate.App: 60%+ coverage (ViewModels testable, Views via UI automation)

**Performance Tests**:
```csharp
[Fact]
public async Task SearchClips_With100kItems_ReturnsWithin50ms()
{
    // Arrange
    await SeedDatabase(clipCount: 100_000);
    var stopwatch = Stopwatch.StartNew();
    
    // Act
    var results = await _searchService.SearchAsync("test query");
    
    // Assert
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(50);
}
```

---

## Research Summary

All technical decisions have been made with clear rationale and alternatives considered. The chosen stack balances:

- **Performance**: Win32 APIs, LiteDB indexing, WPF virtualization
- **Maintainability**: MVVM pattern, DI, clean architecture
- **Testability**: Interfaces, async/await, mocking support
- **User Experience**: Async operations, 60fps rendering, DPI awareness
- **Constitutional Compliance**: All performance and quality gates addressed

No unresolved NEEDS CLARIFICATION items remain. Ready to proceed to Phase 1 (Design & Contracts).
