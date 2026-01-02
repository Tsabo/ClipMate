---
sidebar_position: 4
title: Data Management
---

# Data Management

ClipMate allows you to organize clips into collections for long-term storage and categorization. Whether you need ten clips or ten thousand, understanding collections is key.

## Default Collections

ClipMate creates three collections by default:

| Collection | Purpose | Default Behavior |
|------------|---------|------------------|
| **InBox** | Recent clips | Holds 200 most recent; older clips go to Overflow |
| **Overflow** | Trimmed clips | Holds 800 clips trimmed from InBox |
| **Safe** | Important clips | Never automatically deleted |

## Virtual Collections

Virtual collections are specialized searches, not actual storage:

- **Trashcan** - Shows all clips marked for deletion
- **Today** - Clips captured today
- **Past 7 Days** - Clips from the last week
- **Past 30 Days** - Clips from the last month
- **All Bitmaps** - All image clips

### About the Trashcan

The trashcan shows clips marked for future deletion. Each database has a retention rule (default: 7 days) before permanent deletion.

:::tip Rescue Clips
You can recover clips from the trashcan before they're permanently deleted.
:::

## The Flow of a Clip

Typical clip journey:

```
1. Captured into InBox
         ↓ (200 clips later)
2. Moved to Overflow
         ↓ (800 clips later)
3. Moved to Trashcan
         ↓ (7 days later)
4. Permanently deleted
```

You can intervene at any point to move clips to **Safe** or custom collections.

## Managing the Trashcan

To empty the trashcan immediately: **File** > **Empty Trash**

:::note
This is optional—ClipMate's default handling means you rarely need to empty trash manually.
:::

## Capturing to Specific Collections

You can capture directly into any collection:
1. Make the collection active
2. Enable "accept new clips" on the collection
3. New clips go directly to that collection

### Garbage Avoidance

If a collection has "garbage avoidance" enabled, unwanted captures "bounce" to the InBox instead of cluttering your working collection.

## Maintenance Scheduling

Overflow and Empty Trash processes can run:
- At startup
- At shutdown
- After an hour of inactivity

Configure in [Database Properties](../options/databases.md).

## See Also

- [Database Options](../options/databases.md)
