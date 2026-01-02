---
sidebar_position: 1
title: General Options
---

# General Options

The General tab in the Options dialog controls startup behavior and other general settings.

## Startup Options

### Load at Startup

ClipMate can run with Explorer and Classic loaded in any combination. This is a trade-off between startup time and responsiveness:

| Option | Effect |
|--------|--------|
| **Load Explorer at Startup** | Pre-loads Explorer window in background |
| **Load Classic at Startup** | Pre-loads Classic window in background |

### Initially Show

Determines which window to display when ClipMate first starts:
- **Explorer** - Show ClipMate Explorer
- **Classic** - Show ClipMate Classic
- **Nothing** - Load in background only

If the selected window isn't pre-loaded, it won't load until you activate ClipMate.

### Start with Windows

When enabled, ClipMate loads automatically when Windows boots. This sets an entry in the registry run key:
```
HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
```

:::note ClipBar
There's no startup option for ClipBar. ClipMate automatically loads ClipBar if it detects the ClipBar Dock Site running in the taskbar.
:::

## Other Settings

### Confirm When Deleting Items From "Safe" Collections

Provides extra protection for collections marked as "never delete." You'll receive an "Are you sure?" confirmation dialog before deletion.

### Check for Updates and News

When enabled, ClipMate polls the update server every 5 days. If updates or news are available, a menu item appears to view details and download links.

### Sort Collections Alphabetically

Controls the order of collections in the Collection Tree and menus:
- **Alphabetically** - A-Z ordering
- **By Sort Key** - Custom ordering (move with +/- keys)

## Explorer Layout

| Option | Effect |
|--------|--------|
| **Full Width Editor** | Editor uses full width, shorter Collection Tree |
| **Full Height Collection Tree** | Collection Tree uses full height, narrower Editor |

## See Also

- [Visual Options](visual.md)
- [ClipMate Classic](../user-interface/clipmate-classic.md)
- [ClipMate Explorer](../user-interface/clipmate-explorer.md)
