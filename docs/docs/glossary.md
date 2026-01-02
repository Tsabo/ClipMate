---
sidebar_position: 6
title: Glossary
---

# Glossary

## Application Profile

Configuration settings that determine which clipboard formats to capture from each application. Prevents ClipMate from being overwhelmed by unwanted data formats. See [Application Profile](options/application-profile.md).

## Bitmap

A pixel-based image format. Screenshots and photographs are typically captured as bitmaps. See [Bitmap Tab](user-interface/preview-edit/bitmap-tab.md).

## Clip Item

A piece of data stored in ClipMate. When you copy something to the clipboard, ClipMate creates a new Clip Item containing:
- The data itself
- Timestamp
- Source URL (if from browser)
- Creator application
- Size
- Other metadata

## Clipboard

A global memory area inside Windows for exchanging data between applications. ClipMate constantly monitors the clipboard and captures its contents.

## Clipboard Chain

The mechanism Windows uses to notify applications of clipboard changes. Applications "chain" themselves together, passing notifications along. This chain can break, causing clipboard issues. Modern Windows versions (Vista+) use a newer notification API that eliminates chain problems.

## Collection

A folder within the database where clips are categorized and stored. Default collections:
- **InBox** — Recent captures
- **Overflow** — Clips aged out of InBox
- **Safe** — Permanent storage

See [Data Management](advanced/data-management.md).

## Data Source

A database containing clip data. ClipMate can connect to multiple data sources simultaneously.

## HTML

HyperText Markup Language. When you copy from web browsers, ClipMate captures HTML Format data to preserve formatting. See [HTML Tab](user-interface/preview-edit/html-tab.md).

## Picture (Metafile)

A vector-based image format used for drawings, clip art, and diagrams. Unlike bitmaps, pictures scale without quality loss. See [Picture Tab](user-interface/preview-edit/picture-tab.md).

## QuickPaste

A feature for rapidly pasting clips into applications. Double-click a clip or press Enter to paste into the target application. See [QuickPaste Options](options/quickpaste.md).

## PowerPaste

A feature for pasting multiple clips in sequence. ClipMate automatically loads the next clip after each paste. See [PowerPaste](user-interface/main-toolbar/powerpaste.md).

## Retention Rules

Rules that determine how long clips are kept before automatic deletion. Different collections can have different retention settings.

## Rich Text Format (RTF)

A text format that preserves fonts, colors, and formatting. Word processors typically copy data as RTF. See [Rich Text Tab](user-interface/preview-edit/rich-text-tab.md).

## Target Application

The application where ClipMate will paste data during QuickPaste operations. ClipMate auto-detects the target based on which application was active before you switched to ClipMate.

## Virtual Collection

A dynamic collection that shows clips matching certain criteria (like a saved search). Examples include Trashcan, Today, Past 7 Days, and All Bitmaps. See [Data Management](advanced/data-management.md).
