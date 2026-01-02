---
sidebar_position: 3
title: ClipBar
---

# ClipBar

:::note ClipBar Deprecation
The ClipBar was a feature in ClipMate 7.x that integrated into the Windows taskbar. **This feature has been removed in ClipMate 8** due to changes in how modern Windows versions handle taskbar toolbars.

Windows 10 version 1903 and later, as well as Windows 11, no longer support custom taskbar toolbars. This documentation is preserved for users of legacy ClipMate versions.
:::

## What Was the ClipBar?

The ClipBar integrated directly into the Windows taskbar, showing the current clip and providing up to two rows of toolbar buttons. It gave users quick access to ClipMate functionality without opening a separate window.

{/* TODO: Screenshot - ClipBar in single-row mode (img/clipbar-single.png) */}

{/* TODO: Screenshot - ClipBar with dropped-down clip list (img/clipbar-dropdown.png) */}

## Features

The ClipBar provided:

- **Current Clip Display**: Always visible indicator of what's on the clipboard
- **Drop-Down ClipList**: Click the drop-down arrow to access your clips
- **Customizable Toolbar**: Add frequently-used ClipMate commands
- **QuickPaste Integration**: Double-click a clip to paste it into your target application
- **Drag and Drop**: Drag clips from the ClipBar into OLE-compliant applications

## Modern Alternatives

Since the ClipBar is no longer supported, consider these alternatives:

| Alternative | Description |
|-------------|-------------|
| **System Tray Icon** | Right-click the tray icon for quick access to clips and commands |
| **Hotkeys** | Use **Ctrl+Alt+C** to bring up ClipMate instantly |
| **ClipMate Classic** | A compact window that can be set to always-on-top |
| **QuickPaste Hotkey** | **Ctrl+Shift+Q** brings up Classic in drop-down mode |

## Legacy Installation (ClipMate 7.x Only)

For users still running ClipMate 7.x on compatible Windows versions:

1. The ClipBar component must be installed during ClipMate setup
2. Right-click on the Windows taskbar
3. Select **Toolbars**
4. Select **ClipMate ClipBar**
5. If it appears as a small "bumpy spot," drag it wider

### Keyboard Access

The ClipBar had a system-wide hotkey for quick access, configurable in the Hotkeys options. The default was **Win+V**.

### Configuration

The ClipBar could display one or two toolbar rows depending on taskbar height. To configure:
1. Right-click on any toolbar button
2. Select **Customize** from the popup menu
3. Configure either toolbar in the customization dialog

## Troubleshooting (Legacy)

**ClipBar doesn't appear on the menu**
Windows may not recognize the new toolbar. Try resizing the taskbar or locking/unlocking it.

**ClipBar appears washed out**
With light desktop backgrounds on Windows 7, transparency could cause display issues.

**"ClipBar Dock Panel" appears**
This placeholder appears when ClipMate isn't running or has become disconnected. Run ClipMate and use **Tools → Connect to ClipBar**.

## See Also

- [ClipMate Classic](clipmate-classic.md)
- [Hotkeys](../options/hotkeys.md)
