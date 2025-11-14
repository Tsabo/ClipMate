# Tasks: ClipBar Quick Access & UI Density Improvements

**Feature**: 002-clipbar-quick-access  
**Created**: 2025-11-14  
**Status**: Planning

## Overview

This feature has two main components:
1. **Complete Quick Access Popup** - Finish the Ctrl+Shift+V popup with paste functionality
2. **UI Density Improvements** - Make entire application more compact (Windows utility style)

**Estimated Time**: 15-20 hours total

---

## Phase 1: Rename PowerPaste → QuickAccess (2 hours)

**Goal**: Proper naming to avoid confusion with future PowerPaste feature

- [ ] **T001** [P] Rename `PowerPasteWindow.xaml` → `QuickAccessWindow.xaml`
- [ ] **T002** [P] Rename `PowerPasteWindow.xaml.cs` → `QuickAccessWindow.xaml.cs`
- [ ] **T003** [P] Rename `PowerPasteViewModel.cs` → `QuickAccessViewModel.cs`
- [ ] **T004** [P] Rename `PowerPasteCoordinator.cs` → `QuickAccessCoordinator.cs`
- [ ] **T005** [P] Update all `using` statements and class references
- [ ] **T006** [P] Update DI registrations in `App.xaml.cs`
- [ ] **T007** [P] Rename `PowerPasteViewModelTests.cs` → `QuickAccessViewModelTests.cs`
- [ ] **T008** [P] Update test class names and references
- [ ] **T009** Build and verify all tests pass
- [ ] **T010** Update documentation (spec.md, tasks.md)
- [ ] **T011** Commit: "refactor: Rename PowerPaste to QuickAccess"

**Checkpoint**: Build succeeds, tests pass, terminology correct

---

## Phase 2: Implement PasteService (4 hours)

**Goal**: Actually paste selected clips to active application

### Service Interface & Implementation

- [ ] **T012** [P] Create `IPasteService.cs` interface in `Core/Services/`
  ```csharp
  Task<bool> PasteToActiveWindowAsync(Clip clip, CancellationToken cancellationToken = default);
  Task<bool> PasteTextAsync(string text, CancellationToken cancellationToken = default);
  ```

- [ ] **T013** [P] Create `PasteService.cs` in `Platform/Services/`
  - Get foreground window handle (GetForegroundWindow)
  - Implement paste strategies:
    1. SetClipboard + SendInput(Ctrl+V)
    2. SendInput with VK_RETURN for each character (fallback)
    3. SendMessage with WM_PASTE (fallback)

- [ ] **T014** [P] Add Win32 methods to `NativeMethods.txt`:
  - GetForegroundWindow
  - SendInput
  - SendMessage
  - WM_PASTE constant

- [ ] **T015** [P] Register `PasteService` in DI container

### Unit Tests

- [ ] **T016** [P] Create `PasteServiceTests.cs` in `Tests.Unit/Services/`
  - Test clipboard set/restore
  - Test strategy selection
  - Test error handling

### Integration with QuickAccess

- [ ] **T017** Wire `PasteService` into `QuickAccessViewModel`
- [ ] **T018** Update `SelectClipAsync()` to call `PasteService.PasteToActiveWindowAsync()`
- [ ] **T019** Add error handling and user feedback

### Integration Tests

- [ ] **T020** [P] Create `PasteServiceIntegrationTests.cs`
  - Test paste to Notepad
  - Test paste to TextBox in test window
  - Test clipboard restore after paste

- [ ] **T021** Manual testing with applications:
  - Notepad
  - VS Code
  - Chrome
  - Excel
  - PowerShell

**Checkpoint**: Ctrl+Shift+V → select clip → pastes to active window

---

## Phase 3: Window Positioning (2 hours)

**Goal**: Show popup near cursor, not centered on screen

- [ ] **T022** [P] Add Win32 methods to `NativeMethods.txt`:
  - GetCursorPos
  - MonitorFromPoint
  - GetMonitorInfo

- [ ] **T023** Create `WindowPositioner.cs` helper in `Platform/Helpers/`
  - Get cursor position
  - Get monitor bounds containing cursor
  - Calculate popup position (near cursor, within bounds)
  - Handle DPI scaling

- [ ] **T024** Update `QuickAccessCoordinator` to position window on show
  - Call `WindowPositioner.GetPopupPosition()`
  - Set `QuickAccessWindow.Left` and `.Top`

- [ ] **T025** Add unit tests for `WindowPositioner`
  - Test bounds checking
  - Test multi-monitor scenarios
  - Test DPI scaling

