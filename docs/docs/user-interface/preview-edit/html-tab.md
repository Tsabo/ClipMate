---
sidebar_position: 6
title: HTML Tab
---

# HTML Tab

The HTML Tab displays HTML content copied from web pages or raw HTML tags copied from an editor. Data copied from web browsers appears in ClipMate looking very similar to the original, complete with images.

## HTML Format

The "HTML Format" clipboard type is designed for exchanging portions of HTML pages with all formatting intact (except for actual image data, which is referenced by URL).

When you copy text from a web page, ClipMate captures this HTML Format data and displays it with:
- Original fonts and colors
- Tables and layouts
- Hypertext links (clickable, launching in your default browser)

## Source URL

When an application supports the complete HTML Format specification, it supplies the URL of the page the data was copied from. If the Source URL field shows a valid URL, you can click it to revisit the original page in your browser.

:::note
The page must still exist on the server and you must be connected to the internet.
:::

## Options

Access options from the settings button drop-down menu:

| Option | Description |
|--------|-------------|
| **Use Pictures From Browser Cache** | Attempts to show images from your browser cache |
| **Delete Scripts** | Removes any scripts from the HTML for security |
| **Use All HTML** | Uses complete HTML or truncates to fragment tags |

## About Graphics

When you copy HTML to the clipboard, only the HTML itself is present—images are referenced by `<IMG>` tags, not embedded. When you paste data containing image tags, the receiving application may:
- Re-download images from the original site
- Retrieve images from your browser cache

ClipMate can retrieve images from your browser cache for display.

## Viewing TEXT as HTML

Web developers can view plain text in the HTML tab where it's interpreted as HTML. Text without tags appears as regular text, but any HTML tags are rendered.

When TEXT is displayed this way, the tab caption changes to "TEXT as HTML" to indicate the mode.

## Context Menu

The HTML tab has a context menu provided by the embedded browser. Right-click to access:
- **View Source** - Opens the HTML in a text editor
- **Print** - Prints via the browser engine

## See Also

- [Preview/Edit Window](index.md)
