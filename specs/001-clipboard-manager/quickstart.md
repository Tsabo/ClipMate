# Developer Quickstart: ClipMate Clipboard Manager

**Date**: 2025-11-11  
**Feature**: 001-clipboard-manager  
**Phase**: Phase 1 - Design & Contracts

## Getting Started

This guide helps developers set up their environment and understand the ClipMate architecture for rapid contribution.

---

## Prerequisites

### Required Software
- **Visual Studio 2022** (18.0+) or **VS Code** with C# Dev Kit
- **.NET 10.0 SDK** or later
- **Git** for version control
- **Windows 10** (version 1809+) or **Windows 11** (development target platform)

### Recommended Extensions (VS Code)
- C# Dev Kit (Microsoft)
- XAML (XAML Language Support)
- GitLens
- EditorConfig

---

## Initial Setup

### 1. Clone Repository
```powershell
git clone <repository-url> ClipMate
cd ClipMate
```

### 2. Restore Dependencies
```powershell
dotnet restore
```

### 3. Build Solution
```powershell
dotnet build
```

### 4. Run Tests
```powershell
dotnet test
```

### 5. Run Application
```powershell
cd src/ClipMate.App
dotnet run
```

---

## Solution Structure

```
ClipMate/
|-- Source/
|   ├── ClipMate.slx
|   ├── src/
|   │   ├── ClipMate.App/           # WPF UI layer (Views, ViewModels)
|   │   ├── ClipMate.Core/          # Business logic (Services, Models)
|   │   ├── ClipMate.Data/          # Data access (Repositories, LiteDB)
|   │   └── ClipMate.Platform/      # Windows-specific (Win32, System Tray)
|   ├── tests/
|   │   ├── ClipMate.Core.Tests/    # Unit tests for business logic
|   │   ├── ClipMate.Data.Tests/    # Integration tests for data layer
|   │   └── ClipMate.App.Tests/     # UI automation tests
├── docs/                           # Additional documentation
└── specs/                          # Feature specifications
```

---

## Architecture Overview

### Layered Architecture

**ClipMate.App (Presentation)**
- WPF Views (XAML)
- ViewModels (MVVM pattern with CommunityToolkit.Mvvm)
- Value Converters
- Attached Behaviors for drag-drop

**ClipMate.Core (Business Logic)**
- Service interfaces and implementations
- Domain models (Clip, Collection, Folder, etc.)
- Business rules and validation
- Platform-agnostic code

**ClipMate.Data (Data Access)**
- Repository interfaces and LiteDB implementations
- Database context management
- Query optimization
- Schema migrations

**ClipMate.Platform (Windows Integration)**
- Win32 API wrappers (P/Invoke)
- Clipboard monitoring
- Global hotkey registration
- System tray management

### Dependency Flow
```
App → Core → Data
App → Platform
App ← Core (events)
```

Key principle: **Core and Data have no dependencies on App or Platform**

---

## Key Design Patterns

### MVVM (Model-View-ViewModel)
```csharp
// View (MainWindow.xaml)
<Window DataContext="{Binding Source={StaticResource MainWindowViewModel}}">
    <Button Command="{Binding SearchCommand}" 
            CommandParameter="{Binding SearchText}" />
</Window>

// ViewModel (MainWindowViewModel.cs)
public partial class MainWindowViewModel : ObservableObject
{
    private readonly ISearchService _searchService;
    
    [ObservableProperty]
    private string _searchText;
    
    [RelayCommand]
    private async Task SearchAsync()
    {
        var results = await _searchService.SearchAsync(SearchText, options);
        // Update UI-bound properties
    }
}
```

### Dependency Injection
```csharp
// Startup.cs - Service registration
services.AddSingleton<IClipboardService, ClipboardService>();
services.AddSingleton<ISearchService, SearchService>();
services.AddTransient<MainWindowViewModel>();

// ViewModel - Constructor injection
public MainWindowViewModel(
    IClipboardService clipboardService,
    ISearchService searchService)
{
    _clipboardService = clipboardService;
    _searchService = searchService;
}
```

### Repository Pattern
```csharp
// Interface in Core
public interface IClipRepository
{
    Task<Clip?> GetByIdAsync(Guid id);
    Task<IEnumerable<Clip>> SearchAsync(string query);
}

// Implementation in Data
public class ClipRepository : IClipRepository
{
    private readonly LiteDbContext _context;
    // LiteDB-specific implementation
}
```

---

## Common Development Tasks

### Adding a New Service

1. **Define interface in Core/Services**:
```csharp
public interface INewService
{
    Task DoSomethingAsync();
}
```

2. **Implement in Core/Services**:
```csharp
public class NewService : INewService
{
    public async Task DoSomethingAsync()
    {
        // Implementation
    }
}
```

3. **Register in Startup.cs**:
```csharp
services.AddSingleton<INewService, NewService>();
```

4. **Use via DI**:
```csharp
public class MyViewModel
{
    private readonly INewService _newService;
    
    public MyViewModel(INewService newService)
    {
        _newService = newService;
    }
}
```

