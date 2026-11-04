---
title: ClipMate 7 Import
---

# ClipMate 7 Import

If you used ClipMate 7, you can bring your clips across. ClipMate 7 Import reads your ClipMate 7 database and leaves it unchanged.

{/* ![The Import ClipMate 7 Database dialog](img/import-clipmate-7-database.png) */}

## Run ClipMate 7 Import

For the most consistent copy, close ClipMate 7 first.

1. In ClipMate, open **Tools > Options...**, choose the **Database** tab, and click **Import ClipMate 7...**.
2. Under **Folder**, check the location. ClipMate looks in `%APPDATA%\Thornsoft Development\ClipMate7` by itself. If your database is somewhere else, click **Browse...**. ClipMate shows how many clips, collections and shortcuts it found.
3. If the database has encrypted clips, ClipMate shows how many. Enter each key you used in ClipMate 7, and click **Add Key** after each one.
4. Under **Import Into**, choose one:
   - **A new database** keeps these clips separate. It joins your database list when you click **OK** in Options.
   - **Merge into an existing database** adds the clips to one of your current databases.
5. Click **Go**, then **Done** when it finishes.

If ClipMate 7 Import fails or you click **Abort**, nothing changes. You can also safely run it again, because clips it already brought across are skipped.

## What comes across

- Clips with their text, rich text, HTML, images and file lists, along with titles, capture dates, source URLs and macro settings
- Collections and folders. A collection that already exists, such as Safe or Overflow, is reused
- Shortcuts
- Encrypted clips that one of your keys opens. They stay encrypted, and the same key opens them

## What stays behind

- Encrypted clips that none of your keys opens
- Virtual collections, since ClipMate has its own

## See also

- [What's new](whats-new.md)
- [What's different or gone](whats-different.md)
- [Database Options](../options/databases.md)