- [ ] **T026** Manual testing:
  - Single monitor
  - Dual monitors
  - Triple monitors
  - 100%, 150%, 200% DPI

**Checkpoint**: Popup appears near cursor on correct monitor

---

## Phase 4: Global UI Density Styles (3 hours)

**Goal**: Add compact Windows utility styles to entire application

### App.xaml Global Styles

- [ ] **T027** [P] Add `CompactButton` style to `App.xaml`
  - Height: 22px
  - Padding: 8,2
  - FontSize: 9pt

- [ ] **T028** [P] Add `CompactTextBox` style to `App.xaml`
  - Height: 20px
  - Padding: 4,2
  - FontSize: 9pt

- [ ] **T029** [P] Add `CompactListBoxItem` style to `App.xaml`
  - Height: 20px
  - Padding: 4,2
  - FontSize: 9pt

- [ ] **T030** [P] Add `CompactTreeViewItem` style to `App.xaml`
  - Height: 20px
  - Padding: 2,1
  - FontSize: 9pt

- [ ] **T031** [P] Add `CompactDataGrid` style to `App.xaml`
  - FontSize: 9pt
  - RowHeight: 18px
  - ColumnHeaderHeight: 20px

- [ ] **T032** [P] Set global FontFamily to "Segoe UI" in App.xaml

- [ ] **T033** Test styles render correctly

**Checkpoint**: Global compact styles available

---

## Phase 5: MainWindow UI Density (4 hours)

**Goal**: Apply compact styles to main window

### DataGrid Styling

- [ ] **T034** Update `MainWindow.xaml` DataGrid
  - Add FontSize="9"
  - Add RowHeight="18"
  - Add ColumnHeaderHeight="20"
  - Update RowStyle with MinHeight="18", Padding="0"

- [ ] **T035** Add ColumnHeaderStyle
  - Height: 20px
  - Padding: 4,2
  - FontSize: 9pt
  - FontWeight: Normal

- [ ] **T036** Add CellStyle
  - Padding: 4,2
  - BorderThickness: 0

- [ ] **T037** Test DataGrid readability at different DPI

### TreeView Styling

- [ ] **T038** Update `MainWindow.xaml` TreeView
  - Add FontSize="9"
  - Add ItemContainerStyle with Height="20", Padding="2,1"

- [ ] **T039** Test TreeView expansion/collapse

### ToolBar Styling

- [ ] **T040** Update `MainWindow.xaml` ToolBar
  - Height: 26px
  - Padding: 2,1
  - Button Height: 22px
  - Button Width: 22px
  - Icon Size: 16x16px

- [ ] **T041** Create 16x16px icons for toolbar buttons

### StatusBar Styling

- [ ] **T042** Update `MainWindow.xaml` StatusBar
  - Height: 20px
  - Padding: 4,2
  - FontSize: 9pt

### Splitters

- [ ] **T043** Update GridSplitter widths from 5px to 3px

### Preview Pane

- [ ] **T044** Update preview pane TextBox/RichTextBox
  - FontSize: 9pt
  - Reduce padding

### Menu Bar

- [ ] **T045** Update MenuBar
  - FontSize: 9pt
  - Reduce padding

**Checkpoint**: Main window looks compact like Windows Explorer

---

## Phase 6: QuickAccessWindow UI Density (2 hours)

**Goal**: Redesign popup with minimal padding and compact list

- [ ] **T046** Complete redesign of `QuickAccessWindow.xaml`
  - Change WindowStyle to None
  - Add 1px border manually
  - Remove all margins/padding from Grid

- [ ] **T047** Update Search Box styling
  - Height: 20px
  - Padding: 4,2
  - FontSize: 9pt
  - BorderThickness: 0,0,0,1 (only bottom border)

- [ ] **T048** Update ListBox styling
  - FontSize: 9pt
  - BorderThickness: 0
  - Padding: 0

- [ ] **T049** Update ListBoxItem styling
  - Height: 20px
  - Padding: 4,2
  - BorderThickness: 0,0,0,1 (separator)

- [ ] **T050** Update ItemTemplate
  - 16x16px icon
  - Text with CharacterEllipsis
  - VerticalAlignment: Center

- [ ] **T051** Test readability with long clip titles

- [ ] **T052** Compare with ClipMate 7.5 screenshots side-by-side

**Checkpoint**: QuickAccess popup looks like Windows utility, not web app

---

## Phase 7: Polish & Testing (3 hours)

