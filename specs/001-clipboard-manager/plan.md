# Implementation Plan: ClipMate Clipboard Manager

**Branch**: `001-clipboard-manager` | **Date**: 2025-11-11 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-clipboard-manager/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

ClipMate is a Windows desktop clipboard management application that automatically captures, organizes, and provides instant access to clipboard history. The application uses a three-pane interface (tree/list/preview) for browsing thousands of clipboard items, with PowerPaste popup for quick access from any application. Technical approach leverages .NET 10 with WPF for UI, LiteDB for embedded database storage, Win32 APIs for clipboard monitoring, and MVVM architecture for maintainability and testability.

## Technical Context

**Language/Version**: C# 14+ with .NET 10.0 SDK, nullable reference types enabled  
**Primary Dependencies**: 
- WPF (Windows Presentation Foundation) for UI framework
- CommunityToolkit.Mvvm 8.2+ for MVVM implementation
- LiteDB 5.0+ for embedded NoSQL database
- NAudio 2.2+ for sound playback system
- Microsoft.Extensions.DependencyInjection 8.0+ for IoC container
- GlobalHotKeys or custom Win32 interop for system-wide hotkeys

**Storage**: LiteDB embedded database with separate files per collection, binary blob storage for images/files  
**Testing**: xUnit 2.6+ with FluentAssertions, Moq for mocking, WPF UI automation for integration tests  
**Target Platform**: Windows 10 (version 1809+) and Windows 11, x64 architecture  
**Project Type**: Single desktop application with WPF UI  
**Performance Goals**: 
- Clipboard capture: <100ms response time
- Search operations: <50ms with 100k+ items
- UI rendering: 60fps (16ms frame time)
- Startup time: <2 seconds to system tray

**Constraints**: 
- Memory: <50MB baseline, <200MB with 10k+ clips
- CPU: <1% idle, <5% during capture
- Disk I/O: Async operations only, no UI blocking
- Threading: All UI operations on dispatcher thread

**Scale/Scope**: 
- Support 100,000+ clips per collection
- Multiple collections (typical 3-5, max 20)
- Text clips up to 10MB, images up to 50MB
- 50+ UI screens/dialogs/windows
- ~25,000 LOC estimated

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Code Quality & Architecture Gates:**
- [x] SOLID principles compliance verified in design - Multi-project structure enforces SRP, interfaces enable DIP/OCP, MVVM separates concerns
- [x] Unit test strategy defined with 90%+ coverage target - xUnit with Moq for all Core services, FluentAssertions for readability
- [x] Async/await patterns planned for heavy operations - All database I/O, clipboard monitoring, and file operations use async/await
- [x] Separation of concerns documented (UI/business/data layers) - ClipMate.App (UI), ClipMate.Core (business), ClipMate.Data (persistence)
- [x] Memory management strategy defined for large datasets - UI virtualization, lazy loading, image caching with size limits, binary storage in DB

**Performance Gates:**
- [x] Clipboard capture <100ms requirement planned - Win32 RegisterClipboardFormatListener with background thread processing
- [x] Search <50ms requirement with indexing strategy - LiteDB full-text indexing with stemming, result caching, optimized queries
- [x] UI responsiveness plan (background operations, 60fps) - All heavy work on background threads, WPF virtualization, async commands
- [x] Memory footprint targets defined (<50MB baseline, <200MB max) - Lazy loading, image pooling, configurable cache sizes, memory profiling in tests

**Platform Integration Gates:**
- [x] Windows clipboard API integration strategy defined - Win32 RegisterClipboardFormatListener in ClipMate.Platform with proper cleanup
- [x] Global hotkey implementation approach documented - Win32 RegisterHotKey API with conflict detection and fallback handling
- [x] System tray and DPI awareness requirements addressed - NotifyIcon integration, per-monitor DPI awareness v2, vector icons
- [x] Sound system integration planned - NAudio with async playback, volume control, WAV file support, user customization

**Data Management Gates:**
- [x] Database schema supports performance requirements - LiteDB with indexes on timestamp, content hash, collection/folder for <25ms queries
- [x] Backup and recovery strategy defined - Automatic exports, corruption detection, schema migrations, separate collection files
- [x] Security approach for sensitive data documented - Optional encryption for sensitive clips, secure deletion, configurable data retention