### Adding a New Entity

1. **Define model in Core/Models**:
```csharp
public class NewEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public DateTime Created { get; set; }
}
```

2. **Add repository interface in Core/Repositories**:
```csharp
public interface INewEntityRepository
{
    Task<NewEntity> CreateAsync(NewEntity entity);
    Task<NewEntity?> GetByIdAsync(Guid id);
}
```

3. **Implement repository in Data/LiteDB**:
```csharp
public class NewEntityRepository : INewEntityRepository
{
    private readonly LiteDbContext _context;
    private ILiteCollection<NewEntity> Collection => 
        _context.Database.GetCollection<NewEntity>("entities");
    
    public async Task<NewEntity> CreateAsync(NewEntity entity)
    {
        await Task.Run(() => Collection.Insert(entity));
        return entity;
    }
}
```

4. **Add indexing** (if needed):
```csharp
Collection.EnsureIndex(x => x.Name);
```

### Adding a New View/ViewModel

1. **Create ViewModel in App/ViewModels**:
```csharp
public partial class NewWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title;
    
    [RelayCommand]
    private async Task SaveAsync()
    {
        // Implementation
    }
}
```

2. **Create View in App/Views**:
```xaml
<Window x:Class="ClipMate.App.Views.NewWindow"
        xmlns:vm="clr-namespace:ClipMate.App.ViewModels">
    <Window.DataContext>
        <vm:NewWindowViewModel />
    </Window.DataContext>
    
    <TextBox Text="{Binding Title}" />
    <Button Command="{Binding SaveCommand}" Content="Save" />
</Window>
```

3. **Register ViewModel in DI** (Startup.cs):
```csharp
services.AddTransient<NewWindowViewModel>();
```

---

## Testing Guidelines

### Unit Tests (Core.Tests)

```csharp
public class ClipboardServiceTests
{
    private readonly Mock<IClipRepository> _mockRepository;
    private readonly ClipboardService _service;
    
    public ClipboardServiceTests()
    {
        _mockRepository = new Mock<IClipRepository>();
        _service = new ClipboardService(_mockRepository.Object);
    }
    
    [Fact]
    public async Task StartMonitoring_ShouldRaiseClipCapturedEvent()
    {
        // Arrange
        ClipCapturedEventArgs? capturedArgs = null;
        _service.ClipCaptured += (s, e) => capturedArgs = e;
        
        // Act
        await _service.StartMonitoringAsync();
        // Simulate clipboard change...
        
        // Assert
        capturedArgs.Should().NotBeNull();
        capturedArgs.Clip.Should().NotBeNull();
    }
}
```

### Integration Tests (Data.Tests)

```csharp
public class ClipRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly LiteDbContext _context;
    private readonly ClipRepository _repository;
    
    public ClipRepositoryTests()
    {
        _testDbPath = Path.GetTempFileName();
        _context = new LiteDbContext(_testDbPath);
        _repository = new ClipRepository(_context);
    }
    
    [Fact]
    public async Task CreateAsync_ShouldPersistClip()
    {
        // Arrange
        var clip = new Clip { Id = Guid.NewGuid(), /* ... */ };
        
        // Act
        await _repository.CreateAsync(clip);
        var retrieved = await _repository.GetByIdAsync(clip.Id);
        
        // Assert
        retrieved.Should().NotBeNull();
        retrieved.Id.Should().Be(clip.Id);
    }
    
    public void Dispose()
    {
        _context.Dispose();
        File.Delete(_testDbPath);
    }
}
```

### Performance Tests

```csharp
[Fact]
public async Task SearchAsync_With100kClips_CompletesUnder50ms()
{
    // Arrange
    await SeedDatabase(clipCount: 100_000);
    var stopwatch = Stopwatch.StartNew();
    
    // Act
    var results = await _searchService.SearchAsync("test");
    
    // Assert
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(50,
        "Search must complete within 50ms per constitution");
}
```

---

## Debugging Tips

### Clipboard Monitoring
- Use **OutputDebugString** or **Debug.WriteLine** to trace clipboard events
- Test with multiple applications (Notepad, Visual Studio, browsers)
- Verify debouncing works (apps that set clipboard multiple times)

### Database Inspection
- Use **LiteDB.Studio** (GUI tool) to inspect database files
- Located in: `%APPDATA%/ClipMate/collections/*.db`
- Check indexes with: `db.GetCollection("clips").EnsureIndex(...)`

### WPF Binding Issues
- Enable WPF binding tracing in App.xaml.cs:
```csharp
PresentationTraceSources.SetTraceLevel(binding, PresentationTraceLevel.High);
```
- Use **Snoop** tool for live visual tree inspection

### Performance Profiling
- Use Visual Studio **Performance Profiler** (Alt+F2)
- Focus on CPU usage during clipboard capture and search
- Monitor memory allocation for large clip collections

---

## Code Style and Conventions

