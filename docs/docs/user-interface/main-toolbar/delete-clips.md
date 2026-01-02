---
sidebar_position: 3
title: Delete Clips
---

# Delete Clips

Deletes the currently selected clip(s) from the current collection and sends them to the Trashcan. Depending on database settings, the Trashcan retains deleted clips for a configurable period (default: 7 days) before permanently deleting them.

## Usage

1. Select one or more clips in the ClipList
2. Click the **Delete** toolbar button

The clips are moved to the Trashcan, where they can be recovered if needed.

## Deleting All Clips

To delete all clips in a collection:
1. Press **Ctrl+A** to select all items
2. Press **Delete** or click the Delete button

## Alternative Access

| Method | Action |
|--------|--------|
| Keyboard | Press **Delete** when ClipList has focus |
| Menu | **Edit → Delete Item(s)** |
| Right-click | Select **Delete** from context menu |

## Safe Collections Warning

If you attempt to delete clips from a collection marked as "Safe" (never delete), you'll receive a confirmation dialog asking "Are you sure?" This extra protection prevents accidental deletion of important clips.

You can enable or disable this confirmation in **Options → General → Confirm When Deleting Items From "Safe" Collections**.

## Recovery

Deleted clips go to the Trashcan virtual collection, where they remain for the retention period configured in the database properties. To recover a clip:

1. Navigate to the **Trashcan** collection
2. Select the clip(s) to recover
3. Move them to another collection using **Edit → Move to Collection**

## Permanent Deletion

To permanently delete clips before the retention period expires:
1. Navigate to the Trashcan
2. Use **File → Empty Trash** to permanently remove all trashed clips

## See Also

- [Data Management](../../advanced/data-management.md)
- [Database Options](../../options/databases.md)
