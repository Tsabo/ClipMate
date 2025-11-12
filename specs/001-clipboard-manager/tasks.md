# Tasks: ClipMate Clipboard Manager

**Input**: Design documents from `/specs/001-clipboard-manager/`  
**Prerequisites**: plan.md (✓), spec.md (✓), research.md (✓), data-model.md (✓), contracts/ (✓)

**Tests**: Unit tests are MANDATORY per ClipMate Constitution (90%+ coverage required). Integration tests for Windows API interactions are also required.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

ClipMate uses a multi-project structure:
- `src/ClipMate.App/` - WPF UI project
- `src/ClipMate.Core/` - Business logic and domain models
- `src/ClipMate.Data/` - LiteDB data access layer
- `src/ClipMate.Platform/` - Windows-specific Win32 interop
- `tests/ClipMate.Tests.Unit/` - Unit tests
- `tests/ClipMate.Tests.Integration/` - Integration tests

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [ ] T001 Create .NET 10 solution file at src/ClipMate.sln
- [ ] T002 [P] Create ClipMate.App WPF project in src/ClipMate.App/ClipMate.App.csproj
- [ ] T003 [P] Create ClipMate.Core class library in src/ClipMate.Core/ClipMate.Core.csproj
- [ ] T004 [P] Create ClipMate.Data class library in src/ClipMate.Data/ClipMate.Data.csproj
- [ ] T005 [P] Create ClipMate.Platform class library in src/ClipMate.Platform/ClipMate.Platform.csproj
- [ ] T006 [P] Create ClipMate.Tests.Unit xUnit project in tests/ClipMate.Tests.Unit/ClipMate.Tests.Unit.csproj
- [ ] T007 [P] Create ClipMate.Tests.Integration xUnit project in tests/ClipMate.Tests.Integration/ClipMate.Tests.Integration.csproj
- [ ] T008 Configure project references (App→Core/Data/Platform, Data→Core, Platform→Core, Tests→All)
- [ ] T009 [P] Add NuGet package LiteDB 5.0+ to ClipMate.Data project
- [ ] T010 [P] Add NuGet package CommunityToolkit.Mvvm 8.2+ to ClipMate.Core and ClipMate.App projects
- [ ] T011 [P] Add NuGet package NAudio 2.2+ to ClipMate.Core project
- [ ] T012 [P] Add NuGet package Microsoft.Extensions.DependencyInjection 8.0+ to ClipMate.App project
- [ ] T013 [P] Add NuGet packages Moq, FluentAssertions to test projects
- [ ] T014 [P] Create .editorconfig with C# 12 nullable reference types enabled at repository root
- [ ] T015 [P] Create README.md with setup instructions at repository root
- [ ] T016 [P] Create .gitignore for .NET projects at repository root

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Core Models & Enums

- [ ] T017 [P] Create ClipContentType enum (Text, Image, FileList, RichText) in src/ClipMate.Core/Models/ClipContentType.cs
- [ ] T018 [P] Create RetentionPolicy enum in src/ClipMate.Core/Models/RetentionPolicy.cs
- [ ] T019 [P] Create SearchScope enum in src/ClipMate.Core/Models/SearchScope.cs
- [ ] T020 [P] Create FilterType enum in src/ClipMate.Core/Models/FilterType.cs
- [ ] T021 [P] Create Clip entity model in src/ClipMate.Core/Models/Clip.cs
- [ ] T022 [P] Create Collection entity model in src/ClipMate.Core/Models/Collection.cs
- [ ] T023 [P] Create Folder entity model in src/ClipMate.Core/Models/Folder.cs
- [ ] T024 [P] Create Template entity model in src/ClipMate.Core/Models/Template.cs
- [ ] T025 [P] Create SearchQuery entity model in src/ClipMate.Core/Models/SearchQuery.cs
- [ ] T026 [P] Create ApplicationFilter entity model in src/ClipMate.Core/Models/ApplicationFilter.cs
- [ ] T027 [P] Create SoundEvent entity model in src/ClipMate.Core/Models/SoundEvent.cs

### Repository Interfaces

- [ ] T028 [P] Create IClipRepository interface in src/ClipMate.Core/Repositories/IClipRepository.cs
- [ ] T029 [P] Create ICollectionRepository interface in src/ClipMate.Core/Repositories/ICollectionRepository.cs
- [ ] T030 [P] Create IFolderRepository interface in src/ClipMate.Core/Repositories/IFolderRepository.cs
- [ ] T031 [P] Create ITemplateRepository interface in src/ClipMate.Core/Repositories/ITemplateRepository.cs
- [ ] T032 [P] Create ISearchQueryRepository interface in src/ClipMate.Core/Repositories/ISearchQueryRepository.cs
- [ ] T033 [P] Create IApplicationFilterRepository interface in src/ClipMate.Core/Repositories/IApplicationFilterRepository.cs
- [ ] T034 [P] Create ISoundEventRepository interface in src/ClipMate.Core/Repositories/ISoundEventRepository.cs

### LiteDB Implementation

- [ ] T035 Create LiteDbContext class with connection management in src/ClipMate.Data/LiteDB/LiteDbContext.cs
- [ ] T036 [P] Implement ClipRepository with all 7 indexes in src/ClipMate.Data/LiteDB/ClipRepository.cs
- [ ] T037 [P] Implement CollectionRepository with indexes in src/ClipMate.Data/LiteDB/CollectionRepository.cs
- [ ] T038 [P] Implement FolderRepository with indexes in src/ClipMate.Data/LiteDB/FolderRepository.cs
- [ ] T039 [P] Implement TemplateRepository with indexes in src/ClipMate.Data/LiteDB/TemplateRepository.cs
- [ ] T040 [P] Implement SearchQueryRepository in src/ClipMate.Data/LiteDB/SearchQueryRepository.cs
- [ ] T041 [P] Implement ApplicationFilterRepository in src/ClipMate.Data/LiteDB/ApplicationFilterRepository.cs
- [ ] T042 [P] Implement SoundEventRepository in src/ClipMate.Data/LiteDB/SoundEventRepository.cs
- [ ] T043 Add database schema migration system in src/ClipMate.Data/LiteDB/SchemaManager.cs
- [ ] T044 Add database backup and restore functionality in src/ClipMate.Data/LiteDB/BackupService.cs

### Service Interfaces

