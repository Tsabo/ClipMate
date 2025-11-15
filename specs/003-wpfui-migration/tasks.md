# Feature 003: Migrate to WPF UI - Task Breakdown

## Phase 1: Add WPF UI and Update App.xaml (2 hours)

- [ ] **T001** Add Wpf.Ui package to Directory.Packages.props
  - Version: 4.0.3
  - Label: UI Frameworks

- [ ] **T002** Update App.xaml to include WPF UI theme dictionaries
  - Add xmlns:ui namespace
  - Add ThemesDictionary (Light theme)
  - Add ControlsDictionary

- [ ] **T003** Remove all custom compact styles from App.xaml
  - Delete CompactButton, CompactTextBox, CompactListBox styles
  - Delete CompactTreeView, CompactDataGrid styles
  - Delete CompactMenu, CompactMenuItem styles
  - Delete CompactToolBar, CompactStatusBar styles
  - Delete Window global style (FontFamily, FontSize)

- [ ] **T004** Build and verify application starts
  - Resolve any compilation errors
  - Verify no runtime crashes
  - Note: UI will look broken - expected at this phase

## Phase 2: Convert MainWindow to FluentWindow (3 hours)

- [ ] **T005** Update MainWindow.xaml root element
  - Change Window to ui:FluentWindow
  - Add xmlns:ui namespace declaration
  - Keep existing Title, Height, Width, MinHeight, MinWidth

- [ ] **T006** Remove Window.Resources custom styles
  - Delete PaneHeaderStyle
  - Delete PaneBorderStyle
  - Will use WPF UI defaults

- [ ] **T007** Update Menu styling
  - Remove all FontSize="10" attributes
  - Remove Padding attributes
  - Let WPF UI apply default menu styling

- [ ] **T008** Convert ToolBar to WPF UI buttons
  - Replace Button with ui:Button
  - Add ui:SymbolIcon for each button:
    * New → Symbol="Add24"
    * Folder → Symbol="Folder24"
    * Copy → Symbol="Copy24"
    * Paste → Symbol="ClipboardPaste24"
    * Delete → Symbol="Delete24"
    * Search → Symbol="Search24"
  - Remove explicit FontSize, Padding, Height

- [ ] **T009** Update DataGrid styling
  - Remove custom RowStyle, ColumnHeaderStyle, CellStyle
  - Remove explicit FontSize, FontFamily
  - Keep RowHeight, ColumnHeaderHeight (optional, test both ways)
  - Keep IsReadOnly="True"

- [ ] **T010** Update GridSplitters
  - Remove explicit Width/Height (use defaults)
  - Or adjust to WPF UI standards (test visually)

- [ ] **T011** Update StatusBar styling
  - Remove explicit Height, Padding, FontSize
  - Let WPF UI apply default styling
  - Keep BorderBrush, BorderThickness, Background (or test without)

- [ ] **T012** Update preview pane buttons
  - Replace Button with ui:Button
  - Remove explicit FontSize, Padding, Height
  - Keep Width (or remove and test)

- [ ] **T013** Update MainWindow.xaml.cs if needed
  - Change base class from Window to FluentWindow (if inheritance used)
  - Update any typeof(Window) to typeof(FluentWindow)

- [ ] **T014** Build and test MainWindow
  - Verify window opens and displays correctly
  - Test all menu items
  - Test toolbar buttons
  - Test DataGrid interaction (no DisplayTitle error)
  - Test splitters
  - Test status bar updates

## Phase 3: Replace System Tray with WPF UI NotifyIcon (2 hours)

- [ ] **T015** Remove System.Windows.Forms reference
  - Remove from ClipMate.Platform.csproj
  - Remove System.Drawing.Common if only used for tray icon

- [ ] **T016** Create WPF UI NotifyIcon in MainWindow.xaml
  - Add ui:NotifyIcon element (not inside window - in code-behind or separate resource)
  - Set TooltipText="ClipMate"
  - Set Icon path to application icon
  - Set MenuOnRightClick="True"

