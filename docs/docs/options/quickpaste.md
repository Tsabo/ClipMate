---
sidebar_position: 7
title: QuickPaste Options
---

# QuickPaste

QuickPaste lets you easily paste clips into applications by double-clicking on a clip or selecting it and pressing **ENTER**.

## Universal QuickPaste

In ClipMate 7 and later, QuickPaste is "always on" and available in:
- ClipMate Classic
- ClipMate Explorer
- The ClipBar dropdown

No special hotkey is required to activate QuickPaste mode—it's built into all ClipLists.

## Auto-Targeting

Auto-targeting determines where ClipMate will paste data. When ClipMate comes to the foreground (via hotkey, clicking a window, systray icon, or ClipBar), auto-targeting activates:

1. Identifies the most recently active application
2. Analyzes for reasonability (filtering out taskbar, etc.)
3. Marks it as the "target application"
4. Displays it in the QuickPaste Toolbar

## QuickPaste Toolbar

The QuickPaste Toolbar appears at the bottom of Classic and Explorer, showing:

| Button | Function |
|--------|----------|
| **GoBack** | Toggle whether focus stays with target or returns to ClipMate after paste |
| **Tab** | Send a TAB keystroke to the target application |
| **Enter** | Send an ENTER keystroke to the target application |
| **Target** | Lock/unlock targeting (green check = locked) |

### Target Lock

- **Unlocked (default)** - Auto-targeting re-targets whenever ClipMate gains focus
- **Locked** - Target stays fixed on current application

## Context Menu Access

All QuickPaste commands are available from the ClipList right-click menu. This is useful for:
- Keyboard-centric workflows
- Screen reader users

## Legacy QuickPaste

The legacy hotkey (**Ctrl+Shift+Q**) still works and has an additional effect: it "locks" the target onto the application where you pressed the hotkey.

To return to auto-targeting after using the legacy hotkey:
1. Click the targeting button to unlock
2. Or restart ClipMate

## Fine-Tuning the Target

If QuickPaste locks onto the wrong application or window, you can fine-tune the targeting behavior in the QuickPaste options.

## See Also

- [Pasting Options](pasting.md)
- [Hotkeys](hotkeys.md)
- [PowerPaste](../user-interface/main-toolbar/powerpaste.md)
