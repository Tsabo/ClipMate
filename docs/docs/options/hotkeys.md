---
sidebar_position: 6
title: Hotkeys
---

# Hotkeys

Global hotkeys activate ClipMate functions even when working in another application. Since only one application can use a particular hotkey sequence, conflicts may arise. For this reason, hotkeys are customizable.

## Configuring Hotkeys

1. Click in the desired hotkey control
2. Type the hotkey sequence
3. Use right-click menu to insert **Alt+**, **Ctrl+**, **Shift+**, or **Win+**
4. **Test** the hotkey to verify Windows can reserve it

Testing checks for conflicts with other running applications and validates the key combination.

## Valid Keys

You can use:
- Letters: `a-z`, `A-Z`
- Numbers: `0-9`
- Function keys: `F1-F12`
- Navigation: `Insert`, `Up`, `Down`, `Left`, `Right`, `PgUp`, `PgDn`, `End`, `Home`

Combine with modifier keys: **Ctrl**, **Alt**, **Shift**, **Win** (Windows key)

Separate with plus (+): `Ctrl+Shift+Q`, `Ctrl+Alt+C`, `Win+V`, `Ctrl+F12`, `Win+PgUp`

## Invalid Combinations

:::caution Avoid these
Some combinations are unwise:
- **Shift+A** - Makes it impossible to type capital A
- **Ctrl+X, Ctrl+C, Ctrl+V, Shift+Ins** - Standard cut/copy/paste; would break QuickPaste
:::

## Available Hotkey Commands

| Command | Function |
|---------|----------|
| **Show ClipMate Window** | Bring Classic or Explorer to foreground (like clicking tray icon) |
| **Scroll To Next/Previous Item** | Navigate clips in current collection |
| **Activate QuickPaste** | Open ClipMate in QuickPaste mode |
| **Region Screen Capture** | Capture a selected screen region |
| **Object Screen Capture** | Capture a window or control |
| **View Clip In Floating Window** | Open floating view/edit window |
| **Pop-Up ClipBar List** | Show ClipBar dropdown (keyboard access to ClipList) |
| **Toggle Auto-Capture** | Suspend/resume automatic clipboard capture |
| **Manual Capture** | Capture current clipboard (useful when auto-capture is off) |
| **Manual Filter** | Filter clipboard immediately, keeping only plain text or bitmap |

## Tip: Floating Window

Use **ESC** to quickly dismiss a floating view/edit window when done.

## Individual Clip Shortcuts

ClipMate doesn't support hotkeys for individual clips. Instead, use the **Shortcut** facility to assign key sequences (like `.cc.v.n`) to clips, accessible within QuickPaste.

## See Also

- [QuickPaste Options](quickpaste.md)
- [Screen Capture](../advanced/screen-capture.md)
