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

### ALT Key Required for Collection Tree DragDrop

When enabled (default), you must press **Alt** to move collections via drag/drop, preventing accidental reorganization.

### Pay Attention To Clipboard Viewer Ignore Flag

Respects requests from clipboard-aware programs that want ClipMate to ignore their updates. Turn off to solve certain compatibility issues.

### Enable Cached Database Writes

For performance, ClipMate caches database updates. Turning off guarantees immediate disk writes but causes "disk chatter" and slight delays. Only disable if experiencing frequent crashes with data loss.

### Buttons for Move/Copy to Collection Should Re-Use the Last Selected Target

When enabled, the Move and Copy buttons default to the last collection you selected.

## Reset/Clear Settings

The tab has three buttons: **Layout/Font**, **Application Profile** and **Everything**.

:::caution Not working yet
In this version these buttons don't change anything, even though a message says they did.
:::

## See Also

- [General Options](general.md)
- [Application Profile](application-profile.md)
