---
sidebar_position: 2
title: Sorting Clips
---

# Sorting Clips

Click on a column heading to sort the clips according to the data in that column. ClipMate understands alphabetic, numeric, and date sorts. Click again to sort in the opposite direction.

The sort direction and column can be different for each collection, so you can have one collection sorted alphabetically, another sorted with new clips on top, and another with new clips on the bottom.

## The SortKey Column

One of the columns is the mysterious but important **SortKey** column. This is used to sort clips the way YOU want.

For example, if you have clips that you need at the top of the collection, you can re-order clips by:
- Dragging and dropping them on one another
- Using right-click **"Move to top/bottom of sort order"**

After moving clips, none of the usual sort criteria (alphabetic, date, size) apply. That's where SortKey comes in. As you move clips around, the SortKey value changes to reflect its position relative to the two clips it falls between.

**Example:** If you drop a clip between two clips with SortKey values of 100 and 200, the dropped clip will be assigned 150. When you sort by SortKey, clips appear in your custom order.

## SortKey Assignment

New clips get assigned a SortKey value of the Clip ID multiplied by 100. This means:
- The sequence grows automatically
- There's lots of room between clips for insertions
- You won't run out of numbers (wraps around at ~2 billion)

## Tips

:::tip Sorting affects clip placement
Sorting determines where new clips are placed. If you sort on the SortKey, ID, or Date/Time columns, new clips will appear at the top or bottom of the collection depending on sort direction.
:::

:::tip Recommended default sort
For most uses, sorting by the SortKey column in **descending order** (largest on top) is most useful. It keeps the newest clips on top and is sensitive to manual clip movements.
:::

:::tip Manual SortKey adjustment
To manually adjust the SortKey field, right-click on a clip and use the Property dialog. This is handy for setting a SortKey to a very high number so the clip stays at the top of the sort order.
:::

## See Also

- [The ClipList](index.md)
- [Detail View](detail-view.md)
