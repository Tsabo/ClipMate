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
3. If the arrow points the wrong direction, click the button twice: the first click turns PowerPaste off and the second turns it on going the other way
4. Switch to your target application
5. Paste repeatedly—ClipMate advances after each paste
6. Click the button again to turn off PowerPaste

## Which Clips It Pastes

- With one clip selected, PowerPaste runs from that clip to the end of the collection: to the top going up, to the bottom going down.
- With several clips selected, PowerPaste pastes only those clips.

While PowerPaste is on, clicking a clip in the series makes it the next one pasted. Clicking any other clip takes PowerPaste back to the start of the series.

## Arrow Behavior

- The first click shows the arrow in the direction you last used. The first time you use PowerPaste, it goes up.
- A click while PowerPaste is on turns it off.
- If you turn PowerPaste off without pasting, it goes the other way the next time you turn it on.
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

### Seeing the next fragment

While Exploding PowerPaste runs, ClipMate shows you which fragment your next paste will give you:

- In the clip's text, below the clip list, the fragment is selected. The selection moves on after each paste.
- The window title ends with **[Fragment [...]]**, for example **ClipMate - Clipboard Manager [Fragment [January]]**.
- The tray icon's tooltip shows **Fragment [...]** too, so you can check it without opening ClipMate.

When PowerPaste stops, the title and tooltip go back to normal.

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
