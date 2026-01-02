---
sidebar_position: 11
title: Advanced Options
---

# Advanced Options

These are power-user settings that control timing, behavior, and troubleshooting options.

## Timing Settings

### Startup Delay

Pause for a set number of seconds before initializing. This prevents conflicts with other applications during system startup, particularly when multiple applications try to insert into the clipboard chain simultaneously.

### Capture Delay

Pause after you copy data to let the source application finish. Default is 250ms (¼ second). Increase if you see "Can't open Clipboard" errors with:
- Large, complex applications
- Slow computers
- Very large data types

### Settle Time Between Captures

Minimum wait time after capturing before accepting new items. Decrease if you're missing copies during rapid succession (macros, automation). Be careful—too short may conflict with applications that don't release the clipboard quickly.

### PowerPaste Delay

Time between pasting an item and loading the next. Increase if PowerPaste has trouble, giving the target application more time to process.

## Behavior Settings

### Alt Key Required for Collection Drag/Drop

When enabled (default), you must press **Alt** to move collections via drag/drop, preventing accidental reorganization.

### Pay Attention To Clipboard Ignore Flag

Respects requests from clipboard-aware programs that want ClipMate to ignore their updates. Turn off to solve certain compatibility issues.

### Enable Auto Capture At Startup

Controls whether ClipMate captures clipboard data when it starts. Turn off to start with capture disabled.

### Enable Cached Database Writes

For performance, ClipMate caches database updates. Turning off guarantees immediate disk writes but causes "disk chatter" and slight delays. Only disable if experiencing frequent crashes with data loss.

## Display Settings

### Use Internet Explorer To Display HTML

ClipMate uses IE to render HTML internally. Disable if:
- You don't want HTML display
- Your internet connection won't disconnect after closing IE

Requires restart.

### Collection Menu Style

Controls how collection menus behave when they exceed screen height:
- **Scroll** - Scroll up/down
- **Multiple Columns** - Break into columns

## Reset Options

### Reset All Settings To Defaults

Resets all settings to factory defaults. Requires restart.

:::caution License Warning
Resetting will "un-register" your copy. You'll need to re-enter your license key after restart.
:::

**Alternative:** Press **Ctrl+Shift** while ClipMate loads for the same option.

### Clear Application Profile

Completely wipes the Application Profile if it becomes corrupted. Requires restart.

## See Also

- [General Options](general.md)
- [Application Profile](application-profile.md)
