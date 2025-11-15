# Feature 003: Migrate to WPF UI Library

**Created:** 2025-11-14  
**Status:** Planning  
**Priority:** High  
**Estimated Effort:** 12-16 hours

## Problem Statement

Current custom styling approach creates maintenance burden:
- Custom compact styles scattered across App.xaml and component files
- Manual DPI handling and scaling
- Custom tray icon implementation using Windows Forms (System.Windows.Forms)
- Inconsistent theming and no built-in dark mode support
- Time-consuming to maintain and extend custom controls

## Goals

1. **Eliminate Custom Styling**: Remove all custom compact styles in favor of WPF UI's built-in Fluent Design
2. **Native Tray Icon Support**: Replace Windows Forms NotifyIcon with WPF UI's pure WPF tray implementation
3. **Modern UI**: Adopt Windows 11 Fluent Design System automatically
4. **Reduce Dependencies**: Remove System.Windows.Forms references
5. **Built-in Theming**: Leverage WPF UI's Light/Dark theme support
6. **Maintainability**: Use actively-maintained, well-documented library (8.9k stars, 2.5k projects using it)

## Solution: WPF UI Library

**Library:** [WPF UI (Wpf.Ui)](https://github.com/lepoco/wpfui)  
**Version:** 4.0.3 (Latest)  
**License:** MIT  
**Documentation:** https://wpfui.lepo.co/

### Key Benefits

1. **Fluent Design System**: Windows 11 native look and feel
2. **Pure WPF Tray Icon**: No Windows Forms dependency
3. **Rich Control Set**: Modern controls (FluentWindow, NavigationView, CardControl, etc.)
4. **Built-in Theming**: Light/Dark/HighContrast with auto-switching
5. **Navigation System**: Built-in navigation framework (we may not need initially)
6. **Mica/Acrylic Effects**: Modern window backdrop effects
7. **SymbolIcon**: 2000+ Fluent icons built-in
8. **Active Maintenance**: Regular updates, 101 contributors

## Migration Strategy

### Phase 1: Add WPF UI and Update App.xaml (2 hours)

**Changes:**
1. Add `Wpf.Ui` NuGet package to `Directory.Packages.props`
2. Update `App.xaml` to use WPF UI theme dictionaries
3. Remove all custom compact styles from `App.xaml`
4. Test application starts successfully

**Files:**
- `Directory.Packages.props` - Add package reference
- `App.xaml` - Replace custom styles with WPF UI dictionaries
- `App.xaml.cs` - No changes needed (DI already configured)

### Phase 2: Convert MainWindow to FluentWindow (3 hours)

**Changes:**
1. Change `MainWindow` from `Window` to `ui:FluentWindow`
2. Update `MainWindow.xaml` namespace declarations
3. Convert `Menu` to WPF UI menu (or keep standard - FluentWindow works with both)
4. Convert `ToolBar` buttons to `ui:Button` with `ui:SymbolIcon`
5. Update `DataGrid` styling (WPF UI has built-in support)
6. Convert status bar to use WPF UI styling
7. Test window displays correctly

**Files:**
- `MainWindow.xaml` - Convert to FluentWindow, update controls
- `MainWindow.xaml.cs` - Minimal changes (adjust Window type reference if needed)

### Phase 3: Replace System Tray with WPF UI NotifyIcon (2 hours)

**Changes:**
1. Remove `System.Windows.Forms` reference from `ClipMate.Platform.csproj`
2. Replace `NotifyIcon` (WinForms) with `ui:NotifyIcon` (WPF UI)
3. Update `SystemTrayService` to use WPF UI's tray implementation
4. Remove custom DPI scaling code (WPF UI handles this)
5. Convert `ContextMenuStrip` to WPF `ContextMenu` with WPF UI styling
6. Test tray icon, menu, and interactions

**Files:**
- `ClipMate.Platform.csproj` - Remove WinForms reference
- `SystemTrayService.cs` - Complete rewrite to use WPF UI
- Remove `DpiHelper.cs` - No longer needed

### Phase 4: Convert Dialogs and Views (3 hours)

**Changes:**
1. Convert `TextToolsDialog` to `ui:FluentWindow` or `ui:ContentDialog`
2. Convert `TemplateEditorDialog` to `ui:FluentWindow` or `ui:ContentDialog`
3. Update `CollectionTreeView` to use WPF UI TreeView styling
4. Update `SearchPanel` to use WPF UI TextBox and Button
5. Test all dialogs and views

**Files:**
- `TextToolsDialog.xaml` - Convert to FluentWindow/ContentDialog
- `TemplateEditorDialog.xaml` - Convert to FluentWindow/ContentDialog
- `CollectionTreeView.xaml` - Update styling
- `SearchPanel.xaml` - Update controls

### Phase 5: Convert PowerPaste/QuickAccess Window (2 hours)

**Changes:**
1. Convert `PowerPasteWindow` to `ui:FluentWindow`
2. Update controls to use WPF UI styling
3. Replace custom ListBox styling with WPF UI CardControl or ListView
4. Test popup behavior and keyboard navigation

**Files:**
- `PowerPasteWindow.xaml` - Convert to FluentWindow
- `PowerPasteWindow.xaml.cs` - Update if needed

### Phase 6: Testing and Refinement (2-4 hours)

**Testing:**
1. Visual inspection of all windows, dialogs, and controls
2. Test light/dark theme switching (if implemented)
3. Test tray icon on different DPI settings (100%, 125%, 150%, 200%)
4. Test all user interactions (clicking, keyboard navigation, etc.)
5. Test on Windows 10 and Windows 11
6. Performance testing (startup time, responsiveness)

**Refinement:**
1. Adjust spacing/padding if needed (WPF UI has sensible defaults)
2. Fine-tune colors/themes if needed
3. Update any custom control templates
4. Document any WPF UI customizations

## Technical Details

### NuGet Package

```xml
<ItemGroup Label="UI Frameworks">
  <PackageVersion Include="CommunityToolkit.Mvvm" Version="8.3.2" />
  <PackageVersion Include="Wpf.Ui" Version="4.0.3" />
</ItemGroup>
```

### App.xaml Structure

```xml
<Application x:Class="ClipMate.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ui:ThemesDictionary Theme="Light" />
                <ui:ControlsDictionary />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

### FluentWindow Example

```xml
<ui:FluentWindow x:Class="ClipMate.App.MainWindow"
                 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
                 Title="ClipMate - Clipboard Manager"
                 Height="700" Width="1200"
                 MinHeight="500" MinWidth="800">
    <!-- Content -->
</ui:FluentWindow>
```

### WPF UI NotifyIcon Example

```xml
<ui:NotifyIcon 
    TooltipText="ClipMate"
    Icon="pack://application:,,,/Assets/icon.ico"
    MenuOnRightClick="True">
    <ui:NotifyIcon.Menu>
        <ContextMenu>
            <MenuItem Header="Show ClipMate" Click="ShowWindow_Click">
                <MenuItem.Icon>
                    <ui:SymbolIcon Symbol="Window24" />
                </MenuItem.Icon>
            </MenuItem>
            <Separator />
            <MenuItem Header="Quick Access" Click="QuickAccess_Click">
                <MenuItem.Icon>
                    <ui:SymbolIcon Symbol="ClipboardPaste24" />
                </MenuItem.Icon>
            </MenuItem>
            <Separator />
            <MenuItem Header="Exit" Click="Exit_Click">
                <MenuItem.Icon>
                    <ui:SymbolIcon Symbol="DismissCircle24" />
                </MenuItem.Icon>
            </MenuItem>
        </ContextMenu>
    </ui:NotifyIcon.Menu>
</ui:NotifyIcon>
```

## Breaking Changes

### Removed

1. **All custom compact styles** from `App.xaml`
2. **System.Windows.Forms** dependency (NotifyIcon)
3. **Custom DPI helper** code
4. **PaneHeaderStyle, PaneBorderStyle** - Use WPF UI defaults

### Changed

1. **Window type**: `Window` → `ui:FluentWindow`
2. **Button controls**: Standard `Button` → `ui:Button` (optional, works with both)
3. **Icons**: Text labels → `ui:SymbolIcon` with 2000+ built-in icons
4. **Tray implementation**: WinForms NotifyIcon → WPF UI NotifyIcon

### Added

1. **Wpf.Ui NuGet package**
2. **Theme support**: Light/Dark/HighContrast themes available
3. **Modern controls**: Access to CardControl, InfoBar, etc. for future use

## Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| WPF UI has bugs or limitations | Library is mature (v4.0.3), widely used (2.5k projects), active maintenance |
| Performance impact | WPF UI is optimized for WPF, likely similar or better than custom code |
| Learning curve | Excellent documentation at wpfui.lepo.co, many examples |
| Breaking changes in future | MIT license allows forking if needed, library follows semantic versioning |
| Visual appearance changes | Can customize WPF UI styles if needed, but defaults are excellent |

## Success Criteria

✅ Application compiles and runs without custom styles  
✅ All windows and dialogs use FluentWindow or ContentDialog  
✅ Tray icon works on all DPI settings without custom code  
✅ System.Windows.Forms dependency removed  
✅ Modern Windows 11 Fluent Design appearance  
✅ No visual regressions (all UI elements visible and functional)  
✅ Performance is equal or better than before  
✅ Code is cleaner and more maintainable  

## Future Enhancements (Post-Migration)

1. **Dark Theme**: Implement theme switching (WPF UI makes this trivial)
2. **Navigation**: Use WPF UI NavigationView if we add more top-level sections
3. **Modern Controls**: 
   - Use `ui:InfoBar` for notifications instead of MessageBox
   - Use `ui:CardControl` for grouped content
   - Use `ui:NumberBox` for numeric inputs
4. **Mica Effect**: Enable window backdrop effects on Windows 11
5. **Settings UI**: Use WPF UI controls for settings/preferences dialog

## References

- **WPF UI GitHub**: https://github.com/lepoco/wpfui
- **Documentation**: https://wpfui.lepo.co/
- **NuGet Package**: https://www.nuget.org/packages/WPF-UI/
- **Gallery App**: Available in Microsoft Store (search "WPF UI")
- **Discord Community**: https://discord.gg/AR9ywDUwGq

## Notes

- WPF UI is inspired by Windows 11 design but works on Windows 10
- Segoe Fluent Icons font is included in Windows 11, need to bundle for Windows 10
- Library handles DPI scaling automatically
- Can mix standard WPF controls with WPF UI controls during migration
- FluentWindow provides title bar customization and Windows 11 snap layouts