### Naming Conventions
- **Classes**: PascalCase (e.g., `ClipboardService`)
- **Interfaces**: IPascalCase (e.g., `IClipRepository`)
- **Private fields**: _camelCase (e.g., `_searchService`)
- **Properties**: PascalCase (e.g., `SearchText`)
- **Methods**: PascalCase (e.g., `SearchAsync`)
- **Async methods**: Must end with "Async" suffix

### Code Organization
- One class per file
- Group related files in folders
- Use `#region` sparingly (only for large classes)
- Keep files under 500 lines (split if larger)

### Comments
```csharp
// Use XML comments for public APIs
/// <summary>
/// Searches clips by content with optional filters
/// </summary>
/// <param name="query">Search query text</param>
/// <returns>Matching clips ordered by relevance</returns>
public async Task<SearchResult> SearchAsync(string query);

// Use inline comments for complex logic
// Calculate hash using SHA256 for duplicate detection
var hash = ComputeContentHash(content);
```

### Async/Await Best Practices
```csharp
// ✅ Good: ConfigureAwait(false) in libraries
public async Task<Clip> GetClipAsync(Guid id)
{
    var clip = await _repository.GetByIdAsync(id).ConfigureAwait(false);
    return clip;
}

// ✅ Good: No ConfigureAwait in ViewModels (need UI context)
public async Task LoadClipsAsync()
{
    var clips = await _clipService.GetClipsAsync();
    Clips = new ObservableCollection<Clip>(clips);  // Needs UI thread
}

// ✅ Good: CancellationToken support
public async Task SearchAsync(CancellationToken ct)
{
    ct.ThrowIfCancellationRequested();
    var results = await _repository.SearchAsync(query, ct);
}
```

---

## Common Gotchas

### WPF Dispatcher Threading
```csharp
// ❌ Wrong: Updating UI from background thread
Task.Run(() => {
    Clips.Add(newClip);  // Exception: Wrong thread!
});

// ✅ Right: Use dispatcher or await in ViewModel
var clips = await Task.Run(() => _repository.GetClips());
Clips.Clear();
foreach (var clip in clips)
    Clips.Add(clip);  // On UI thread
```

### LiteDB Concurrency
```csharp
// ❌ Wrong: Shared LiteDatabase instance across threads
var db = new LiteDatabase("data.db");
Parallel.ForEach(items, item => {
    db.GetCollection<Clip>().Insert(item);  // Not thread-safe!
});

// ✅ Right: Use lock or separate instances
lock (_dbLock)
{
    _db.GetCollection<Clip>().Insert(clip);
}
```

### Memory Leaks
```csharp
// ❌ Wrong: Not unsubscribing from events
_clipboardService.ClipCaptured += OnClipCaptured;

// ✅ Right: Unsubscribe in Dispose
public void Dispose()
{
    _clipboardService.ClipCaptured -= OnClipCaptured;
}
```

---

## Resources

### Documentation
- [WPF Documentation](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)
- [LiteDB Documentation](https://www.litedb.org/docs/)
- [xUnit Documentation](https://xunit.net/docs/getting-started/netcore/cmdline)

### Tools
- [LiteDB.Studio](https://github.com/mbdavid/LiteDB.Studio) - Database viewer
- [Snoop](https://github.com/snoopwpf/snoopwpf) - WPF visual tree inspector
- [BenchmarkDotNet](https://benchmarkdotnet.org/) - Performance benchmarking

### Community
- GitHub Issues - Bug reports and feature requests
- GitHub Discussions - Questions and general discussion

---

## Quick Reference

### Build Commands
```powershell
# Restore dependencies
dotnet restore

# Build debug
dotnet build

# Build release
dotnet build -c Release

# Run tests
dotnet test

# Run with logging
dotnet run --project Source/src/ClipMate.App --verbosity detailed

# Publish self-contained
dotnet publish -c Release -r win-x64 --self-contained
```

### Useful PowerShell Scripts
```powershell
# Clean all bin/obj folders
Get-ChildItem -Recurse -Directory -Include bin,obj | Remove-Item -Recurse -Force

# Count lines of code
(Get-ChildItem -Recurse -Include *.cs | Select-String .).Count

# Find TODOs
Get-ChildItem -Recurse -Include *.cs | Select-String "TODO"
```

---

## Next Steps

1. **Read the Constitution** (`.specify/memory/constitution.md`) - Understand core principles
2. **Review Data Model** (`specs/001-clipboard-manager/data-model.md`) - Learn entity structure
3. **Explore Service Contracts** (`specs/001-clipboard-manager/contracts/`) - Understand service interfaces
4. **Run the Application** - See it in action
5. **Pick a Task** - Start with `/speckit.tasks` when available
6. **Write Tests First** - TDD is mandatory per constitution

---

## Getting Help

- Check existing GitHub Issues and Discussions
- Review the specification documents in `specs/001-clipboard-manager/`
- Consult the constitution for architectural decisions
- Ask questions in team chat or create a GitHub Discussion

**Happy coding! 🚀**
