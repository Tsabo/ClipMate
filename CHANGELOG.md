# Changelog

All notable changes to ClipMate will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Main Toolbar Buttons** - Completed toolbar implementation for ClassicWindow and ExplorerWindow with all 16 documented buttons (PowerPaste, Delete, Print, Append, Remove Breaks, Move to Collection, Copy to Collection, Search, Switch View, View Clip, Outbound Filtering, Templates, Text Clean-up, Loop PowerPaste, Explode PowerPaste, Event Log)
- **CustomFontIconSource Integration** - Toolbar buttons now use custom font glyphs for Delete, Append, CopyToCollection, and MoveToCollection for consistent iconography
- **Dynamic Dropdown Menus** - Implemented GetItemData handlers for Collections and Templates dropdowns with cross-database support
- **Keyboard Shortcuts** - Added comprehensive InputBindings to both Explorer and Classic windows for menu shortcuts (Ctrl+F, Ctrl+E, Ctrl+N, Ctrl+R, Ctrl+P, Ctrl+O, Ctrl+U, Ctrl+D, Alt+Enter, Alt+R, Ctrl+Alt+A, Ctrl+Alt+B, Ctrl+Alt+T, Ctrl+Alt+P, Ctrl+Alt+N, F5-F8, F12)

### Fixed
- **SwitchView Command** - Fixed window switching to properly toggle between Classic and Explorer by closing current window and opening the other
- **Clipboard Lock Errors** - Implemented retry logic with exponential backoff (50ms, 100ms, 200ms) for CLIPBRD_E_CANT_OPEN errors using ThrottleDebounce.Retrier.Attempt
- **TrayIcon Menu State Updates** - Fixed Auto Capture and Filter Outbound Clips checkboxes in tray icon context menu not updating by adding PopupMenu.Opening event handler to refresh bindings
- **Cross-Database Clipboard Operations** - ClassicWindow now properly supports copying and moving clips between databases with ClipData and blob migration

### Changed
- **ClipboardService Dependencies** - Added ThrottleDebounce package reference for retry logic implementation
- **ClassicWindow Collections Integration** - Added ITemplateService dependency and collection dropdown handlers for full feature parity with ExplorerWindow
- **Toolbar Button Order** - Standardized button order across both windows to match main-toolbar-buttons.md specification

## [0.1.0-alpha.6] - 2026-1-5

### Added
- **Blob Data Export Support** - XML export now includes all clip content (TextContent, HtmlContent, ImageData as base64, FilePathsJson)
- **LoadBlobDataAsync Method** - New IClipService method to load blob data from database tables (BlobTxt, BlobPng, BlobJpg, BlobBlob) into clip transient properties
- **Export/Import Integration Tests** - Comprehensive test suite verifying clips loaded from database can be exported with all blob data intact and successfully re-imported
- **Pre-Release Update Check** - About dialog now includes dropdown option to check for pre-release versions from GitHub

### Fixed
- **XML Export Missing Content** - Fixed issue where XML export only included clip metadata without actual content (text, images, HTML, file paths), making re-import impossible
- **ClipExportDto Serialization** - Added ImageDataBase64 and FilePathsJson properties with proper base64 encoding for binary image data
- **Export Workflow** - XmlExportViewModel now calls LoadBlobDataAsync to populate blob data before export
- **Update Check Service GitHub URL** - Corrected repository URL from `clipmate/ClipMate` to `Tsabo/ClipMate` to fix 404 errors when checking for updates
- **About Dialog Button Bindings** - Fixed DataContext binding issues for DropDownButton in ThemedWindow.DialogButtons using RelativeSource
- **Update Check CommandParameter** - Fixed type mismatch by using proper sys:Boolean type instead of string "True" for CommandParameter
- **Version Comparison with Build Metadata** - Fixed ApplicationVersion.ParseSemanticVersion to properly strip build metadata (git hash after `+`) when comparing versions, allowing correct detection of newer releases

