---
sidebar_position: 5
title: Append
---

# Append

Append combines multiple text clips into one larger clip. This is useful for assembling data from multiple sources into a single unit.

## Two Ways to Append

### 1. Multi-Select Append

Select multiple clips in the ClipList and append them together:

1. Select multiple clips using **Ctrl+Click** or **Shift+Click**
2. Click the **Append** button or use **Edit → Append**
3. The clips are combined into one clip

Items are appended top-down or bottom-up depending on the direction you selected them. The general direction is preserved, though specific ordering may vary.

### 2. Auto-Append Mode

Turn on Append mode to automatically combine all newly copied data:

1. Click the **Append** button without multiple clips selected
2. Append mode activates (like a growing snowball)
3. Each new copy operation adds to the growing clip
4. Click the button again to turn off Auto-Append

When capturing in Auto-Append mode, you'll hear a different sound to indicate data is being appended rather than captured as a new clip.

## Append Rules

Configure how clips are joined in **Options → Capturing → Appending**:

| Setting | Description |
|---------|-------------|
| Separator | Characters inserted between clips (default: line break) |
| Strip Trailing Line Feed | Remove extra line breaks from the end of clips |

Use `\t` for tab and `\n` for line break in the separator field.

## Limitations

- **Text Only**: Append works only with text clips
- Other formats like Bitmap, Picture, Rich Text Format, and HTML cannot be appended

## Alternative Access

| Method | Action |
|--------|--------|
| Menu | **Edit → Append** |
| Keyboard | Configurable hotkey |

:::note
This feature was previously called "Glue" in earlier ClipMate versions.
:::

## See Also

- [Capturing Options](../../options/capturing.md)
- [Tutorial: Lesson 3 - Append](../../tutorial/lesson-three-append.md)
