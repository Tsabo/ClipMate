---
sidebar_position: 8
title: Database Options
---

# Database Options

{/* ![Database Options Tab](img/options-database.png) */}

ClipMate uses a robust database system to store your clips. By default, every installation creates a database called "My Clips" stored locally in single-user mode.

## Database Definitions

You can define multiple databases for:
- Multi-user sharing on a network
- Secondary data sources on removable drives
- Keeping old databases available for occasional use

### Database List

- **Checkmark** - Determines whether the database loads at startup
- **Add** - Creates a new database entry
- **Edit** - Modify existing database properties
- **Delete** - Remove database entry
- **Open Folder** - Opens database directory in Windows Explorer

:::note Database Files
ClipMate uses .DAT, .IDX, and .BLB files (about 22-24 files total). Files ending in "K" are backups from cleanup/repair operations and can be deleted.
:::

:::caution Directory Rules
- Each database must have its own directory
- Don't put two databases in the same directory
- Don't put a database in a directory used for other files
:::

## Database Maintenance

| Setting | Purpose |
|---------|---------|
| **Internal Backup Interval** | How often to invoke built-in backup |
| **Set Offline Daily** | Close databases for external utilities (backup, antivirus, defrag) |

## Individual Database Properties

### Basic Settings

| Property | Description |
|----------|-------------|
| **Title** | Shows at top level of collection tree |
| **Directory** | Path to database (local or mapped network drive) |
| **Database ID** | Permanent identifier stored inside the database file (read-only). It stays the same if you rename the database or copy its file. A database created by an earlier version gets one the next time it's opened. |
| **Auto Load at Startup** | Whether to load when ClipMate starts |
| **Multi-User** | Enable if others update this database simultaneously |
| **Read-Only** | Prevents writing (must have at least one read/write database) |
| **Share with Peers** | Lets your paired Devices browse this database, read-only, while networking is on (see [Devices](devices.md)). Off by default. A database whose file was copied from another shared database has the same Database ID; click **Give this Database a new ID** to share both. |

### Maintenance Settings

| Property | Description |
|----------|-------------|
| **Trashcan Retention Days** | 7-30 days recommended |
| **Purging and Aging** | When to run overflow/trashcan processing |
| **Temp File Location** | Where database temp files are stored |

### Temp File Location Options

1. **Database Directory** - Best option (don't use for slow devices or network)
2. **System TMP** - Good performance but may conflict with cleanup utilities
3. **Program Directory** - Use if other options don't work

## Database Backup

ClipMate includes ZIP-based backup creating files like:
```
ClipMate7_DB_My Clips_2024-01-15.ZIP
```

### Backup Schedule

- Default: Prompts every 7 days at startup
- Change interval in Database options
- Set to 0 or 9999 days to disable prompts

### Unattended Backup

Enable "auto-OK" option with a 2-3 second countdown to run automatically while still allowing intervention.

### Where to Backup

:::tip Backup Strategy
Back up to a different hard disk if possible. Transfer backup files to CD, USB drive, or cloud storage periodically.
:::

### Multi-User Backups

- Don't backup databases being actively written to by multiple users
- Designate ONE user to run backups
- Turn off backup prompts for other users

## Database Restore

Restore from backup with **File** > **Database Maintenance** > **Restore Database**.

### Before Restoring

1. Select a collection in the target database to make it "active"
2. Consider backing up current database first
3. For preserving existing data, create a new database and restore there

## Importing from ClipMate 7

If you used the original ClipMate 7.x, you can bring its clips across. Click **Import ClipMate 7...** under the database list.

1. **Folder** - ClipMate looks in the default location (`%APPDATA%\Thornsoft Development\ClipMate7`) automatically. Use **Browse...** if your ClipMate 7 database is somewhere else. ClipMate shows how many clips, collections and shortcuts it found.
2. **Import Into** - Choose one:
   - **A new database** - Creates a separate database with the default collections and imports into it. It is added to the database list when you click **OK** in Options.
   - **Merge into an existing database** - Adds the clips to one of your current databases.
3. Click **Go**, then **Done** when the import finishes.

What gets imported:

- Clips with their text, rich text, HTML, images and file lists, plus titles, capture dates, source URLs and macro settings
- Collections and folders. Collections that already exist (such as InBox, Safe and Overflow) are reused rather than duplicated
- Shortcuts (nicknames)

What is skipped:

- **Encrypted clips** - ClipMate 7's encryption can't be read. Decrypt these clips in ClipMate 7 before you import.
- **Virtual collections** - ClipMate has its own versions of these.
- **Clips already present** - Clips are matched by their ClipMate 7 identity, so you can safely run the import again.

:::tip
You can run the import while ClipMate 7 is still open. For a consistent copy, though, close ClipMate 7 first. The import is all-or-nothing: if it fails or you click **Abort**, nothing is changed.
:::

## See Also

- [Data Management](../advanced/data-management.md)
