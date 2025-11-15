# WPF UI Migration - Quick Reference

## Key Changes Summary

### 1. Window → FluentWindow

**Before:**
```xml
<Window x:Class="ClipMate.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        Title="ClipMate">
```

**After:**
```xml
<ui:FluentWindow x:Class="ClipMate.App.MainWindow"
                 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                 xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
                 Title="ClipMate">
```

### 2. App.xaml Theme Setup

**Before:**
```xml
<Application.Resources>
    <!-- 100+ lines of custom compact styles -->
</Application.Resources>
```

**After:**
```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ui:ThemesDictionary Theme="Light" />
            <ui:ControlsDictionary />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

### 3. Buttons with Icons

**Before:**
```xml
<Button Content="Copy" Height="22" Width="70" FontSize="10" Padding="8,2"/>
```

**After:**
```xml
<ui:Button Icon="{ui:SymbolIcon Copy24}">
    Copy
</ui:Button>
```

Or more compact:
```xml
<ui:Button Content="Copy" Icon="{ui:SymbolIcon Copy24}" />
```

### 4. System Tray Icon

**Before (Windows Forms):**
```csharp
using System.Windows.Forms;

private NotifyIcon _notifyIcon;
private ContextMenuStrip _contextMenu;

_notifyIcon = new NotifyIcon
{
    Icon = new System.Drawing.Icon(...),
    Visible = true
};
```

**After (WPF UI):**
```xml
<!-- In XAML or code -->
<ui:NotifyIcon TooltipText="ClipMate"
               Icon="pack://application:,,,/Assets/icon.ico"
               MenuOnRightClick="True">
    <ui:NotifyIcon.Menu>
        <ContextMenu>
            <MenuItem Header="Show ClipMate">
                <MenuItem.Icon>
                    <ui:SymbolIcon Symbol="Window24" />
                </MenuItem.Icon>
            </MenuItem>
        </ContextMenu>
    </ui:NotifyIcon.Menu>
</ui:NotifyIcon>
```

### 5. Menu Items (No Changes Required!)

WPF UI styles standard menus automatically:
```xml
<Menu>
    <MenuItem Header="_File">
        <MenuItem Header="_New Collection..."/>
        <MenuItem Header="_Exit"/>
    </MenuItem>
</Menu>
```

Just remove custom FontSize, Padding attributes.

### 6. DataGrid (Minor Changes)

**Before:**
```xml
<DataGrid FontSize="10" RowHeight="18">
    <DataGrid.ColumnHeaderStyle>
        <Style TargetType="DataGridColumnHeader">
            <Setter Property="FontSize" Value="10"/>
            <!-- ... -->
        </Style>
    </DataGrid.ColumnHeaderStyle>
</DataGrid>
```

**After:**
```xml
<DataGrid IsReadOnly="True">
    <!-- WPF UI applies beautiful styling automatically -->
    <!-- Keep RowHeight if you want specific height -->
</DataGrid>
```

## Common Fluent Icons

Here are commonly needed icons from WPF UI's SymbolIcon:

| Purpose | Symbol |
|---------|--------|
| Add/New | `Add24` |
| Folder | `Folder24` or `FolderOpen24` |
| Copy | `Copy24` |
| Paste | `ClipboardPaste24` |
| Delete | `Delete24` or `Dismiss24` |
| Search | `Search24` |
| Settings | `Settings24` |
| Edit | `Edit24` |
| Save | `Save24` |
| Cancel | `Dismiss24` |
| Window | `Window24` |
| Exit | `DismissCircle24` or `ArrowExit24` |
| Info | `Info24` |
| Warning | `Warning24` |
| Error | `ErrorCircle24` |
| Success | `Checkmark24` |

**Browse all icons:** https://fluent2.microsoft.design/icons or WPF UI Gallery app

## Theme Control

### Setting Theme in App.xaml
```xml
<!-- Light theme (default) -->
<ui:ThemesDictionary Theme="Light" />

<!-- Dark theme -->
<ui:ThemesDictionary Theme="Dark" />

<!-- High contrast -->
<ui:ThemesDictionary Theme="HighContrast" />
```

### Changing Theme at Runtime (Future Enhancement)
```csharp
using Wpf.Ui.Appearance;

// Change to dark theme
ApplicationThemeManager.Apply(ApplicationTheme.Dark);

// Change to light theme  
ApplicationThemeManager.Apply(ApplicationTheme.Light);

