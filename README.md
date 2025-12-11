# ClipMate

A modern Windows clipboard manager built with .NET 9 and WPF, designed to be a powerful recreation of the classic ClipMate utility with contemporary technologies and UX patterns.

## Features

### Core Functionality ✅
- **Automatic Clipboard Capture**: Real-time monitoring of clipboard changes with support for text, images, rich text, HTML, and file lists
- **Persistent History**: All clipboard items stored in SQLite database with Entity Framework Core
- **System Tray Integration**: Runs silently in background with tray icon, context menu, and notifications
- **Application Lifecycle**: Single-instance enforcement, start with Windows, minimize to tray

### User Interface 🎨
- **Three-Pane Explorer**: ClipMate Explorer interface with collection tree, clip list (DataGrid), and multi-format preview pane
- **DevExpress WPF Controls**: Professional UI components with built-in theming and rich editing capabilities
- **Multiple View Modes**: Detailed list, icons, and thumbnails for clip viewing
- **Text Tools**: Advanced text manipulation (case conversion, trimming, line operations, encoding/decoding, etc.)
- **Template System**: Create and manage reusable text templates with variable substitution
- **Dual ClipList Mode**: Side-by-side clip comparison and management

### Data Management 📊
- **Collections**: Organize clips into multiple databases/workspaces
- **Folders**: Hierarchical organization within collections
- **Smart Duplicate Detection**: SHA256 content hashing prevents duplicate storage
- **Application Filters**: Exclude clipboard capture from specific applications
- **Rich Metadata**: Track source application, capture timestamp, and content type

### Content Handling 📋
- **Text Formats**: Plain text, RTF, HTML with syntax highlighting and preview
- **Image Support**: Bitmap capture with thumbnail generation and zoom viewer
- **File Lists**: Capture and display file paths from Windows Explorer
- **Preview Modes**: Dedicated tabs for Text, Rich Text, HTML, Image, and File previews
- **HexEditor Integration**: Binary content viewing for advanced users

### Planned Features 🚧
- **ClipBar Quick Access** (002-clipbar-quick-access): Global hotkey popup for instant paste access
- **WPF UI Migration** (003-wpfui-migration): Modern Fluent Design System integration
- **Search & Filtering**: Full-text search across collections
- **PowerPaste**: Advanced paste transformations
- **Sound Feedback**: Audio cues for clipboard operations
- **Global Hotkeys**: Customizable keyboard shortcuts

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- Windows 10 (1809+) or Windows 11
- Visual Studio 2025+ or VS Code with C# Dev Kit (for development)

## Architecture

ClipMate follows clean architecture principles with strict separation of concerns:

```
Source/
├── src/
│   ├── ClipMate.App/              # WPF UI layer (Views, ViewModels, Controls)
│   ├── ClipMate.Core/             # Business logic & domain models
│   ├── ClipMate.Data/             # EF Core entities, DbContext, repositories
│   ├── ClipMate.Data.Schema/      # Database schema migration tools
│   └── ClipMate.Platform/         # Windows-specific Win32 interop
└── tests/
    ├── ClipMate.Tests.Unit/       # Unit tests (TUnit framework)
    ├── ClipMate.Tests.Unit.Schema/ # Schema tests
    └── ClipMate.Tests.Integration/ # Integration tests
```

### Technology Stack

**UI Framework**
- **WPF**: Windows Presentation Foundation with .NET 9
- **DevExpress WPF**: Professional UI controls (GridControl, RichTextEdit, HtmlEdit, etc.)
- **WPF-UI 4.0.3**: Fluent Design System components (in migration)
- **MVVM**: CommunityToolkit.Mvvm 8.4.0 with source generators

**Data Layer**
- **Database**: Entity Framework Core 9.0.0 + SQLite (migrated from LiteDB per ADR-001)
- **ORM**: Full EF Core feature set (migrations, change tracking, LINQ)
- **Schema Migration**: Custom schema migration system via ClipMate.Data.Schema
- **Location**: `%LOCALAPPDATA%\ClipMate\clipmate.db`