### Changed
- **Test Infrastructure** - Added InternalsVisibleTo for ClipMate.Tests.Integration to access internal repository classes for proper integration testing

## [0.1.0-alpha.5] - 2026-1-4

### Added
- **Clipboard Erase Detection** - Implemented detection for when external applications clear the clipboard with audio feedback
- **Database Maintenance Features**:
  - Automatic integrity checks on startup (PRAGMA integrity_check)
  - Automatic repair prompt and process for corrupted databases
  - Old backup file cleanup (14-day retention)
  - CleanupMethod configuration enforcement:
    - AtStartup: Runs cleanup during application startup
    - AtShutdown: Runs cleanup during application shutdown
    - AfterHourIdle: Runs cleanup during hourly idle maintenance
    - Never: Disables automatic cleanup
    - Manual: Cleanup only via user action
- **MaintenanceSchedulerService Enhancements**:
  - Now respects per-database CleanupMethod configuration
  - Automatic old backup cleanup during idle maintenance
  - Processes all configured databases independently
- **HotkeyBindDialog Redesign** - Complete UX overhaul with toggle modifier buttons (Shift, Ctrl, Win, Alt), special key dropdown menu with 18 media/browser/volume keys, and improved key capture workflow matching ClipboardFusion interface

### Fixed
- **Search Results Auto-Selection** - Search results node is now automatically selected after executing a search, with database node expanded for visibility
- **Search Results Refresh** - Fixed issue where search results wouldn't refresh when the search results node was already selected
- **Clipboard Erase Sound** - Removed incorrect sound playback when deleting clips from ClipMate database (sound now only plays when external apps clear clipboard)

### Changed
- **ClipService Dependencies** - Removed ISoundService dependency as clip deletion no longer triggers sound effects
- **App Startup** - Added comprehensive maintenance tasks including integrity checks, backup cleanup, and CleanupMethod.AtStartup processing
- **App Shutdown** - Added CleanupMethod.AtShutdown processing
- **DatabaseMaintenanceService** - Added CleanupOldBackupsAsync and CheckDatabaseIntegrityAsync methods
- **Dialog Buttons** - Migrated HotkeyBindDialog and TextToolsDialog to use DevExpress ThemedWindow.DialogButtons for consistent themed appearance
- **Special Key Selection** - Replaced ComboBoxEdit with DropDownButton + PopupMenu in HotkeyBindDialog for more native DevExpress implementation

## [0.1.0-alpha.4] - 2026-1-3

### Added
- **StateRefreshRequestedEvent Pattern** - Implemented centralized state synchronization for ViewModels with service-derived properties
- **Window Initialization State Refresh** - Explorer and Classic windows now send state refresh events after loading to ensure correct initial UI state
- **Update Check Service** - Implemented automatic update checking against GitHub releases with user notifications in About dialog
- **ApplicationVersion Value Object** - Added semantic versioning support with structured version representation

