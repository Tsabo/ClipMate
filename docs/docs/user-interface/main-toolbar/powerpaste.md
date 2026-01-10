---
sidebar_position: 2
title: PowerPaste
---

# <span class="clipmate-emoji">&#xE008;</span> PowerPaste Button

The PowerPaste button activates ClipMate's powerful batch pasting feature. Once activated, PowerPaste automatically advances to the next clip each time you paste, allowing you to rapidly paste a series of items.

{/* ![PowerPaste Button States](img/toolbar-powerpaste.png) */}

## Button States

The PowerPaste button has visual indicators showing its state:

| State | Appearance | Meaning |
|-------|------------|---------|
| <span class="clipmate-emoji">&#xE008;</span> Off | No arrow | PowerPaste is inactive |
| <span class="clipmate-emoji">&#xE00A;</span> Up | Arrow pointing up | Will paste upward through the ClipList |
| <span class="clipmate-emoji">&#xE009;</span> Down | Arrow pointing down | Will paste downward through the ClipList |

## Usage

1. Select the starting clip in your series
2. Click the PowerPaste button once to activate (arrow appears)
3. If the arrow points the wrong direction, click again to flip it
4. Switch to your target application
5. Paste repeatedly—ClipMate advances after each paste
6. Click the button again to turn off PowerPaste

## Arrow Behavior

- First click shows the arrow in the direction you last used
- If you paste and click the button, PowerPaste turns off
- If you click without pasting, the arrow flips direction
- The direction is remembered between sessions

## Alternative Access

- **Edit Menu**: PowerPaste Up/Down
- **System Tray Menu**: PowerPaste options
- **Keyboard**: Use configurable hotkeys

## Looping PowerPaste

When you enable the **Loop PowerPaste** option, PowerPaste will automatically restart from the beginning when it reaches the end of the collection (or extended selection). This allows you to paste the same series of data repeatedly.

### How It Works

1. Turn on Loop PowerPaste from the Edit menu or toolbar toggle
2. Start PowerPaste as normal
3. When PowerPaste reaches the end, it automatically loops back to the start
4. You'll hear the "PowerPaste complete" sound play twice to indicate the loop
5. Click the PowerPaste button to turn it off when finished

### Use Cases

- Pasting repetitive data patterns
- Filling forms that repeat the same field structure
- Testing applications with the same data set multiple times

## Exploding PowerPaste

**Exploding PowerPaste** allows you to paste individual fragments from a single clip, breaking up delimited data into separate fields. This is perfect for working with comma-delimited data from spreadsheets or structured text.

### Example

If you have data like this in one clip:
```
January, 31
February, 28
March, 31
```

With Exploding PowerPaste enabled, each paste will give you the next fragment:
1. January
2. 31
3. February
4. 28
5. March
6. 31

### How to Use

1. Copy your delimited data as one clip
2. Turn on **Explode Into Fragments** from the Edit menu or toolbar toggle
3. Activate PowerPaste
4. Each paste operation pastes the next fragment
5. Commas are removed and spaces are trimmed automatically

### Configuration

Configure fragment delimiters in [Pasting Options](../../options/pasting.md):
- Default delimiters: `,` `.` `;` `:` `\t` (tab) `\n` (line break)
- Option to strip delimiters
- Option to remove spaces and control characters

### Real-World Example

Pasting spreadsheet data into a database form:
```
John Doe, 123 Main Street, Anytown, NY, 12345, USA
```

With Exploding PowerPaste, you can paste each field separately:
- John Doe → Name field
- 123 Main Street → Address field  
- Anytown → City field
- NY → State field
- 12345 → ZIP field
- USA → Country field

## See Also

- [Tutorial: Lesson 2 - PowerPaste](../../tutorial/lesson-two-powerpaste.md)
- [Pasting Options](../../options/pasting.md)