- [ ] T045 [P] Create IClipboardService interface in src/ClipMate.Core/Services/IClipboardService.cs
- [ ] T046 [P] Create ISearchService interface in src/ClipMate.Core/Services/ISearchService.cs
- [ ] T047 [P] Create ICollectionService interface in src/ClipMate.Core/Services/ICollectionService.cs
- [ ] T048 [P] Create IFolderService interface in src/ClipMate.Core/Services/IFolderService.cs
- [ ] T049 [P] Create IClipService interface in src/ClipMate.Core/Services/IClipService.cs
- [ ] T050 [P] Create IHotkeyService interface in src/ClipMate.Core/Services/IHotkeyService.cs
- [ ] T051 [P] Create ITemplateService interface in src/ClipMate.Core/Services/ITemplateService.cs
- [ ] T052 [P] Create ISoundService interface in src/ClipMate.Core/Services/ISoundService.cs
- [ ] T053 [P] Create ISettingsService interface in src/ClipMate.Core/Services/ISettingsService.cs
- [ ] T054 [P] Create IApplicationFilterService interface in src/ClipMate.Core/Services/IApplicationFilterService.cs

### Win32 Platform Layer

- [ ] T055 [P] Create Win32Constants class with clipboard format constants in src/ClipMate.Platform/Win32/Win32Constants.cs
- [ ] T056 [P] Create Win32Methods class with P/Invoke declarations in src/ClipMate.Platform/Win32/Win32Methods.cs
- [ ] T057 [P] Create ClipboardMonitor Win32 wrapper in src/ClipMate.Platform/ClipboardMonitor.cs
- [ ] T058 [P] Create HotkeyManager Win32 wrapper in src/ClipMate.Platform/HotkeyManager.cs
- [ ] T059 [P] Create DpiHelper for DPI awareness in src/ClipMate.Platform/DpiHelper.cs

### MVVM Infrastructure

- [ ] T060 Create ViewModelBase class with INotifyPropertyChanged in src/ClipMate.Core/ViewModels/ViewModelBase.cs
- [ ] T061 [P] Create RelayCommand implementation in src/ClipMate.Core/Commands/RelayCommand.cs
- [ ] T062 [P] Create AsyncRelayCommand implementation in src/ClipMate.Core/Commands/AsyncRelayCommand.cs
- [ ] T063 [P] Create EventAggregator for loose coupling in src/ClipMate.Core/Events/EventAggregator.cs

### Dependency Injection Setup

- [ ] T064 Create ServiceCollectionExtensions for Core services in src/ClipMate.Core/DependencyInjection/ServiceCollectionExtensions.cs
- [ ] T065 Create ServiceCollectionExtensions for Data repositories in src/ClipMate.Data/DependencyInjection/ServiceCollectionExtensions.cs
- [ ] T066 Create App.xaml.cs with DI container configuration in src/ClipMate.App/App.xaml.cs
- [ ] T067 Configure service lifetimes (singleton for services, scoped for repositories) in App.xaml.cs

### Testing Infrastructure

- [ ] T068 [P] Create TestFixtureBase class with common setup in tests/ClipMate.Tests.Unit/TestFixtureBase.cs
- [ ] T069 [P] Create MockRepositoryFactory for test data in tests/ClipMate.Tests.Unit/Mocks/MockRepositoryFactory.cs
- [ ] T070 [P] Create TestDataBuilder for entity creation in tests/ClipMate.Tests.Unit/Builders/TestDataBuilder.cs
- [ ] T071 [P] Create IntegrationTestBase with real database in tests/ClipMate.Tests.Integration/IntegrationTestBase.cs

### Error Handling & Logging

- [ ] T072 [P] Create AppException base exception class in src/ClipMate.Core/Exceptions/AppException.cs
- [ ] T073 [P] Create ClipboardException for clipboard errors in src/ClipMate.Core/Exceptions/ClipboardException.cs
- [ ] T074 [P] Create DatabaseException for database errors in src/ClipMate.Core/Exceptions/DatabaseException.cs
- [ ] T075 Create ILogger interface and FileLogger implementation in src/ClipMate.Core/Logging/ILogger.cs
- [ ] T076 Add global exception handler in App.xaml.cs

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Automatic Clipboard Capture and History (Priority: P1) 🎯 MVP

**Goal**: Automatically capture everything copied to clipboard and store persistently. Core value proposition.

**Independent Test**: Copy various content types (text, images, files) and verify they appear in history list, persist after restart.

### Tests for User Story 1 (MANDATORY per Constitution) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (TDD required)**

- [ ] T077 [P] [US1] Unit test for ClipboardService.StartMonitoringAsync in tests/ClipMate.Tests.Unit/Services/ClipboardServiceTests.cs
- [ ] T078 [P] [US1] Unit test for ClipboardService text capture in tests/ClipMate.Tests.Unit/Services/ClipboardServiceTests.cs
- [ ] T079 [P] [US1] Unit test for ClipboardService image capture in tests/ClipMate.Tests.Unit/Services/ClipboardServiceTests.cs
- [ ] T080 [P] [US1] Unit test for ClipboardService duplicate detection via ContentHash in tests/ClipMate.Tests.Unit/Services/ClipboardServiceTests.cs
- [ ] T081 [P] [US1] Unit test for ClipService.GetRecentAsync with timestamp ordering in tests/ClipMate.Tests.Unit/Services/ClipServiceTests.cs
- [ ] T082 [P] [US1] Integration test for clipboard monitoring lifecycle in tests/ClipMate.Tests.Integration/ClipboardMonitoringTests.cs
- [ ] T083 [P] [US1] Integration test for clip persistence after app restart in tests/ClipMate.Tests.Integration/ClipPersistenceTests.cs
- [ ] T084 [P] [US1] Integration test for application filter exclusion in tests/ClipMate.Tests.Integration/ApplicationFilterTests.cs

### Implementation for User Story 1

