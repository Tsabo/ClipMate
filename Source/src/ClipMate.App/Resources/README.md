# ClipMate Resources

## Icons Needed

### System Tray Icon (`clipmate-tray.ico`)
- **Size**: 16x16, 24x24, 32x32, 48x48 (multi-resolution .ico file)
- **Style**: Simple clipboard icon, modern flat design
- **Colors**: Blue/purple theme to match branding
- **Purpose**: Shows in Windows system tray (notification area)
- **Current**: Using `SystemIcons.Application` placeholder

### Application Icon (`clipmate.ico`)
- **Size**: 16x16, 24x24, 32x32, 48x48, 256x256 (multi-resolution .ico file)
- **Style**: Full-color clipboard icon with branding
- **Purpose**: Shows in taskbar, Alt+Tab, window title bar
- **Current**: Not set (using default)

## Toolbar Icons

The toolbar uses **Segoe MDL2 Assets** font for icons:
- New Collection: `&#xE710;` (Page)
- New Folder: `&#xE8B7;` (NewFolder)
- Copy: `&#xE8C8;` (Copy)
- Paste: `&#xE77F;` (Paste)
- Delete: `&#xE74D;` (Delete)
- Search: `&#xE721;` (Find)

## Creating Icons

### Option 1: Use a designer
- Hire designer on Fiverr/Upwork
- Provide ClipMate brand guidelines
- Request multi-resolution .ico files

### Option 2: Generate from SVG
1. Create SVG icon (e.g., in Inkscape, Figma)
2. Use tool to convert to .ico:
   - https://convertio.co/svg-ico/
   - https://icoconvert.com/
3. Ensure multiple resolutions included

### Option 3: Use existing icon libraries
- https://icons8.com/ (search "clipboard")
- https://www.flaticon.com/ (search "clipboard manager")
- Ensure proper licensing for commercial use

## Embedding Icons in Application

Once we have the icon files:

1. Add to project:
   ```xml
   <ItemGroup>
     <Resource Include="Resources\clipmate-tray.ico" />
     <Resource Include="Resources\clipmate.ico" />
   </ItemGroup>
   ```

2. Set application icon in `.csproj`:
   ```xml
   <PropertyGroup>
     <ApplicationIcon>Resources\clipmate.ico</ApplicationIcon>
   </PropertyGroup>
   ```

3. Load tray icon in SystemTrayService.cs:
   ```csharp
   var iconStream = Application.GetResourceStream(
       new Uri("pack://application:,,,/Resources/clipmate-tray.ico"));
   _notifyIcon.Icon = new Icon(iconStream.Stream);
   ```

## Temporary Solution

Currently using `SystemIcons.Application` which shows the generic Windows application icon. This is acceptable for development/testing but should be replaced before production release.
