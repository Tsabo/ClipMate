---
sidebar_position: 9
title: Search
---

# 🔍 Search

{/* ![Search Button](img/toolbar-search.png) */}

Invokes ClipMate's powerful search facility to find clips based on text content, metadata, date ranges, and more.

## Usage

Click the **Search** button to open the Search dialog.

## Alternative Access

| Method | Action |
|--------|--------|
| Menu | **View → Find** |
| Keyboard | **Ctrl+F** |

## Search Capabilities

ClipMate's SQL-based search engine can find clips based on:

- Text appearing in the title, body, creator name, or source URL
- Data format (text, HTML, Bitmap, etc.)
- Date of capture
- Encryption status
- Collection membership

You can save queries for reuse, and advanced users can write custom SQL queries.

## What Gets Searched

**Search in** at the top of the dialog shows the database being searched: the one selected in the collection tree. The scope list next to it offers **All collections**, the selected **Collection** or the selected **Folder**, and starts with the narrowest one available.

You can also search a database shared by one of your paired Devices; see [Searching a Remote Database](../../options/devices.md#searching-a-remote-database).

## Quick Search Tips

| Search Type | Syntax | Example |
|-------------|--------|---------|
| Whole keywords | Separate by commas | `apple, banana` |
| Wildcard at end | Use `*` | `POWER*` finds Power, PowerPoint, Powerful |
| Contains anywhere | Use `%` | `%winter%` finds anything with "winter" |