- [ ] T085 [US1] Implement ClipboardService with Win32 RegisterClipboardFormatListener in src/ClipMate.Core/Services/ClipboardService.cs (depends on T057, T045)
- [ ] T086 [US1] Add clipboard format detection and prioritization logic to ClipboardService
- [ ] T087 [US1] Add text capture with multiple format support (plain, RTF, HTML) to ClipboardService
- [ ] T088 [US1] Add image capture with bitmap handling to ClipboardService
- [ ] T089 [US1] Add file list capture from Windows Explorer to ClipboardService
- [ ] T090 [US1] Add ContentHash calculation using SHA256 for duplicate detection to ClipboardService
- [ ] T091 [US1] Add thread-safe clipboard data extraction with retry logic to ClipboardService
- [ ] T092 [US1] Add ClipCaptured event with ClipCapturedEventArgs to ClipboardService
- [ ] T093 [US1] Implement ClipService with clip CRUD operations in src/ClipMate.Core/Services/ClipService.cs (depends on T049, T028)
- [ ] T094 [US1] Add GetRecentAsync method with timestamp DESC ordering to ClipService
- [ ] T095 [US1] Add duplicate detection check before saving in ClipService
- [ ] T096 [US1] Implement ApplicationFilterService with exclusion rules in src/ClipMate.Core/Services/ApplicationFilterService.cs (depends on T054, T033)
- [ ] T097 [US1] Add filter matching logic (ProcessName, WindowTitle) to ApplicationFilterService
- [ ] T098 [US1] Wire ClipboardService.ClipCaptured event to ClipService.CreateAsync in App.xaml.cs
- [ ] T099 [US1] Add application filter check before saving clip in ClipboardService event handler
- [ ] T100 [US1] Add error handling and logging for clipboard capture failures
- [ ] T101 [US1] Add background thread processing to avoid UI blocking in ClipboardService
- [ ] T102 [US1] Add debouncing (50ms window) for rapid clipboard changes in ClipboardService
- [ ] T103 [US1] Run all US1 unit tests and verify 90%+ coverage for ClipboardService and ClipService
- [ ] T104 [US1] Run all US1 integration tests and verify clipboard monitoring works end-to-end

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently

---

## Phase 4: User Story 2 - Three-Pane Interface Organization (Priority: P1)

**Goal**: Visual interface with tree navigation, list views, and content preview for efficient browsing.

**Independent Test**: Open app, navigate collections/folders in tree, select items in list, view content in preview pane.

### Tests for User Story 2 (MANDATORY per Constitution) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (TDD required)**

- [ ] T105 [P] [US2] Unit test for MainWindowViewModel initialization in tests/ClipMate.Tests.Unit/ViewModels/MainWindowViewModelTests.cs
- [ ] T106 [P] [US2] Unit test for CollectionTreeViewModel.LoadCollectionsAsync in tests/ClipMate.Tests.Unit/ViewModels/CollectionTreeViewModelTests.cs
- [ ] T107 [P] [US2] Unit test for ClipListViewModel.LoadClipsAsync with collection filter in tests/ClipMate.Tests.Unit/ViewModels/ClipListViewModelTests.cs
- [ ] T108 [P] [US2] Unit test for ClipListViewModel view mode switching in tests/ClipMate.Tests.Unit/ViewModels/ClipListViewModelTests.cs
- [ ] T109 [P] [US2] Unit test for PreviewPaneViewModel content type selection in tests/ClipMate.Tests.Unit/ViewModels/PreviewPaneViewModelTests.cs
- [ ] T110 [P] [US2] Integration test for main window initialization and layout in tests/ClipMate.Tests.Integration/UI/MainWindowTests.cs
- [ ] T111 [P] [US2] UI automation test for collection selection in tests/ClipMate.Tests.Integration/UI/CollectionNavigationTests.cs

### Implementation for User Story 2

- [ ] T112 [P] [US2] Create MainWindow.xaml with three-pane grid layout in src/ClipMate.App/Views/MainWindow.xaml
- [ ] T113 [P] [US2] Add GridSplitter controls with position persistence in MainWindow.xaml
- [ ] T114 [P] [US2] Create MenuBar with File, Edit, View, Tools, Help menus in MainWindow.xaml
- [ ] T115 [P] [US2] Create ToolBar with icon buttons in MainWindow.xaml
- [ ] T116 [P] [US2] Create StatusBar with dynamic content in MainWindow.xaml
- [ ] T117 [US2] Create MainWindowViewModel in src/ClipMate.App/ViewModels/MainWindowViewModel.cs (depends on T060)
- [ ] T118 [US2] Add window state persistence (size, position, splitter) to MainWindowViewModel
- [ ] T119 [US2] Create CollectionTreeView.xaml with HierarchicalDataTemplate in src/ClipMate.App/Views/Controls/CollectionTreeView.xaml
- [ ] T120 [US2] Create CollectionTreeViewModel in src/ClipMate.App/ViewModels/CollectionTreeViewModel.cs (depends on T060, T047)
- [ ] T121 [US2] Add SelectedCollectionChanged event to CollectionTreeViewModel
- [ ] T122 [US2] Implement CollectionService with CRUD operations in src/ClipMate.Core/Services/CollectionService.cs (depends on T047, T029)
- [ ] T123 [US2] Implement FolderService with hierarchy management in src/ClipMate.Core/Services/FolderService.cs (depends on T048, T030)
- [ ] T124 [US2] Create ClipListView.xaml with VirtualizingStackPanel in src/ClipMate.App/Views/Controls/ClipListView.xaml
- [ ] T125 [US2] Add multiple view modes (List, Details, Icons) to ClipListView.xaml using DataTemplate selectors
- [ ] T126 [US2] Create ClipListViewModel in src/ClipMate.App/ViewModels/ClipListViewModel.cs (depends on T060, T049)
- [ ] T127 [US2] Add LoadClipsAsync with collection/folder filtering to ClipListViewModel
- [ ] T128 [US2] Add view mode switching command to ClipListViewModel
- [ ] T129 [US2] Add clip selection with multi-select support to ClipListViewModel
- [ ] T130 [US2] Add right-click context menu to ClipListView.xaml
- [ ] T131 [US2] Create PreviewPane.xaml with ContentPresenter in src/ClipMate.App/Views/Controls/PreviewPane.xaml
- [ ] T132 [US2] Create PreviewPaneViewModel in src/ClipMate.App/ViewModels/PreviewPaneViewModel.cs (depends on T060)
- [ ] T133 [US2] Add DataTemplate for text preview in PreviewPane.xaml
- [ ] T134 [US2] Add DataTemplate for image preview in PreviewPane.xaml
- [ ] T135 [US2] Add DataTemplate for file list preview in PreviewPane.xaml
- [ ] T136 [US2] Wire CollectionTreeViewModel selection to ClipListViewModel refresh
- [ ] T137 [US2] Wire ClipListViewModel selection to PreviewPaneViewModel content update
- [ ] T138 [US2] Add high DPI support using DpiHelper in MainWindow.xaml.cs (depends on T059)
- [ ] T139 [US2] Add WPF theme integration with SystemParameters in App.xaml
- [ ] T140 [US2] Run all US2 unit tests and verify 90%+ coverage
- [ ] T141 [US2] Run all US2 integration tests and UI automation tests

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently

---

## Phase 5: User Story 3 - PowerPaste Quick Access (Priority: P2)

**Goal**: Global hotkey opens popup menu showing recent clipboard items for instant pasting from any app.

**Independent Test**: Press hotkey in any app, navigate with keyboard, paste selected item.

