---
sidebar_position: 4
title: Print
---

# Print

Prints the currently selected clip(s). ClipMate can print text clips and bitmap images, with various formatting options.

## Usage

1. Select one or more clips in the ClipList
2. Click the **Print** toolbar button
3. Confirm printer settings in the print dialog (unless QuickPrint is enabled)

## Alternative Access

| Method | Action |
|--------|--------|
| Menu | **File → Print** |
| Keyboard | **Ctrl+P** |

## Print Options

Configure printing behavior in **Options → Printing**:

- **Printer Selection**: Choose default printer or use system default
- **Print Header**: Add "ClipMate Report" or custom text at page top
- **Print Details**: Include clip title, date/time, source URL, and creator
- **Print Footer**: Page numbers, date/time, and custom text
- **QuickPrint**: Skip the print dialog for faster printing

## Report Selection

ClipMate uses different report layouts for different content types:

| Content Type | Report Style |
|--------------|--------------|
| Text clips | Multiple clips per page, large clips span pages |
| Small bitmaps | 4-6 images per page |
| Large bitmaps | One image per page (full-page layout) |

The large/small bitmap threshold is configurable (default: 640 pixels).

## Automatic Screenshot Printing

You can configure ClipMate to automatically print screen captures, similar to the DOS-era PrintScreen behavior. Enable this in **Options → Printing → Automatically print screen shots**.

## HTML Printing

HTML clips have special printing support through Internet Explorer's native print engine:
1. View the HTML clip in the editor
2. Right-click within the HTML preview
3. Select **Print** from the context menu
