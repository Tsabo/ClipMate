---
sidebar_position: 11
title: Advanced Options
---

# Advanced Options

{/* ![Advanced Options Tab](img/options-advanced.png) */}

These are power-user settings that control timing, behavior, and troubleshooting options.

## Timing Settings

### Startup Delay

Pause for a set number of seconds before initializing. This prevents conflicts with other applications during system startup, particularly when multiple applications try to insert into the clipboard chain simultaneously.

### Capture Delay

Pause after you copy data to let the source application finish. Each further change to the clipboard restarts the pause, so a program that adds its data in steps is captured once. Default is 250ms (¼ second); 0 captures at once. Increase if you see "Can't open Clipboard" errors with:
- Large, complex applications
- Slow computers
- Very large data types

### Settle Time Between Captures

Minimum wait time after capturing before accepting new items: a change that would be captured sooner is dropped. Default is 100ms. Decrease if you're missing copies during rapid succession (macros, automation). Be careful—too short may conflict with applications that don't release the clipboard quickly.

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

## Reset/Clear Settings

The tab has three buttons: **Layout/Font**, **Application Profile** and **Everything**. Each one asks you to confirm first, and none of them can be undone. Closing Options with **Cancel** doesn't undo them either.

### Layout/Font

Puts the ClipMate Classic window back to its default position and size, unpins it if it was tacked, and sets the editor font and font size back to their defaults. This happens the next time ClipMate starts, so restart ClipMate after using it. Other settings don't change, including the Explorer layout on the [General](general.md) tab.

### Application Profile

Clears every entry in the [Application Profile](application-profile.md), if it becomes corrupted or you want to start over. This happens at once, with no restart. ClipMate builds new entries as you copy from each program.

### Everything

Sets all settings back to their factory defaults. Use it if your settings have gone badly wrong.

- The Application Profile is cleared and **Start with Windows** is turned off at once.
- Everything else is reset the next time ClipMate starts, so restart ClipMate after using it. Any settings you change before restarting are reset too.

These are kept:

- Your Databases, their clips and each Database's own settings
- Saved searches
- Paired Devices. **Enable networking** on the Devices tab is turned off, so turn it back on to connect to them again.

## See Also

- [General Options](general.md)
- [Application Profile](application-profile.md)