### Tests for User Story 3 (MANDATORY per Constitution) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (TDD required)**

- [ ] T142 [P] [US3] Unit test for HotkeyService.RegisterAsync with conflict detection in tests/ClipMate.Tests.Unit/Services/HotkeyServiceTests.cs
- [ ] T143 [P] [US3] Unit test for PowerPasteViewModel initialization in tests/ClipMate.Tests.Unit/ViewModels/PowerPasteViewModelTests.cs
- [ ] T144 [P] [US3] Unit test for PowerPasteViewModel.FilterItems search-as-you-type in tests/ClipMate.Tests.Unit/ViewModels/PowerPasteViewModelTests.cs
- [ ] T145 [P] [US3] Integration test for global hotkey registration in tests/ClipMate.Tests.Integration/HotkeyIntegrationTests.cs
- [ ] T146 [P] [US3] Integration test for PowerPaste window activation in tests/ClipMate.Tests.Integration/UI/PowerPasteTests.cs

### Implementation for User Story 3

- [ ] T147 [US3] Implement HotkeyService with Win32 RegisterHotKey in src/ClipMate.Core/Services/HotkeyService.cs (depends on T050, T058)
- [ ] T148 [US3] Add hotkey conflict detection to HotkeyService
- [ ] T149 [US3] Add HotkeyPressed event to HotkeyService
- [ ] T150 [US3] Add ComponentDispatcher.ThreadFilterMessage integration in HotkeyService
- [ ] T151 [P] [US3] Create PowerPasteWindow.xaml borderless popup window in src/ClipMate.App/Views/PowerPasteWindow.xaml
- [ ] T152 [P] [US3] Add window positioning logic (near cursor) in PowerPasteWindow.xaml.cs
- [ ] T153 [P] [US3] Add ListBox with keyboard navigation in PowerPasteWindow.xaml
- [ ] T154 [US3] Create PowerPasteViewModel in src/ClipMate.App/ViewModels/PowerPasteViewModel.cs (depends on T060, T049)
- [ ] T155 [US3] Add LoadRecentItemsAsync (configurable count) to PowerPasteViewModel
- [ ] T156 [US3] Add FilterItems instant search with <50ms response to PowerPasteViewModel
- [ ] T157 [US3] Add PasteSelectedCommand with clipboard setting to PowerPasteViewModel
- [ ] T158 [US3] Add CancelCommand to close window to PowerPasteViewModel
- [ ] T159 [US3] Wire HotkeyService.HotkeyPressed to PowerPasteWindow.Show()
- [ ] T160 [US3] Add keyboard navigation (arrow keys, Enter, Escape) to PowerPasteWindow
- [ ] T161 [US3] Add hover preview tooltip in PowerPasteWindow.xaml
- [ ] T162 [US3] Add proper focus management and window deactivation in PowerPasteWindow
- [ ] T163 [US3] Add PowerPaste settings (hotkey, item count) to SettingsService
- [ ] T164 [US3] Run all US3 unit tests and verify 90%+ coverage
- [ ] T165 [US3] Run all US3 integration tests

**Checkpoint**: User Stories 1, 2, AND 3 should all work independently

---

## Phase 6: User Story 4 - Collections and Folder Organization (Priority: P2)

**Goal**: Create multiple collections and organize clips into folders by project/context.

**Independent Test**: Create collections, create folders, drag clips between folders/collections.

### Tests for User Story 4 (MANDATORY per Constitution) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (TDD required)**

- [ ] T166 [P] [US4] Unit test for CollectionService.CreateAsync with validation in tests/ClipMate.Tests.Unit/Services/CollectionServiceTests.cs
- [ ] T167 [P] [US4] Unit test for FolderService.CreateAsync with hierarchy in tests/ClipMate.Tests.Unit/Services/FolderServiceTests.cs
- [ ] T168 [P] [US4] Unit test for ClipService.MoveToFolderAsync in tests/ClipMate.Tests.Unit/Services/ClipServiceTests.cs
- [ ] T169 [P] [US4] Integration test for collection switching in tests/ClipMate.Tests.Integration/CollectionManagementTests.cs
- [ ] T170 [P] [US4] UI automation test for drag-drop between folders in tests/ClipMate.Tests.Integration/UI/DragDropTests.cs

### Implementation for User Story 4

- [ ] T171 [US4] Add CreateAsync, UpdateAsync, DeleteAsync to CollectionService (depends on T122)
- [ ] T172 [US4] Add GetAllAsync, GetByIdAsync to CollectionService
- [ ] T173 [US4] Add retention policy enforcement to CollectionService
- [ ] T174 [US4] Add CreateAsync, UpdateAsync, DeleteAsync to FolderService (depends on T123)
- [ ] T175 [US4] Add GetByCollectionAsync with hierarchy to FolderService
- [ ] T176 [US4] Add circular reference validation to FolderService
- [ ] T177 [US4] Add MoveToFolderAsync to ClipService (depends on T093)
- [ ] T178 [US4] Add CopyToCollectionAsync to ClipService
- [ ] T179 [P] [US4] Create NewCollectionDialog.xaml in src/ClipMate.App/Views/Dialogs/NewCollectionDialog.xaml
- [ ] T180 [P] [US4] Create NewFolderDialog.xaml in src/ClipMate.App/Views/Dialogs/NewFolderDialog.xaml
- [ ] T181 [US4] Add CreateCollectionCommand to MainWindowViewModel (depends on T117)
- [ ] T182 [US4] Add CreateFolderCommand to CollectionTreeViewModel (depends on T120)
- [ ] T183 [US4] Add DeleteCollectionCommand to CollectionTreeViewModel
- [ ] T184 [US4] Add DeleteFolderCommand to CollectionTreeViewModel
- [ ] T185 [US4] Add RenameCollectionCommand to CollectionTreeViewModel
- [ ] T186 [US4] Add RenameFolderCommand to CollectionTreeViewModel
- [ ] T187 [US4] Add color/icon assignment UI to collection/folder dialogs
- [ ] T188 [US4] Implement drag-drop behavior for clips between folders in src/ClipMate.App/Behaviors/ClipDragDropBehavior.cs
- [ ] T189 [US4] Add visual feedback during drag operation to ClipListView.xaml
- [ ] T190 [US4] Add auto-categorization rules to FolderService
- [ ] T191 [US4] Add auto-categorize on capture to ClipboardService event handler
- [ ] T192 [US4] Add collection switcher to system tray menu in App.xaml.cs
- [ ] T193 [US4] Add collection switch hotkey support to HotkeyService
- [ ] T194 [US4] Run all US4 unit tests and verify 90%+ coverage
- [ ] T195 [US4] Run all US4 integration tests and UI automation tests