✅ **All gates passed** - Design meets all constitutional requirements

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── ClipMate.App/                    # Main WPF application project
│   ├── App.xaml                     # Application entry point and resources
│   ├── App.xaml.cs
│   ├── Views/                       # WPF Windows and UserControls
│   │   ├── MainWindow.xaml          # Main three-pane shell
│   │   ├── PowerPasteWindow.xaml    # Global popup menu
│   │   ├── Controls/                # Reusable UI controls
│   │   │   ├── ClipListView.xaml
│   │   │   ├── CollectionTreeView.xaml
│   │   │   └── PreviewPane.xaml
│   │   └── Dialogs/                 # Modal dialogs
│   │       ├── SettingsDialog.xaml
│   │       ├── NewCollectionDialog.xaml
│   │       └── TemplateEditorDialog.xaml
│   ├── ViewModels/                  # MVVM ViewModels
│   │   ├── MainWindowViewModel.cs
│   │   ├── PowerPasteViewModel.cs
│   │   ├── ClipListViewModel.cs
│   │   └── CollectionTreeViewModel.cs
│   ├── Converters/                  # WPF value converters
│   ├── Behaviors/                   # Attached behaviors for drag-drop
│   ├── Resources/                   # Images, sounds, icons
│   └── Startup.cs                   # DI container configuration
│
├── ClipMate.Core/                   # Business logic and domain models
│   ├── Models/                      # Domain entities
│   │   ├── Clip.cs
│   │   ├── Collection.cs
│   │   ├── Folder.cs
│   │   ├── Template.cs
│   │   └── ClipboardFormat.cs
│   ├── Services/                    # Business logic services
│   │   ├── IClipboardService.cs
│   │   ├── ClipboardService.cs      # Clipboard monitoring
│   │   ├── ISearchService.cs
│   │   ├── SearchService.cs         # Full-text search with indexing
│   │   ├── IHotkeyService.cs
│   │   ├── HotkeyService.cs         # Global hotkey registration
│   │   ├── ISoundService.cs
│   │   ├── SoundService.cs          # Audio cue playback
│   │   ├── ITemplateService.cs
│   │   └── TemplateService.cs       # Template variable expansion
│   ├── Repositories/                # Data access interfaces
│   │   ├── IClipRepository.cs
│   │   ├── ICollectionRepository.cs
│   │   └── ITemplateRepository.cs
│   └── Utilities/                   # Helper classes
│       ├── ClipboardFormatHelper.cs
│       ├── TextTransformations.cs
│       └── Win32Interop.cs          # P/Invoke declarations
│
├── ClipMate.Data/                   # Data access layer
│   ├── LiteDB/                      # LiteDB implementation
│   │   ├── LiteDbContext.cs
│   │   ├── ClipRepository.cs
│   │   ├── CollectionRepository.cs
│   │   └── TemplateRepository.cs
│   ├── Migrations/                  # Schema migration scripts
│   └── Indexing/                    # Search index management
│
└── ClipMate.Platform/               # Windows-specific integration
    ├── Win32/                       # Win32 API wrappers
    │   ├── ClipboardMonitor.cs      # RegisterClipboardFormatListener
    │   ├── HotkeyManager.cs         # RegisterHotKey/UnregisterHotKey
    │   └── SystemTrayManager.cs     # NotifyIcon integration
    └── DpiAwareness.cs              # High DPI support

tests/
├── ClipMate.Core.Tests/             # Unit tests for business logic
│   ├── Services/
│   │   ├── ClipboardServiceTests.cs
│   │   ├── SearchServiceTests.cs
│   │   └── TemplateServiceTests.cs
│   └── Models/
│       └── ClipTests.cs
│
├── ClipMate.Data.Tests/             # Integration tests for data access
│   ├── Repositories/
│   │   ├── ClipRepositoryTests.cs
│   │   └── CollectionRepositoryTests.cs
│   └── Performance/
│       └── SearchPerformanceTests.cs
│
└── ClipMate.App.Tests/              # UI automation tests
    ├── ViewModels/
    │   └── MainWindowViewModelTests.cs
    └── Integration/
        ├── ClipboardCaptureTests.cs
        └── PowerPasteTests.cs

docs/                                # Additional documentation
├── architecture.md                  # Architecture diagrams and decisions
├── api-reference.md                 # Public API documentation
└── deployment.md                    # Build and deployment guide
```

**Structure Decision**: Using a multi-project solution structure to enforce clean separation of concerns per the constitution. ClipMate.Core contains all platform-agnostic business logic and is fully unit-testable. ClipMate.Data provides data access abstraction with LiteDB implementation. ClipMate.Platform isolates Windows-specific Win32 APIs for potential cross-platform work. ClipMate.App is the WPF presentation layer following MVVM pattern with proper data binding.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No constitutional violations. All design decisions align with constitutional requirements. The four-project structure is justified by clean separation of concerns and follows standard .NET architectural patterns.

---

## Phase Completion Summary

### Phase 0: Research ✅ Complete
- **Output**: `research.md` - 10 technical decisions with rationale
- **Status**: All NEEDS CLARIFICATION items resolved
- **Key Decisions**: Win32 clipboard APIs, LiteDB with indexing, WPF MVVM with CommunityToolkit.Mvvm, async/await patterns

### Phase 1: Design & Contracts ✅ Complete
- **Output**: 
  - `data-model.md` - 7 core entities with full validation rules and indexing strategy
  - `contracts/service-interfaces.md` - 10 service interfaces defining business logic contracts
  - `quickstart.md` - Developer onboarding guide with architecture overview
- **Status**: All design artifacts complete, agent context updated
- **Constitutional Re-Check**: All gates passed post-design

### Phase 2: Task Breakdown
**Status**: Ready for `/speckit.tasks` command
- Specification complete with 8 prioritized user stories
- Implementation plan complete with architecture and contracts
- All prerequisites met for task generation

### Artifacts Generated

| Document | Purpose | Status |
|----------|---------|--------|
| `plan.md` | Implementation plan and architecture | ✅ Complete |
| `research.md` | Technical research and decisions | ✅ Complete |
| `data-model.md` | Entity definitions and database schema | ✅ Complete |
| `contracts/service-interfaces.md` | Service interface contracts | ✅ Complete |
| `quickstart.md` | Developer onboarding guide | ✅ Complete |
| `.github/copilot-instructions.md` | Agent context (auto-updated) | ✅ Complete |

### Next Steps

Run `/speckit.tasks` to generate the task breakdown based on this plan. The task generator will:
1. Create tasks organized by user story priority (P1, P2, P3, P4)
2. Define foundational tasks (setup, database, core services)
3. Break down each user story into implementation tasks
4. Include test tasks per constitutional TDD requirements
5. Establish dependencies and parallel execution opportunities

**Branch**: `001-clipboard-manager`  
**Ready for**: Task generation and implementation