### Features

- [ ] **T053** Add clip type icons (text/image/file/HTML) to ListBox
- [ ] **T054** Add source application name to clip preview (optional)
- [ ] **T055** Add keyboard shortcuts Ctrl+1-9 for quick select
- [ ] **T056** Add clip timestamp to preview tooltip

### Testing

- [ ] **T057** Test on Windows 10 (1809+)
- [ ] **T058** Test on Windows 11
- [ ] **T059** Test at 100% DPI
- [ ] **T060** Test at 150% DPI
- [ ] **T061** Test at 200% DPI
- [ ] **T062** Test single monitor setup
- [ ] **T063** Test dual monitor setup
- [ ] **T064** Test triple monitor setup
- [ ] **T065** Test with Windows High Contrast theme
- [ ] **T066** Test with screen reader (accessibility)

### Performance

- [ ] **T067** Measure popup show time (must be <100ms)
- [ ] **T068** Measure search filter time (must be <50ms)
- [ ] **T069** Measure paste time (must be <200ms)
- [ ] **T070** Profile memory usage (must be <5MB for popup)

### Documentation

- [ ] **T071** Update user guide with Quick Access feature
- [ ] **T072** Document keyboard shortcuts (Ctrl+Shift+V, arrow keys, etc.)
- [ ] **T073** Add troubleshooting section for paste failures
- [ ] **T074** Update architecture diagrams
- [ ] **T075** Create UI style guide document

**Checkpoint**: All tests pass, performance requirements met, docs updated

---

## Phase 8: Code Review & Cleanup (1 hour)

- [ ] **T076** Code review all changes
- [ ] **T077** Remove commented-out code
- [ ] **T078** Update XML documentation comments
- [ ] **T079** Run static analysis (Roslyn analyzers)
- [ ] **T080** Fix any warnings
- [ ] **T081** Verify 90%+ test coverage
- [ ] **T082** Final commit and push

---

## Parallel Opportunities

**Can be done in parallel:**
- Phase 1 (Rename) + Phase 2 (PasteService) - Different files
- Phase 4 (Global Styles) + Phase 3 (Positioning) - Different areas
- T041 (Create icons) can be done anytime
- Documentation (T071-T075) can be done throughout

---

## Testing Matrix

| Application | Paste Works | Notes |
|-------------|-------------|-------|
| Notepad     | [ ]         |       |
| Notepad++   | [ ]         |       |
| VS Code     | [ ]         |       |
| Visual Studio | [ ]       |       |
| Chrome      | [ ]         |       |
| Firefox     | [ ]         |       |
| Edge        | [ ]         |       |
| Excel       | [ ]         |       |
| Word        | [ ]         |       |
| PowerShell  | [ ]         |       |
| CMD         | [ ]         |       |
| Outlook     | [ ]         |       |

---

## Success Criteria Summary

- [ ] Quick Access popup opens with Ctrl+Shift+V
- [ ] Popup positioned near cursor on correct monitor
- [ ] Selected clip pastes to active application
- [ ] Popup closes after paste
- [ ] Escape closes without pasting
- [ ] UI density matches Windows Explorer detail view
- [ ] Row height 18-20px throughout application
- [ ] Font size 9pt throughout application
- [ ] Performance <100ms popup, <50ms search, <200ms paste
- [ ] Works on Windows 10 and 11
- [ ] Works at 100%, 150%, 200% DPI
- [ ] 90%+ test coverage
- [ ] All documentation updated

---

## Dependencies

- Phase 2 depends on Phase 1 (renaming)
- Phase 3 can be parallel with Phase 1-2
- Phase 4 can be parallel with Phase 1-3
- Phase 5 depends on Phase 4 (global styles)
- Phase 6 depends on Phase 4 (global styles)
- Phase 7 depends on all previous phases
- Phase 8 depends on all previous phases

---

## Risk Mitigation

**Risk**: Paste fails in some applications  
**Mitigation**: Implement multiple paste strategies with fallback

**Risk**: UI too compact, hard to read  
**Mitigation**: Follow Windows Explorer as reference, test with users

**Risk**: Positioning wrong on multi-monitor  
**Mitigation**: Extensive testing on different configurations

**Risk**: DPI scaling issues  
**Mitigation**: Test at 100%, 150%, 200% DPI throughout

---

## Notes

- Use feature branch: `002-clipbar-quick-access`
- Commit frequently after each task
- Run tests before each commit
- Compare with ClipMate 7.5 screenshots throughout