**Checkpoint**: User Stories 1-4 should all work independently

---

## Phase 7: User Story 5 - Search and Discovery (Priority: P2)

**Goal**: Powerful search to quickly find clips by content, date, or type even with thousands of items.

**Independent Test**: Enter search terms, verify instant results (<50ms), test filters (type, date, scope).

### Tests for User Story 5 (MANDATORY per Constitution) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (TDD required)**

- [ ] T196 [P] [US5] Unit test for SearchService.SearchAsync with full-text indexing in tests/ClipMate.Tests.Unit/Services/SearchServiceTests.cs
- [ ] T197 [P] [US5] Unit test for SearchService.SearchAsync with <50ms performance requirement in tests/ClipMate.Tests.Unit/Services/SearchServiceTests.cs
- [ ] T198 [P] [US5] Unit test for SearchService regex pattern validation in tests/ClipMate.Tests.Unit/Services/SearchServiceTests.cs
- [ ] T199 [P] [US5] Unit test for SavedSearchService.SaveAsync in tests/ClipMate.Tests.Unit/Services/SavedSearchServiceTests.cs
- [ ] T200 [P] [US5] Integration test for search with 100k+ clips dataset in tests/ClipMate.Tests.Integration/SearchPerformanceTests.cs
- [ ] T201 [P] [US5] Integration test for search result highlighting in tests/ClipMate.Tests.Integration/UI/SearchTests.cs

### Implementation for User Story 5

- [ ] T202 [US5] Implement SearchService with LiteDB full-text indexing in src/ClipMate.Core/Services/SearchService.cs (depends on T046, T028)
- [ ] T203 [US5] Add SearchAsync with instant results as user types to SearchService
- [ ] T204 [US5] Add search scope filtering (current/all collections, folder) to SearchService
- [ ] T205 [US5] Add content type filtering (text/images/files) to SearchService
- [ ] T206 [US5] Add date range filtering to SearchService
- [ ] T207 [US5] Add regex pattern matching with validation to SearchService
- [ ] T208 [US5] Add search result caching for performance to SearchService
- [ ] T209 [US5] Add result highlighting logic to SearchService
- [ ] T210 [US5] Create SavedSearchService for managing saved queries in src/ClipMate.Core/Services/SavedSearchService.cs
- [ ] T211 [P] [US5] Create SearchPanel.xaml with search box and filters in src/ClipMate.App/Views/Controls/SearchPanel.xaml
- [ ] T212 [US5] Create SearchViewModel in src/ClipMate.App/ViewModels/SearchViewModel.cs (depends on T060)
- [ ] T213 [US5] Add SearchCommand with debounced search-as-you-type to SearchViewModel
- [ ] T214 [US5] Add scope selection (ComboBox) to SearchPanel.xaml
- [ ] T215 [US5] Add content type filter checkboxes to SearchPanel.xaml
- [ ] T216 [US5] Add date range picker to SearchPanel.xaml
- [ ] T217 [US5] Add regex mode toggle to SearchPanel.xaml
- [ ] T218 [US5] Add SaveSearchCommand to SearchViewModel
- [ ] T219 [US5] Add saved searches dropdown to SearchPanel.xaml
- [ ] T220 [US5] Wire SearchViewModel results to ClipListViewModel display
- [ ] T221 [US5] Add search result highlighting to PreviewPane content templates
- [ ] T222 [US5] Add performance monitoring to verify <50ms search time
- [ ] T223 [US5] Run all US5 unit tests and verify 90%+ coverage
- [ ] T224 [US5] Run all US5 integration tests including performance tests with 100k clips

**Checkpoint**: User Stories 1-5 should all work independently

---

## Phase 8: User Story 6 - Text Processing Tools (Priority: P3)

**Goal**: Built-in text manipulation tools for quick editing without external editors.

**Independent Test**: Select text clip, apply transformations (case conversion, line operations, find/replace).

### Tests for User Story 6 (MANDATORY per Constitution) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (TDD required)**

- [ ] T225 [P] [US6] Unit test for TextTransformService.ConvertCase in tests/ClipMate.Tests.Unit/Services/TextTransformServiceTests.cs
- [ ] T226 [P] [US6] Unit test for TextTransformService.SortLines in tests/ClipMate.Tests.Unit/Services/TextTransformServiceTests.cs
- [ ] T227 [P] [US6] Unit test for TextTransformService.RemoveDuplicateLines in tests/ClipMate.Tests.Unit/Services/TextTransformServiceTests.cs
- [ ] T228 [P] [US6] Unit test for TextTransformService.FindAndReplace with regex in tests/ClipMate.Tests.Unit/Services/TextTransformServiceTests.cs
- [ ] T229 [P] [US6] Integration test for text tool application to clip in tests/ClipMate.Tests.Integration/TextTransformTests.cs

### Implementation for User Story 6

- [ ] T230 [P] [US6] Create TextTransformService in src/ClipMate.Core/Services/TextTransformService.cs
- [ ] T231 [P] [US6] Add ConvertCase method (uppercase/lowercase/titlecase/sentencecase) to TextTransformService
- [ ] T232 [P] [US6] Add SortLines method (alphabetically/numerically/reverse) to TextTransformService
- [ ] T233 [P] [US6] Add RemoveDuplicateLines method to TextTransformService
- [ ] T234 [P] [US6] Add AddLineNumbers method to TextTransformService
- [ ] T235 [P] [US6] Add FindAndReplace method with literal/regex modes to TextTransformService
- [ ] T236 [P] [US6] Add CleanUpText method (spaces/line breaks/trim) to TextTransformService
- [ ] T237 [P] [US6] Add ConvertFormat method (plain/RTF/HTML) to TextTransformService
- [ ] T238 [P] [US6] Create TextToolsDialog.xaml with tool selection in src/ClipMate.App/Views/Dialogs/TextToolsDialog.xaml
- [ ] T239 [US6] Create TextToolsViewModel in src/ClipMate.App/ViewModels/TextToolsViewModel.cs (depends on T060)
- [ ] T240 [US6] Add ApplyTransformCommand to TextToolsViewModel
- [ ] T241 [US6] Add tool preview before applying to TextToolsViewModel
- [ ] T242 [US6] Add custom macro creation UI to TextToolsDialog.xaml
- [ ] T243 [US6] Add macro save/load functionality to TextTransformService
- [ ] T244 [US6] Add Text Tools menu item to MainWindow menu bar
- [ ] T245 [US6] Wire text tool commands to clip content update in ClipService
- [ ] T246 [US6] Run all US6 unit tests and verify 90%+ coverage
- [ ] T247 [US6] Run all US6 integration tests