**Platform Integration**
- **Win32 APIs**: CsWin32 0.3.248 for code-generated P/Invoke (replaced manual declarations per ADR-005)
- **Clipboard Monitor**: `WM_CLIPBOARDUPDATE` message handling
- **Hotkey Manager**: `RegisterHotKey` Win32 API wrapper
- **System Tray**: Windows Forms NotifyIcon
- **Startup Manager**: Registry integration for "Start with Windows"

**Infrastructure**
- **DI Container**: Microsoft.Extensions.DependencyInjection 9.0.0 (per ADR-003)
- **Logging**: Microsoft.Extensions.Logging 9.0.0 (Console + Debug providers, per ADR-004)
- **Configuration**: Microsoft.Extensions.Hosting 9.0.0
- **Audio**: NAudio 2.2.1 for sound playback

**Testing**
- **Test Framework**: TUnit 0.4.31 (modern alternative to xUnit, migrated from xUnit)
- **Mocking**: Moq 4.20.72
- **Current Status**: 556 tests (552 passing, 4 skipped)

**Additional Libraries**
- **Emoji.Wpf**: Emoji rendering in WPF
- **WebView2**: HTML preview with Microsoft Edge WebView2
- **WpfHexaEditor**: Binary content viewing
- **Tomlyn**: TOML configuration parsing

### Recent Architecture Achievements

**December 2025 - OptionsViewModel Refactoring**
- ✅ **Major Refactoring**: Decomposed 779-line "God Object" OptionsViewModel into clean coordinator pattern
- ✅ **Result**: 83% code reduction (779 → 129 lines) with improved maintainability
- ✅ **Child ViewModels Created**: 7 specialized ViewModels following single responsibility principle
  - `GeneralOptionsViewModel` - Startup, layout, confirmations, updates
  - `PowerPasteOptionsViewModel` - PowerPaste configuration
  - `QuickPasteOptionsViewModel` - QuickPaste targets and formatting
  - `EditorOptionsViewModel` - Monaco and ClipViewer settings
  - `CapturingOptionsViewModel` - Clipboard capture preferences
  - `ApplicationProfilesOptionsViewModel` - Application-specific profiles
  - `SoundsOptionsViewModel` - Sound event configuration
- ✅ **Pattern**: Parent coordinates LoadAsync/SaveAsync across all children
- ✅ **Tests**: All 556 tests passing with updated test fixtures

### Package Management

This project uses **Central Package Management** (CPM) via `Directory.Packages.props`. All package versions are centrally managed at the repository root for consistency.

## Getting Started

### Clone the Repository

```powershell
git clone https://github.com/yourusername/ClipMate.git
cd ClipMate
```

### Build the Solution

```powershell
cd Source
dotnet restore
dotnet build
```

### Run the Application

```powershell
cd src/ClipMate.App
dotnet run
```

### Run Tests

Run all tests:
```powershell
cd Source
dotnet test
```

Run unit tests only:
```powershell
dotnet test tests/ClipMate.Tests.Unit
```

Run integration tests only:
```powershell
dotnet test tests/ClipMate.Tests.Integration
```

Run tests with coverage:
```powershell
dotnet test --collect:"XPlat Code Coverage"
```

## Development

### Code Style

This project follows the **Implementation Policy** (see `.github/copilot-instructions.md`):
1. **Microsoft-First**: Prefer built-in .NET libraries over third-party solutions
2. **Approval Required**: All third-party packages require explicit approval
3. **Document Decisions**: Architecture decisions are documented and tracked

