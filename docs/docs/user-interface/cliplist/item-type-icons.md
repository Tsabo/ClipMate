---
sidebar_position: 5
title: Item Type Icons
---

# Item Type Icons

The first column in the ClipList displays icons that tell you which formats are present in each clip.

## Icon Reference

| Icon | Format | Description |
|------|--------|-------------|
| 🖼️ Bitmap | **Bitmap** | Bitmap image data (BMP, screenshot pixels) |
| 🎨 Picture | **Picture** | Picture (Metafile) image - vector graphics |
| 📝 Text | **Text** | Plain text with no formatting |
| 📄 RTF | **Rich Text** | Rich Text Format - contains font, alignment, color, etc. |
| 🌐 HTML | **HTML** | HTML Format - web content with markup |
| 📁 HDROP | **HDROP** | File list copied from Windows Explorer |

## Multiple Formats

A single clip can contain multiple formats. For example, when you copy text from a word processor, the clip may contain:
- Plain text
- Rich Text Format (RTF)
- HTML

ClipMate captures all available formats and displays the primary icon based on the richest format available.

## HDROP (File Lists)

When you copy files from Windows Explorer, ClipMate captures them as HDROP format. This contains a list of file paths.

You can convert an HDROP clip to plain text:
- **Edit** > **Convert File Pointer To Text**

There's also an option to do this automatically. See [Capturing Options](../../options/capturing.md).

## See Also

- [The ClipList](index.md)
- [Capturing Options](../../options/capturing.md)