**Checkpoint**: User Stories 1-6 should all work independently

---

## Phase 9: User Story 7 - Template and Macro System (Priority: P3)

**Goal**: Create reusable text templates with variables for automating repetitive text entry.

**Independent Test**: Create template with variables, insert via hotkey/menu, verify variable substitution.

### Tests for User Story 7 (MANDATORY per Constitution) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (TDD required)**

- [ ] T248 [P] [US7] Unit test for TemplateService.ExpandVariablesAsync in tests/ClipMate.Tests.Unit/Services/TemplateServiceTests.cs
- [ ] T249 [P] [US7] Unit test for TemplateService date/time formatting in tests/ClipMate.Tests.Unit/Services/TemplateServiceTests.cs
- [ ] T250 [P] [US7] Unit test for TemplateService prompt variable handling in tests/ClipMate.Tests.Unit/Services/TemplateServiceTests.cs
- [ ] T251 [P] [US7] Integration test for template insertion workflow in tests/ClipMate.Tests.Integration/TemplateTests.cs

### Implementation for User Story 7

- [ ] T252 [US7] Implement TemplateService with variable expansion in src/ClipMate.Core/Services/TemplateService.cs (depends on T051, T031)
- [ ] T253 [US7] Add ExpandVariablesAsync with {DATE}, {TIME}, {USERNAME}, {COMPUTERNAME} to TemplateService
- [ ] T254 [US7] Add date/time format string support ({DATE:yyyy-MM-dd}) to TemplateService
- [ ] T255 [US7] Add {PROMPT:Label} interactive input support to TemplateService
- [ ] T256 [US7] Add variable validation in TemplateService
- [ ] T257 [P] [US7] Create TemplateEditorDialog.xaml in src/ClipMate.App/Views/Dialogs/TemplateEditorDialog.xaml
- [ ] T258 [US7] Create TemplateEditorViewModel in src/ClipMate.App/ViewModels/TemplateEditorViewModel.cs (depends on T060)
- [ ] T259 [US7] Add syntax highlighting for variables in TemplateEditorDialog.xaml
- [ ] T260 [US7] Add CreateTemplateCommand to TemplateEditorViewModel
- [ ] T261 [US7] Add UpdateTemplateCommand to TemplateEditorViewModel
- [ ] T262 [US7] Add DeleteTemplateCommand to TemplateEditorViewModel
- [ ] T263 [US7] Add template categorization to TemplateService
- [ ] T264 [US7] Add category tree view to TemplateEditorDialog.xaml
- [ ] T265 [US7] Add InsertTemplateCommand to MainWindowViewModel
- [ ] T266 [US7] Add template menu with hierarchical categories to MainWindow menu bar
- [ ] T267 [US7] Add template hotkey support to HotkeyService
- [ ] T268 [US7] Add prompt dialog for {PROMPT:} variables in src/ClipMate.App/Views/Dialogs/PromptDialog.xaml
- [ ] T269 [US7] Add template import/export to TemplateService
- [ ] T270 [US7] Add usage count tracking to TemplateRepository
- [ ] T271 [US7] Run all US7 unit tests and verify 90%+ coverage
- [ ] T272 [US7] Run all US7 integration tests

**Checkpoint**: User Stories 1-7 should all work independently

---

## Phase 10: User Story 8 - Sound Feedback System (Priority: P4)

**Goal**: Audio cues for clipboard operations to provide confidence without watching screen.

**Independent Test**: Perform operations (copy, paste, search), verify appropriate sounds play, test customization.

### Tests for User Story 8 (MANDATORY per Constitution) ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (TDD required)**

- [ ] T273 [P] [US8] Unit test for SoundService.PlayAsync in tests/ClipMate.Tests.Unit/Services/SoundServiceTests.cs
- [ ] T274 [P] [US8] Unit test for SoundService volume control in tests/ClipMate.Tests.Unit/Services/SoundServiceTests.cs
- [ ] T275 [P] [US8] Integration test for sound playback with NAudio in tests/ClipMate.Tests.Integration/SoundTests.cs

### Implementation for User Story 8

- [ ] T276 [US8] Implement SoundService with NAudio playback in src/ClipMate.Core/Services/SoundService.cs (depends on T052, T034)
- [ ] T277 [US8] Add PlayAsync method with WAV file support to SoundService
- [ ] T278 [US8] Add volume control (0-100%) to SoundService
- [ ] T279 [US8] Add sound event mapping (capture, paste, error, etc.) to SoundService
- [ ] T280 [US8] Add global mute toggle to SoundService
- [ ] T281 [US8] Add sound caching for performance to SoundService
- [ ] T282 [US8] Create default sound files (capture.wav, paste.wav, error.wav) in src/ClipMate.App/Resources/Sounds/
- [ ] T283 [P] [US8] Create SoundSettingsPanel.xaml in src/ClipMate.App/Views/Controls/SoundSettingsPanel.xaml
- [ ] T284 [US8] Add event-to-sound mapping UI to SoundSettingsPanel.xaml
- [ ] T285 [US8] Add custom sound file selection to SoundSettingsPanel.xaml
- [ ] T286 [US8] Add volume slider to SoundSettingsPanel.xaml
- [ ] T287 [US8] Add sound preview button to SoundSettingsPanel.xaml
- [ ] T288 [US8] Add global mute toggle to SoundSettingsPanel.xaml
- [ ] T289 [US8] Wire ClipboardService.ClipCaptured to SoundService.PlayAsync("capture")
- [ ] T290 [US8] Wire PowerPasteViewModel.PasteSelected to SoundService.PlayAsync("paste")
- [ ] T291 [US8] Wire error handlers to SoundService.PlayAsync("error")
- [ ] T292 [US8] Add sound theme system with predefined sets to SoundService
- [ ] T293 [US8] Run all US8 unit tests and verify 90%+ coverage
- [ ] T294 [US8] Run all US8 integration tests

**Checkpoint**: All 8 user stories should now be independently functional

---

## Phase 11: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories and final quality assurance

### System Integration

- [ ] T295 [P] Create system tray integration with NotifyIcon in src/ClipMate.App/SystemTray/TrayIconManager.cs
- [ ] T296 [P] Add context menu to system tray (Open, PowerPaste, Exit) in TrayIconManager.cs
- [ ] T297 [P] Add minimize to tray behavior to MainWindow.xaml.cs
- [ ] T298 [P] Add Windows startup registration to SettingsService
- [ ] T299 [P] Create startup configuration dialog in src/ClipMate.App/Views/Dialogs/StartupDialog.xaml
- [ ] T300 [P] Add toast notification support for errors/warnings in src/ClipMate.App/Notifications/ToastNotificationService.cs
- [ ] T301 [P] Add multi-monitor support with window position tracking to MainWindowViewModel
- [ ] T302 Add proper application shutdown and cleanup in App.xaml.cs