- [ ] **T017** Create ContextMenu for NotifyIcon
  - MenuItem "Show ClipMate" with Window24 icon
  - Separator
  - MenuItem "Quick Access" with ClipboardPaste24 icon
  - Separator  
  - MenuItem "Exit" with DismissCircle24 icon

- [ ] **T018** Rewrite SystemTrayService.cs for WPF UI
  - Replace NotifyIcon (WinForms) with NotifyIcon (WPF UI)
  - Remove BuildContextMenu() method (use XAML)
  - Remove DPI scaling code (WPF UI handles this)
  - Update Initialize() to use WPF UI NotifyIcon
  - Update ShowBalloonTip() to use WPF UI notification

- [ ] **T019** Remove DpiHelper.cs
  - No longer needed with WPF UI
  - Remove from ClipMate.Platform project

- [ ] **T020** Update SystemTrayService event handlers
  - Wire up NotifyIcon.Click to show window
  - Wire up ContextMenu item click events
  - Test tray icon click behavior

- [ ] **T021** Test tray icon functionality
  - Verify icon appears in system tray
  - Test right-click context menu
  - Test "Show ClipMate" command
  - Test "Quick Access" command
  - Test "Exit" command
  - Test on different DPI settings (100%, 125%, 150%, 200%)

## Phase 4: Convert Dialogs and Views (3 hours)

- [ ] **T022** Convert TextToolsDialog.xaml to FluentWindow
  - Change Window to ui:FluentWindow
  - Add xmlns:ui namespace
  - Remove custom styling
  - Update buttons to ui:Button

- [ ] **T023** Convert TemplateEditorDialog.xaml to FluentWindow
  - Change Window to ui:FluentWindow
  - Add xmlns:ui namespace
  - Remove custom styling
  - Update buttons to ui:Button
  - Update TextBoxes (optional - WPF UI styles standard controls)

- [ ] **T024** Update CollectionTreeView.xaml
  - Remove custom TreeView styling
  - Let WPF UI apply default TreeView style
  - Test folder tree display and interaction

- [ ] **T025** Update SearchPanel.xaml
  - Convert Button to ui:Button
  - Add search icon: ui:SymbolIcon Symbol="Search24"
  - Convert TextBox (optional - standard controls work)
  - Remove custom styling

- [ ] **T026** Test all dialogs
  - Open TextToolsDialog - verify display and functionality
  - Open TemplateEditorDialog - verify display and functionality
  - Test CollectionTreeView - verify folders display correctly
  - Test SearchPanel - verify search works

## Phase 5: Convert PowerPaste/QuickAccess Window (2 hours)

- [ ] **T027** Convert PowerPasteWindow.xaml to FluentWindow
  - Change Window to ui:FluentWindow
  - Add xmlns:ui namespace
  - Remove custom styling

- [ ] **T028** Update PowerPasteWindow ListBox
  - Consider using ui:CardControl for each clip
  - Or use ListView with WPF UI styling
  - Remove custom ListBox ItemTemplate styling

- [ ] **T029** Update PowerPasteWindow.xaml.cs if needed
  - Update Window type references
  - Test keyboard navigation still works

- [ ] **T030** Test PowerPaste/QuickAccess functionality
  - Open with Ctrl+Shift+V
  - Verify clips display correctly
  - Test keyboard navigation (Up/Down/Enter/Escape)
  - Test paste functionality
  - Verify window positioning

## Phase 6: Testing and Refinement (2-4 hours)

### Visual Testing

- [ ] **T031** Test MainWindow visual appearance
  - Window title bar (FluentWindow features)
  - Menu bar
  - Toolbar (icons and layout)
  - Collection tree
  - Clip DataGrid
  - Preview pane
  - Status bar
  - Splitters

- [ ] **T032** Test all dialogs visual appearance
  - TextToolsDialog
  - TemplateEditorDialog
  - PowerPasteWindow

- [ ] **T033** Test system tray
  - Icon visibility and clarity
  - Context menu appearance
  - Menu item icons