Code formatting is enforced via `.editorconfig`:
- **Nullable Reference Types**: Enabled project-wide (C# 14)
- **Naming**: Interfaces start with `I`, private fields start with `_`
- **Indentation**: 4 spaces for C#, 2 spaces for XML/JSON
- **Braces**: Allman style (braces on new line)

### Testing Approach

ClipMate follows **Test-Driven Development (TDD)** with strict requirements:

1. **90%+ Code Coverage**: Mandatory per ClipMate Constitution
2. **Write Tests First**: Red → Green → Refactor cycle
3. **Test Framework**: TUnit (modern alternative to xUnit)
4. **Mocking**: Moq 4.20.72
5. **Assertions**: Fluent assertions for readability

Test organization:
- `ClipMate.Tests.Unit/` - Unit tests for business logic
- `ClipMate.Tests.Unit.Schema/` - Database schema tests
- `ClipMate.Tests.Integration/` - Win32 API and full-stack integration tests

### Project References

```
ClipMate.App
├── ClipMate.Core (business logic & service interfaces)
├── ClipMate.Data (EF Core entities, DbContext, repositories)
└── ClipMate.Platform (Win32 interop)

ClipMate.Data
└── ClipMate.Core

ClipMate.Data.Schema
└── ClipMate.Core

ClipMate.Platform
└── ClipMate.Core

Tests (Unit & Integration)
├── ClipMate.Core
├── ClipMate.Data
├── ClipMate.Data.Schema
└── ClipMate.Platform
```

**Key Principles:**
- Core and Data.Schema have no dependencies on App or Platform
- Business logic is platform-agnostic and testable
- Win32 APIs isolated in Platform layer
- All projects use Central Package Management

## Database

ClipMate uses **Entity Framework Core 9.0** with **SQLite** for persistent storage:

- **Location**: `%LOCALAPPDATA%\ClipMate\clipmate.db`
- **Schema**: Relational with custom schema migration system
- **Migrations**: Automatic schema migration via `DatabaseSchemaMigrationService` using `ClipMate.Data.Schema` tools
- **Context**: `ClipMateDbContext` with scoped lifetime in DI container

### Schema Migration

ClipMate uses a custom schema migration system that automatically detects and applies schema changes:
- Compares current database schema with EF Core model
- Generates and executes SQL migrations automatically
- No manual migration steps required
- Handles tables, columns, indexes, and foreign keys

### Database Reset Script

```powershell
# Deletes database and resets to clean state
.\scripts\reset-database.ps1
```

## Architecture Decisions

Key architectural decisions made during development:

- **ADR-001**: Entity Framework Core 9.0 + SQLite (replaced LiteDB)
- **ADR-002**: CommunityToolkit.Mvvm 8.4.0 (Microsoft MVVM toolkit)
- **ADR-003**: Microsoft.Extensions.DependencyInjection (standard .NET DI)
- **ADR-004**: Microsoft.Extensions.Logging (replaced custom logging)
- **ADR-005**: Microsoft.Windows.CsWin32 (replaced manual P/Invoke)

## Key Features Deep Dive

### Clipboard Monitoring
ClipMate uses Win32 `WM_CLIPBOARDUPDATE` messages for real-time clipboard change detection:
- Low overhead, no polling
- Captures clipboard content immediately upon change
- Supports multiple clipboard formats in priority order
- Thread-safe clipboard access with retry logic

### Duplicate Detection
SHA256 content hashing prevents duplicate clips:
- Hash calculated on raw content bytes
- Database query checks for existing hash before insertion
- Efficient storage utilization
- Fast duplicate lookup via indexed hash column

### Application Filtering
Exclude clipboard capture from specific applications:
- Match by process name (e.g., `KeePass.exe`)
- Match by window title pattern
- Configured via ApplicationFilter entity
- Prevents capturing sensitive data (passwords, PINs)

### Multi-Format Support
Captures all common clipboard formats:
- **Text**: Plain text (CF_UNICODETEXT)
- **Rich Text**: RTF formatting (CF_RTF)
- **HTML**: HTML with metadata (CF_HTML)
- **Images**: Bitmap data (CF_BITMAP, CF_DIB)
- **Files**: File paths from Explorer (CF_HDROP)

### DevExpress Integration
Professional UI components provide rich functionality:
- **GridControl**: High-performance data grid with sorting, filtering, grouping
- **RichEditControl**: Full-featured RTF editor
- **HtmlEditControl**: HTML editing and preview
- **ImageEditControl**: Image display with zoom and pan
- **Theming**: Office2019Colorful theme applied globally

## Contributing

### Development Workflow

1. **Fork & Clone**: Fork the repository and clone your fork
2. **Branch**: Create feature branch from `master`
3. **Implement**: Follow TDD approach (tests first, implementation second)
4. **Test**: Ensure 90%+ code coverage and all tests pass
5. **Document**: Update documentation as needed
6. **Commit**: Use conventional commits format
7. **Push & PR**: Push to your fork and open a Pull Request

### Before Submitting PR

```powershell
# Build solution
cd Source
dotnet build

# Run all tests
dotnet test

# Check code coverage (optional)
dotnet test --collect:"XPlat Code Coverage"
```

### Commit Message Format

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>

[optional body]

[optional footer]
```

**Types**: `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`, `build`  
**Scope**: `core`, `app`, `platform`, `data`, `tests`, `docs`

**Examples:**
```
feat(app): add text tools dialog with case conversion
fix(platform): prevent clipboard monitor crash on bitmap errors
docs(readme): update installation instructions
test(core): add template service unit tests
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Development Progress

### Feature 001: Clipboard Manager (Active Development)

**Completed Phases:**
- ✅ **Phase 1**: Project Setup - Solution structure, multi-project configuration, Central Package Management
- ✅ **Phase 2**: Foundational Infrastructure - Core models, EF Core DbContext, repositories, service interfaces, Win32 platform layer, MVVM infrastructure, DI setup
- ✅ **Phase 3**: Clipboard Capture (User Story 1) - Real-time clipboard monitoring, multi-format support, duplicate detection, application filters, persistent storage

**In Progress:**
- 🚧 **Phase 4**: ClipMate Explorer Interface (~70% complete)
  - ✅ Three-pane layout (Tree, List, Preview)
  - ✅ System tray integration with NotifyIcon
  - ✅ Collection tree view
  - ✅ Clip list DataGrid with sorting/filtering
  - ✅ Multi-format preview pane (Text, RTF, HTML, Image, Files)
  - ✅ Options dialog with 7 configuration tabs
  - 🚧 Main menu implementation (File, Edit, View, Tools, Help)
  - 🚧 Toolbar implementation
  - 🚧 ClipList context menu (Copy, Delete, Edit, Properties, etc.)
  - 🚧 Collection tree context menu (New, Rename, Delete, Properties)

**Upcoming Phases:**
- 📋 **Phase 5**: Search & Discovery - Full-text search, filtering, date ranges
- 📋 **Phase 6**: Collections & Folders - Multi-collection management, drag-drop organization
- 📋 **Phase 7**: Template System Enhancements - Additional variables, advanced substitution
- 📋 **Phase 8**: Sound Feedback - Add sound files for audio cues
- 📋 **Phase 9**: Global Hotkeys - Customizable keyboard shortcuts
- 📋 **Phase 10**: PowerPaste Features - Advanced paste transformations
- 📋 **Phase 11**: Content Filters - Advanced application/content filtering

### Feature 002: ClipBar Quick Access (Planned - Requires Rework)
Global hotkey popup for instant paste access. Current implementation needs complete rework to match compact, Windows utility-style design with instant filtering and keyboard navigation.

### Feature 003: WPF UI Migration (Planned)
Migrate from DevExpress theming to WPF UI library for Fluent Design System, modern theming, and reduced maintenance burden.

## Implementation Status

### ✅ Completed
- Project structure with 4 main projects + 3 test projects
- Entity Framework Core 9.0 + SQLite database layer with custom schema migration
- All repository implementations (Clip, Collection, Folder, Template, SearchQuery, ApplicationFilter, SoundEvent)
- ClipboardService with Win32 clipboard monitoring
- Multi-format clipboard capture (Text, RTF, HTML, Image, FileList)
- SHA256-based duplicate detection
- MainWindow with three-pane layout (Tree, List, Preview)
- DevExpress DataGrid integration for clip list
- Text, Rich Text, HTML, Image, and File preview tabs
- System tray service (Windows Forms NotifyIcon)
- Application lifecycle (single instance, start with Windows, minimize to tray)
- **Options Dialog System** - Comprehensive preferences UI with 7 tabs (General, Pasting, QuickPaste, Editor, Capturing, App Profiles, Sounds)
- **Configuration Management** - Full IConfigurationService with persist to disk
- **QuickPaste Configuration UI** - Manage good/bad targets and formatting strings
- **PowerPaste Settings UI** - Delay, delimiter, trim, loop, explode configuration
- **Application Profiles** - Full CRUD operations for app-specific behavior
- **Sound System Infrastructure** - ISoundService, SoundsOptionsViewModel (sound files pending)
- Template system with TemplateService and TemplateEditorDialog
- Text Tools Dialog - Case conversion, encoding, line operations, trim, etc.
- Text transformation utilities (case conversion, encoding, etc.)
- Multiple ViewModels with MVVM pattern (MainWindowViewModel, ClipListViewModel, CollectionTreeViewModel, PreviewPaneViewModel, OptionsViewModel + 7 children)

### 🚧 In Progress
- **Phase 4 Completion**: Main menu implementation, toolbar implementation, context menus (ClipList & Collections)
- Search and filtering UI
- Collection/folder drag-drop operations
- Sound files for sound feedback system (infrastructure complete)
- Global hotkey configuration UI

### 📋 Planned
- Feature 002: ClipBar Quick Access (global hotkey popup - complete rework)
- Feature 003: WPF UI Migration (Fluent Design System)
- PowerPaste transformations and advanced paste features
- Advanced search with full-text indexing
- ClipMate Classic mode (compact toolbar interface)

### Known Issues

### Current Limitations
- **Main Menu Incomplete**: File, Edit, View, Tools, Help menus need implementation
- **Toolbar Incomplete**: Main toolbar buttons need implementation
- **Context Menus Incomplete**: ClipList and Collection tree context menus in progress
- **Search Not Implemented**: Full-text search UI pending
- **ClipBar Needs Rework**: Quick access popup requires complete redesign
- **Sound Files Missing**: Sound system infrastructure complete but audio files not added
- **No Cloud Sync**: Local storage only
- **Windows Only**: No cross-platform support (by design)

## Performance

### Benchmarks (Local Testing)
- **Clipboard Capture**: <10ms average response time
- **Duplicate Detection**: Hash calculation <5ms for typical text clips
- **Database Insert**: <50ms average via EF Core
- **UI Responsiveness**: Async operations prevent UI blocking
- **Memory Footprint**: ~80MB typical, ~150MB with large images loaded

### Optimization Techniques
- Async/await throughout for non-blocking operations
- Debouncing for rapid clipboard changes (50ms window)
- SHA256 hashing on background thread
- EF Core compiled queries for hot paths
- Image thumbnail generation cached in database

## Project History

**Genesis**: November 11, 2025 - Project initiated as modern recreation of ClipMate 7.5

**Architecture Evolution:**
- Initial design used LiteDB → Switched to EF Core 9.0 + SQLite (team expertise, ADR-001)
- Custom logging → Migrated to Microsoft.Extensions.Logging (ADR-004)
- Manual P/Invoke → Migrated to CsWin32 0.3.248 code generation (ADR-005)
- xUnit → Migrated to TUnit 0.4.31 for modern testing experience
- Custom MVVM → Adopted CommunityToolkit.Mvvm 8.4.0 (ADR-002)
- Custom DI → Adopted Microsoft.Extensions.DependencyInjection (ADR-003)
- Monolithic OptionsViewModel → Refactored to coordinator pattern with 7 child ViewModels (Dec 2025)

## Support

For issues, questions, or feature requests, please [open an issue](https://github.com/Tsabo/ClipMate/issues).

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

**Built with .NET 9, WPF, and DevExpress**  
*A modern recreation of the classic ClipMate clipboard manager*

Status: Active Development | Last Updated: December 4, 2025