// Auto theme (matches Windows)
ApplicationThemeManager.Apply(ApplicationTheme.Auto);
```

## WPF UI Control Equivalents

| Standard WPF | WPF UI Enhanced | Notes |
|-------------|-----------------|-------|
| `Window` | `ui:FluentWindow` | Modern title bar, snap layouts |
| `Button` | `ui:Button` | Can add icons, better styling |
| `TextBox` | Standard works | WPF UI styles it automatically |
| `ComboBox` | Standard works | WPF UI styles it automatically |
| `CheckBox` | Standard works | WPF UI styles it automatically |
| `RadioButton` | Standard works | WPF UI styles it automatically |
| `DataGrid` | Standard works | WPF UI styles it automatically |
| `TreeView` | Standard works | WPF UI styles it automatically |
| `Menu` | Standard works | WPF UI styles it automatically |
| `StatusBar` | Standard works | WPF UI styles it automatically |
| `MessageBox` | `ui:MessageBox` | Better styling (optional) |
| N/A | `ui:CardControl` | Fluent card container |
| N/A | `ui:InfoBar` | Modern notification banner |
| N/A | `ui:NavigationView` | Side navigation (if needed) |

## FluentWindow Features

```xml
<ui:FluentWindow WindowStartupLocation="CenterScreen"
                 ExtendsContentIntoTitleBar="False"
                 WindowBackdropType="Mica"
                 WindowCornerPreference="Round">
```

Properties:
- `ExtendsContentIntoTitleBar` - Use full window for content
- `WindowBackdropType` - `None`, `Mica`, `Acrylic`, `Tabbed`
- `WindowCornerPreference` - `Round`, `RoundSmall`, `DoNotRound`
- Title bar is customizable (add buttons, search, etc.)

## Handling Title Bar Customization

If you want custom title bar content:
```xml
<ui:FluentWindow ExtendsContentIntoTitleBar="True">
    <ui:FluentWindow.TitleBar>
        <ui:TitleBar Title="ClipMate">
            <!-- Custom buttons can go here -->
        </ui:TitleBar>
    </ui:FluentWindow.TitleBar>
    
    <!-- Main content -->
</ui:FluentWindow>
```

## Package Dependencies

After migration, your dependencies will be:

```xml
<ItemGroup Label="UI Frameworks">
  <PackageVersion Include="CommunityToolkit.Mvvm" Version="8.3.2" />
  <PackageVersion Include="Wpf.Ui" Version="4.0.3" />
</ItemGroup>
```

**Removed:**
- System.Windows.Forms (for NotifyIcon)
- System.Drawing.Common (if only used for tray icon)
- Custom DPI helper code

## Testing Checklist

After each phase:

1. ✅ Application builds
2. ✅ Application starts
3. ✅ No runtime exceptions
4. ✅ UI elements visible
5. ✅ Interactions work (clicks, typing, etc.)
6. ✅ Visual appearance acceptable

## Common Issues and Solutions

### Issue: FluentWindow doesn't show title
**Solution:** Set `Title` property on FluentWindow directly

### Issue: Icons not showing
**Solution:** Ensure `ui:SymbolIcon` is inside `Icon` property or `MenuItem.Icon`

### Issue: NotifyIcon not showing in tray
**Solution:** Ensure icon file exists and path is correct (use pack URI)

### Issue: Custom colors not applying
**Solution:** WPF UI uses theme colors. Override specific brushes if needed:
```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ui:ThemesDictionary Theme="Light" />
            <ui:ControlsDictionary />
        </ResourceDictionary.MergedDictionaries>
        
        <!-- Override specific colors -->
        <SolidColorBrush x:Key="AccentBrush" Color="#0078D4"/>
    </ResourceDictionary>
</Application.Resources>
```

### Issue: DataGrid TwoWay binding error
**Solution:** Add `IsReadOnly="True"` to DataGrid (already in our plan)

## Migration Order

1. ✅ **App.xaml** - Add theme dictionaries, remove custom styles
2. ✅ **MainWindow** - Convert to FluentWindow, update controls
3. ✅ **SystemTrayService** - Replace with WPF UI NotifyIcon
4. ✅ **Dialogs** - Convert to FluentWindow
5. ✅ **Views** - Remove custom styling
6. ✅ **Testing** - Comprehensive testing on all DPIs

## Resources

- **Documentation:** https://wpfui.lepo.co/
- **API Reference:** https://wpfui.lepo.co/api/Wpf.Ui.html
- **Sample Apps:** https://github.com/lepoco/wpfui/tree/main/samples
- **Icon Browser:** WPF UI Gallery app (Microsoft Store)
- **Discord:** https://discord.gg/AR9ywDUwGq

## Pro Tips

1. **Start with App.xaml** - Getting themes loaded is 80% of the work
2. **Standard controls work** - Don't feel you must use `ui:Button` everywhere
3. **Icons are optional** - Can add them gradually
4. **Test incrementally** - Build and run after each phase
5. **Keep it simple** - Use WPF UI defaults, customize only if needed
6. **Fluent icons** - Browse the gallery app to find perfect icons
7. **Theme switching** - Easy to add later as enhancement
