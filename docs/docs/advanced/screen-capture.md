---
sidebar_position: 3
title: Screen Capture
---

# Screen Capture

ClipMate handles many screen capture tasks, including:
- Individual windows
- Entire desktop
- Rubber-band selection areas
- Object capture
- Multi-monitor support

## Windows PrintScreen Key

| Key | Captures |
|-----|----------|
| **PrintScreen** | Entire desktop (all monitors) |
| **Alt+PrintScreen** | Current window only |

ClipMate automatically captures PrintScreen images.

## ClipMate Screen Capture Features

Found under **Edit** > **Screen Capture**, these features can also be added to toolbars including the ClipBar.

### Area Capture

Draw a rubber-band selection to capture a specific screen region.

- **Menu:** Edit > Screen Capture > Area Capture
- **Default Hotkey:** Alt+Ctrl+F12

:::note Multi-Monitor
Area Capture only works on the "active screen." Move ClipMate to the target screen first—it will hide itself to get out of the way.
:::

### Object Capture

Click on any screen object to capture just that object. Examples:
- Click browser content → captures content (not toolbar, menu, title bar)
- Click toolbar → captures just the toolbar

This is useful when you want clean captures without extra cleanup.

- **Default Hotkey:** Alt+Ctrl+F11
- **Menu:** Edit > Screen Capture > Object Capture

:::note Multi-Monitor
Same behavior as Area Capture—works on the active screen.
:::

### Monitor Capture

Capture a specific monitor instead of the entire desktop.

- **Screen Capture Monitor 1** - First monitor
- **Screen Capture Monitor 2** - Second monitor
- Supports up to 8 monitors (add buttons via toolbar customization)

### Desktop Capture

Captures the entire desktop—same result as PrintScreen. On multi-monitor systems, stretches across all screens.

### Capture Current Screen

Captures the monitor where ClipMate is currently displayed. Only useful with multiple monitors.

### Capture Screen With Cursor

Captures the current screen with the cursor visible. Useful for:
- Demonstrations
- Training materials
- User manuals

:::note
Uses a stock cursor overlaid at the current position, so it may not exactly match your actual cursor.
:::

## Printing Screenshots

ClipMate can print screenshots, including automatically printing new captures.

## See Also

- [Hotkeys](../options/hotkeys.md)
- [Bitmap Tab](../user-interface/preview-edit/bitmap-tab.md)