### Functional Testing

- [ ] **T034** Test all MainWindow interactions
  - Menu commands
  - Toolbar buttons
  - DataGrid selection and sorting
  - Preview pane tabs
  - Status bar updates
  - Window resizing and splitters

- [ ] **T035** Test all dialogs interactions
  - TextToolsDialog operations
  - TemplateEditorDialog save/cancel
  - PowerPaste selection and paste

- [ ] **T036** Test tray icon interactions
  - Show/hide window
  - Quick Access popup
  - Exit application

### Cross-DPI Testing

- [ ] **T037** Test on 100% DPI (1920x1080)
  - All windows display correctly
  - Text is readable
  - Icons are appropriate size

- [ ] **T038** Test on 125% DPI
  - All windows display correctly
  - Text is readable
  - Icons are appropriate size

- [ ] **T039** Test on 150% DPI (High DPI laptop)
  - All windows display correctly
  - Text is readable
  - Icons are appropriate size
  - Tray icon is correct size

- [ ] **T040** Test on 200% DPI (4K display)
  - All windows display correctly
  - Text is readable
  - Icons are appropriate size
  - Tray icon is correct size

### Platform Testing

- [ ] **T041** Test on Windows 11
  - Verify Fluent Design appearance
  - Test Windows 11 snap layouts (FluentWindow feature)
  - Verify all functionality

- [ ] **T042** Test on Windows 10
  - Verify appearance (should still look good)
  - Verify all functionality
  - Note any differences from Windows 11

### Performance Testing

- [ ] **T043** Measure application startup time
  - Compare before/after migration
  - Should be similar or better

- [ ] **T044** Test UI responsiveness
  - Window opening/closing
  - DataGrid scrolling with many items
  - Dialog opening

### Refinement Tasks

- [ ] **T045** Adjust spacing/padding if needed
  - Review all windows for spacing issues
  - Make minor adjustments using WPF UI properties
  - Document any customizations

- [ ] **T046** Fine-tune colors if needed
  - Check if default theme works well
  - Adjust specific colors if needed (e.g., delete button red)
  - Keep customizations minimal

- [ ] **T047** Update icons throughout application
  - Replace any text button labels with proper icons
  - Use ui:SymbolIcon consistently
  - Consider adding icons to menu items

- [ ] **T048** Review and clean up code
  - Remove unused using statements
  - Remove commented-out old code
  - Remove unused helper classes (DpiHelper)
  - Update code comments

- [ ] **T049** Update documentation
  - Update README if it mentions custom styling
  - Document WPF UI usage in copilot-instructions.md
  - Note any WPF UI customizations for future reference

- [ ] **T050** Final build and smoke test
  - Clean build entire solution
  - Run application
  - Quick test of all major features
  - Verify no console errors/warnings

## Success Checklist

After completing all tasks, verify:

- [ ] ✅ Application builds without errors
- [ ] ✅ No System.Windows.Forms references remain
- [ ] ✅ No custom compact styles in App.xaml
- [ ] ✅ All windows use FluentWindow
- [ ] ✅ Tray icon works correctly on all DPI settings
- [ ] ✅ All dialogs display and function correctly
- [ ] ✅ DataGrid has no TwoWay binding errors
- [ ] ✅ Application looks modern (Windows 11 Fluent Design)
- [ ] ✅ Performance is acceptable
- [ ] ✅ Code is cleaner and more maintainable

## Rollback Plan

If migration fails or causes major issues:

1. **Revert commits**: `git reset --hard <commit-before-migration>`
2. **Remove WPF UI package**: Delete from Directory.Packages.props
3. **Restore custom styles**: Revert App.xaml changes
4. **Restore tray icon**: Revert SystemTrayService changes
5. **Document issues**: Note what went wrong for future attempt

## Notes

- Can complete phases incrementally and test between phases
- Some controls can remain standard WPF (they work with WPF UI)
- WPF UI applies global styling to standard controls automatically
- Can mix ui:Button and Button if needed during transition
- Focus on getting it working first, polish later