### Fixed
- **Menu State Indicators** - Auto Capture menu items now show checked/unchecked state across Explorer, Classic, and Tray windows
- **ClipViewer Toolbar State** - Toggle buttons (Tack, Word Wrap, Show Non-Printing) now display proper checked state using BarCheckItem bindings
- **Service State Synchronization** - ViewModels now receive notifications when underlying service state changes (clipboard monitoring, target lock, go-back state, database count)
- **Help Links** - Updated all help links to point to new documentation site (https://jeremy.browns.info/ClipMate)
- **DatabaseMaintenanceCoordinator** - Refactored to support multiple databases and show appropriate backup dialogs based on database count

### Changed
- **QuickPasteService** - Added IMessenger dependency to send state change notifications
- **DatabaseManager** - Added IMessenger dependency to notify when databases are loaded/unloaded
- **ClipboardCoordinator** - Sends StateRefreshRequestedEvent instead of AutoCaptureStateChangedEvent for broader state coverage
- **MainMenuViewModel** - Now refreshes IsAutoCapturing and HasMultipleDatabases properties on state change events
- **QuickPasteToolbarViewModel** - Now refreshes IsTargetLocked and GoBackEnabled properties on state change events
- **Clipboard Services** - Changed to transient lifetime for better resource management
- **Installer Copyright** - Updated to 2026

## [0.1.0-alpha.3] - 2026-1-3

### Added
- **Virtual Collection Deletion** - Users can now delete virtual collections through context menu

### Changed
- **Application Profile Format Handling** - Updated smart defaults to use "CF_" prefixed format names matching clipboard enumeration
- **Bitmap Format Detection** - All bitmap variants (CF_BITMAP, CF_DIB, CF_DIBV5) treated as equivalent when checking application profiles
- **Sample Data** - Updated Welcome clip to remove version number and signature, modernized text

### Fixed
- **Context Menu Commands** - Fixed XAML bindings for Add/Delete Collection and Activate/Deactivate Database commands using TreeView.Tag approach
- **Command Execution** - Implemented proper CanExecute logic for collection commands based on selected node type
- **Virtual Collection Organization** - Removed duplicate "Virtual" parent collection from seeder; virtual collections now top-level entities organized by UI
- **Cascade Delete** - Collection deletion now properly removes orphaned clips and child collections to prevent foreign key constraint errors
- **Application Profile Format Matching** - Fixed format name mismatches between clipboard enumeration (CF_BITMAP) and profile storage (BITMAP)
- **Bitmap Format Filtering** - Resolved issue where enabled bitmap formats were still filtered out due to name prefix differences
- **Clipboard Suppression** - Fixed ContentHash not being populated when loading clips from database, preventing re-capture of viewed clips
- **ClipViewer Display** - Fixed StorageType values in sample clips (changed from 0 to StorageType.Text) so ClipViewer can properly load content

## [0.1.0-alpha.2] - 2026-01-02

### Added
- **About Dialog** - Version information, credits, and third-party attribution
- **Docusaurus Documentation** - Initial documentation site setup with essential files

### Changed
- **QuickPaste Formatting** - Improved formatting string selection logic for better paste behavior
- **README Updates** - Added project acknowledgment and tribute to original ClipMate

### Fixed
- **About Dialog XAML** - Cleaned up formatting for better maintainability
- **Collection Roles** - Added CollectionRole.Inbox enum for semantic identification
- **Duplicate Role Validation** - Repository-level validation prevents duplicate special role assignments
- **Initialization Errors** - Fixed "No database is currently selected" when starting minimized
- **DatabaseKey Propagation** - ClipAddedEvent now includes DatabaseKey for proper context tracking

### Tests
- Enhanced service provider mocks in view model tests
- Added IClipboardService mock coverage

## [0.1.0-alpha.1] - 2026-01-01

### Added

#### Core Features
- **Clipboard Monitoring** - Real-time capture of text, images, RTF, HTML, and file paths
- **Multi-Database Support** - Switch between multiple SQLite databases with independent collections
- **Collections & Folders** - Hierarchical organization with Collections, Folders, and Virtual (SQL-based) collections
- **Search System** - Full-text search with saved queries and SQL support for advanced filtering
- **QuickPaste** - Auto-targeting paste functionality with formatting strings and Good/Bad target lists
- **PowerPaste** - Automation engine with macro execution and keystroke simulation
- **Templates** - Reusable content with tag replacement (#DATE#, #TIME#, etc.)
- **Shortcuts** - Nickname system for quick clip access (e.g., `.s.welcome`)
- **Import/Export** - XML format with flat file fallback for large content
- **Application Profiles** - Per-application capture filtering and settings
- **Retention Management** - Automatic overflow handling with clip cascade (Collection → Overflow → Trashcan)
- **Duplicate Detection** - Content hashing with configurable per-collection duplicate handling
- **ClipData Architecture** - Multi-format storage supporting simultaneous Text, RTF, HTML, and Bitmap formats

#### UI Components
- **Three-Pane Explorer** - Collections tree, clip list, and content viewer with resizable splitters
- **Monaco Editor** - Code/text editing with syntax highlighting and 40+ themes
- **Hex Editor** - Binary content viewing and editing for non-text formats (WpfHexEditor)
- **Clip Viewer Window** - Standalone viewer with auto-follow and pinning features
- **DevExpress Controls** - Modern themed UI with Office 2019 styles
- **Emoji Icons** - Twemoji font integration in context menus, toolbars, and status displays
- **Sound Effects** - Audio feedback for clipboard operations (capture, paste, ignore)
- **Clip Properties Dialog** - Detailed metadata editing and shortcut management

#### Diagnostic Tools
- **SQL Console** - Direct database querying with IntelliSense and result grid
- **Event Log Viewer** - Real-time application event monitoring
- **Clipboard Diagnostics** - Format inspection and raw clipboard data viewing
- **Database Schema Viewer** - Interactive table and relationship explorer
- **SQL Maintenance Services** - Database optimization and diagnostics

#### Data Management
- **Setup Wizard** - First-run configuration and database initialization
- **Default Data Seeder** - Pre-configured collections matching ClipMate 7.5 structure
- **Database Migration** - Automatic schema upgrades with backup creation
- **Maintenance Scheduler** - Background optimization and cleanup tasks
- **Undo Service** - Transaction-based undo for clip operations
- **Centralized Clip Operations** - Unified service layer for all clip manipulations

### Changed
- **Architecture** - Layered design with strict separation: UI → Services → Repositories → DbContext
- **Database Access** - Entity Framework Core 9.0 with Dapper for performance-critical queries
- **MVVM Pattern** - CommunityToolkit.Mvvm for relay commands and observable properties
- **Logging** - Structured logging with Serilog (file and debug output)
- **Configuration** - TOML format for user settings with type-safe models
- **Unit Tests** - Migrated to in-memory SQLite connections and IDatabaseContextFactory pattern
- **RTF Viewer** - Replaced with DevExpress RichEditControl for enhanced functionality
- **HTML Viewer** - Integrated HtmlRenderer.WPF for improved rendering

### Fixed
- **AcceptDuplicates Enforcement** - Collections properly reject duplicates based on configuration
- **TUnit Compatibility** - Updated to version 1.7.7 for improved testing
- **Property References** - Corrected ColumnBase to BaseColumn in DataGrid methods
- **Font Icons** - Updated SVG resources and font mappings
- **Test Assertions** - Improved backup progress and search functionality tests

### Infrastructure
- **Build System** - Cake build automation with version stamping and artifact management
- **CI/CD Pipeline** - GitHub Actions for automated builds and installer generation
- **Versioning** - MinVer for semantic versioning from Git tags
- **Installers** - Inno Setup with dual strategy (standard installer + portable ZIP)
- **WebView2** - Fixed Version runtime bundled for offline portable installations
- **Code Signing** - SignPath integration configured (pending certificate approval)
- **Testing** - TUnit framework with 100+ unit and integration tests
- **Font Pipeline** - nanoemoji and fonttools for color emoji font generation
- **LibMan Integration** - Automated client-side library management
- **.NET 10 Upgrade** - Migrated from .NET 9 to .NET 10 preview

### Known Limitations
- Encryption not yet implemented (planned for future release)
- Printing functionality pending
- Help documentation in progress
- Installers unsigned (awaiting SignPath certificate approval)

---

## Release Notes Format

Each release should include:

### Added
New features and capabilities

### Changed
Changes to existing functionality

### Fixed
Bug fixes

### Deprecated
Features that will be removed in upcoming releases

### Removed
Features that have been removed

### Security
Security-related changes

---

## Version History

| Version | Date | Type | Notes |
|---------|------|------|-------|
| 0.1.0 | TBD | Pre-alpha | Initial development release |

---

**Note:** This project is in active development. Breaking changes may occur between pre-release versions.
