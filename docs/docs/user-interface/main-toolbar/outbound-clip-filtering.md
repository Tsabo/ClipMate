---
sidebar_position: 13
title: Outbound Clip Filtering
---

# Outbound Clip Filtering

{/* ![Outbound Filtering Toggle](img/toolbar-outbound-filtering.png) */}

When enabled, this feature prevents non-text formats like RTF and HTML from reaching the clipboard, causing data to be pasted as pure text. Think of it as the Application Profile in reverse—where Application Profiles filter data coming INTO ClipMate, this feature filters data going OUT to the system clipboard.

## Why Use Outbound Filtering?

When you copy text from a web browser or word processor, the clipboard typically contains the same information in multiple formats:
- Plain TEXT
- Rich Text Format (RTF)
- HTML

When you paste, the target application chooses which format to use. Often it picks RTF or HTML, bringing along unwanted formatting (fonts, colors, links, etc.).

With Outbound Filtering enabled, ClipMate strips away everything except plain text, so your paste assumes the formatting of the destination document—as if you had typed it.

## How It Works

### Active Filtering (Default)
The clipboard is updated immediately after any new clip arrives:
1. You copy HTML from Firefox
2. ClipMate captures the clip with all formats (Text, RTF, HTML)
3. Within half a second, ClipMate updates the clipboard with plain text only
4. You hear a "whoosh" sound indicating the filter activated
5. You can now paste as plain text without interacting with ClipMate

### Passive Filtering
The clipboard is updated only when you bring ClipMate to the foreground and then switch away:
1. Copy data as usual
2. Click on ClipMate, then click away
3. As ClipMate loses focus, it updates the clipboard with filtered content

Configure the filtering mode in **Options → Pasting**.

## Graphics

Outbound filtering also works with graphics. If you have a graphic clip with multiple image formats (Bitmap, Metafile, Picture, OLE), filtering forces it to paste as Bitmap only.

If a clip has both TEXT and Bitmap, TEXT takes priority.

## Sound Indicator

When Active Filtering is enabled, you'll typically hear two sounds in quick succession:
1. **Pop** - New data captured
2. **Whoosh** - Outbound filter applied

## Limitations

:::caution
Outbound filtering (especially Active mode) disables the ability to copy/paste files within Windows Explorer. This isn't a bug—it's a side effect of replacing clipboard contents with plain text. The HDROP format that enables file operations gets filtered out.
:::

## Alternative Access

| Method | Action |
|--------|--------|
| Menu | **Edit → Filter Outbound Clips** |
| Hotkey | **Manual Filter** hotkey (configurable) |

## See Also

- [Pasting Options](../../options/pasting.md)
- [Application Profile](../../options/application-profile.md)