### Settings & Preferences

- [ ] T303 Implement SettingsService with JSON storage in src/ClipMate.Core/Services/SettingsService.cs (depends on T053)
- [ ] T304 [P] Create SettingsDialog.xaml with tabbed interface in src/ClipMate.App/Views/Dialogs/SettingsDialog.xaml
- [ ] T305 [P] Add General settings tab (startup, theme, language) to SettingsDialog.xaml
- [ ] T306 [P] Add Capture settings tab (filters, formats, debounce) to SettingsDialog.xaml
- [ ] T307 [P] Add PowerPaste settings tab (hotkey, item count) to SettingsDialog.xaml
- [ ] T308 [P] Add Collections settings tab (retention, auto-categorize) to SettingsDialog.xaml
- [ ] T309 Add settings persistence and loading on startup to SettingsService

### Performance Optimization

- [ ] T310 [P] Profile database query performance with 100k clips dataset and optimize indexes
- [ ] T311 [P] Implement memory pooling for large image clips in src/ClipMate.Core/Utilities/ImagePool.cs
- [ ] T312 [P] Add background cleanup task for expired clips in src/ClipMate.Core/Services/CleanupService.cs
- [ ] T313 [P] Optimize UI rendering with VirtualizingStackPanel validation in ClipListView
- [ ] T314 Add progressive loading for large collections in ClipListViewModel
- [ ] T315 [P] Add performance monitoring and diagnostics in src/ClipMate.Core/Diagnostics/PerformanceMonitor.cs
- [ ] T316 Run memory leak detection tests with long-running scenarios
- [ ] T317 Validate <100ms capture, <50ms search, <50MB memory requirements per constitution

### Data Management

- [ ] T318 [P] Add clip deduplication based on ContentHash to ClipService
- [ ] T319 [P] Implement retention policy enforcement in src/ClipMate.Core/Services/RetentionService.cs
- [ ] T320 Add database optimization and compression to BackupService
- [ ] T321 [P] Add export functionality (text, HTML, CSV) to src/ClipMate.Core/Services/ExportService.cs
- [ ] T322 [P] Add import functionality from text/CSV in src/ClipMate.Core/Services/ImportService.cs
- [ ] T323 Add database repair and recovery to BackupService

### Documentation & Help

- [ ] T324 [P] Create user documentation in docs/user-guide.md
- [ ] T325 [P] Create keyboard shortcuts reference in docs/keyboard-shortcuts.md
- [ ] T326 [P] Create troubleshooting guide in docs/troubleshooting.md
- [ ] T327 [P] Add inline help tooltips to all UI elements
- [ ] T328 [P] Create About dialog with version/license info in src/ClipMate.App/Views/Dialogs/AboutDialog.xaml

### Testing & Quality Assurance

- [ ] T329 Run full unit test suite and verify 90%+ coverage requirement met
- [ ] T330 Run all integration tests including Windows API interactions
- [ ] T331 Create UI automation test suite for critical workflows in tests/ClipMate.Tests.Integration/UI/
- [ ] T332 [P] Create performance benchmark tests in tests/ClipMate.Tests.Performance/
- [ ] T333 Run performance regression tests against constitution requirements
- [ ] T334 [P] Add accessibility compliance testing (screen reader, keyboard navigation)
- [ ] T335 Test on Windows 10 (1809+) and Windows 11 with different DPI settings
- [ ] T336 Run stress tests with 100k+ clips and multiple collections
- [ ] T337 Validate quickstart.md by following setup instructions on clean machine

### Code Quality

- [ ] T338 [P] Code review of all services for SOLID principles compliance
- [ ] T339 [P] Code review of async/await patterns for correctness
- [ ] T340 [P] Refactor any code smells or duplication found
- [ ] T341 [P] Validate separation of concerns (UI/business/data layers)
- [ ] T342 [P] Run static analysis tools (Roslyn analyzers)
- [ ] T343 Update XML documentation comments for all public APIs
- [ ] T344 Validate all constitutional gates are satisfied

### Deployment Preparation

- [ ] T345 [P] Set up ClickOnce deployment configuration in src/ClipMate.App/ClipMate.App.csproj
- [ ] T346 [P] Create MSI installer project with WiX Toolset in installer/
- [ ] T347 [P] Add Windows integration (file associations, registry) to installer
- [ ] T348 [P] Create portable application build configuration in build/portable.ps1
- [ ] T349 [P] Implement automatic update system in src/ClipMate.App/Updates/UpdateService.cs
- [ ] T350 [P] Add digital signing for executables and installers
- [ ] T351 Create release notes in CHANGELOG.md
- [ ] T352 Create deployment guide in docs/deployment.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-10)**: All depend on Foundational phase completion
  - User stories CAN proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P1 → P2 → P2 → P2 → P3 → P3 → P4)
- **Polish (Phase 11)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational - No dependencies on other stories
- **User Story 2 (P1)**: Can start after Foundational - Integrates with US1 for displaying clips but independently testable
- **User Story 3 (P2)**: Can start after Foundational - Requires US1 (clip data) but independently testable
- **User Story 4 (P2)**: Can start after Foundational - Extends US2 (UI) and US1 (data) but independently testable
- **User Story 5 (P2)**: Can start after Foundational - Requires US1 (clip data) and US2 (UI) but independently testable
- **User Story 6 (P3)**: Can start after Foundational - Operates on clip data from US1 but independently testable
- **User Story 7 (P3)**: Can start after Foundational - Independent feature, no dependencies on other stories
- **User Story 8 (P4)**: Can start after Foundational - Integrates with all stories for sound cues but independently testable

### Within Each User Story

1. **Tests FIRST** (TDD required per constitution) - Write tests, ensure they FAIL
2. **Service interfaces** (from Foundational phase)
3. **Service implementations** - Core business logic
4. **ViewModels** - Presentation logic
5. **Views** - XAML UI components
6. **Integration** - Wire components together
7. **Tests PASS** - Run tests, ensure 90%+ coverage

### Parallel Opportunities

**Setup Phase**: All tasks marked [P] (T002-T016) can run in parallel

**Foundational Phase**: Groups of parallel tasks:
- T017-T027: All model/enum definitions in parallel
- T028-T034: All repository interfaces in parallel
- T036-T042: All repository implementations in parallel (after T035)
- T045-T054: All service interfaces in parallel
- T055-T059: All Win32 wrappers in parallel
- T060-T063: MVVM infrastructure in parallel
- T068-T071: Testing infrastructure in parallel
- T072-T076: Error handling in parallel

**User Story Phases**: After Foundational completes, user stories can proceed in parallel by different team members:
- Developer A: US1 (T077-T104)
- Developer B: US2 (T105-T141) - starts T112-T116 UI in parallel while A works on US1
- Developer C: US3 (T142-T165) - can prepare after US1 data available
- Continue pattern for US4-US8

**Within User Stories**: Tests marked [P] can run in parallel, UI tasks marked [P] can run in parallel

**Polish Phase**: Most tasks marked [P] can run in parallel (T295-T352)

---

## Parallel Example: User Story 1

```powershell
# Launch all US1 tests together (TDD - write first, ensure FAIL):
# T077-T084: All unit and integration tests for US1

# While tests are being written, start service implementation:
# T085-T104: ClipboardService, ClipService, ApplicationFilterService implementation

# Tests should FAIL initially, then PASS after implementation
```

---

## Parallel Example: User Story 2

```powershell
# Launch all US2 tests together:
# T105-T111: All unit, integration, and UI automation tests

# Launch UI components in parallel:
# T112-T116: MainWindow XAML components (layout, menus, toolbars, status)
# T119: CollectionTreeView XAML
# T124-T125: ClipListView XAML with view modes
# T131-T135: PreviewPane XAML with templates

# ViewModels depend on services but can be parallelized:
# T117: MainWindowViewModel
# T120: CollectionTreeViewModel  
# T126: ClipListViewModel
# T132: PreviewPaneViewModel

# Services can be implemented in parallel:
# T122: CollectionService
# T123: FolderService
```

---

## Implementation Strategy

### MVP First (User Story 1 + User Story 2 Only)

**Rationale**: US1 provides core clipboard capture, US2 provides basic UI to view/access clips. Together they deliver minimum viable product.

1. Complete Phase 1: Setup (T001-T016)
2. Complete Phase 2: Foundational (T017-T076) - CRITICAL, blocks everything
3. Complete Phase 3: User Story 1 (T077-T104) - Clipboard capture working
4. Complete Phase 4: User Story 2 (T105-T141) - UI to view captured clips
5. **STOP and VALIDATE**: Test US1 + US2 work together, user can copy and view clips
6. Deploy/demo MVP if ready

**Value Delivered**: Users can capture clipboard history and browse it in organized interface. Core value proposition proven.

### Incremental Delivery (Recommended)

1. **Foundation + US1 + US2** → MVP (clipboard capture + basic UI) → Deploy/Demo
2. **Add US3** (PowerPaste) → Quick access feature → Deploy/Demo
3. **Add US4** (Collections) → Organization for power users → Deploy/Demo
4. **Add US5** (Search) → Discovery at scale → Deploy/Demo
5. **Add US6** (Text Tools) → Productivity enhancement → Deploy/Demo
6. **Add US7** (Templates) → Automation for power users → Deploy/Demo
7. **Add US8** (Sound) → Polish and feedback → Deploy/Demo
8. **Polish Phase** → Production-ready release

Each increment adds value without breaking previous functionality. Users can adopt early and provide feedback.

### Parallel Team Strategy

With 3+ developers after Foundational phase:

**Week 1-2: Foundation**
- All team members: Complete Setup + Foundational together (T001-T076)

**Week 3-4: MVP**
- Developer A: US1 Clipboard Capture (T077-T104)
- Developer B: US2 Main UI (T105-T141)
- Developer C: Start US3 PowerPaste prep (research, test planning)

**Week 5-6: Core Features**
- Developer A: US3 PowerPaste (T142-T165)
- Developer B: US4 Collections (T166-T195)
- Developer C: US5 Search (T196-T224)

**Week 7-8: Advanced Features**
- Developer A: US6 Text Tools (T225-T247)
- Developer B: US7 Templates (T248-T272)
- Developer C: US8 Sound (T273-T294)

**Week 9-10: Polish**
- All team members: Polish & Cross-Cutting (T295-T352) in parallel

**Week 11: QA & Deploy**
- Testing, bug fixes, deployment preparation

---

## Notes

- **[P] marker**: Task can run in parallel with other [P] tasks (different files, no dependencies)
- **[Story] label**: Maps task to specific user story for traceability and independent implementation
- **Tests are MANDATORY**: 90%+ coverage required per ClipMate Constitution
- **TDD approach**: Write tests FIRST, ensure they FAIL, then implement until tests PASS
- **Independent stories**: Each user story should be completable and testable without others
- **Constitution gates**: Validate all gates satisfied before considering complete (see plan.md)
- **Performance requirements**: <100ms capture, <50ms search, <50MB memory must be validated
- **Commit strategy**: Commit after each task or logical group, use feature branches per user story
- **Stop at checkpoints**: Validate each user story works independently before proceeding
- **File paths**: All paths follow ClipMate multi-project structure (src/ClipMate.*/tests/ClipMate.Tests.*)

---

## Task Summary

**Total Tasks**: 352 tasks

**Phase Breakdown**:
- Phase 1 (Setup): 16 tasks
- Phase 2 (Foundational): 60 tasks (CRITICAL - blocks all user stories)
- Phase 3 (US1 - Clipboard Capture): 28 tasks (8 tests + 20 implementation)
- Phase 4 (US2 - Three-Pane UI): 37 tasks (7 tests + 30 implementation)
- Phase 5 (US3 - PowerPaste): 24 tasks (5 tests + 19 implementation)
- Phase 6 (US4 - Collections): 30 tasks (5 tests + 25 implementation)
- Phase 7 (US5 - Search): 29 tasks (6 tests + 23 implementation)
- Phase 8 (US6 - Text Tools): 23 tasks (5 tests + 18 implementation)
- Phase 9 (US7 - Templates): 25 tasks (4 tests + 21 implementation)
- Phase 10 (US8 - Sound): 22 tasks (3 tests + 19 implementation)
- Phase 11 (Polish): 58 tasks (cross-cutting concerns, testing, deployment)

**Test Tasks**: 48 mandatory test tasks (TDD required per constitution)

**Parallelizable Tasks**: 145 tasks marked [P] (can run concurrently)

**MVP Scope**: Phase 1-2 + Phase 3 (US1) + Phase 4 (US2) = 141 tasks for minimum viable product

**Suggested MVP**: Complete through US2 (141 tasks) for working clipboard manager with basic UI, then iterate with US3+ based on feedback.
