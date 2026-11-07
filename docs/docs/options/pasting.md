---
sidebar_position: 5
title: Pasting Options
---

# Pasting Options

![The Pasting tab of the Options dialog, with the PowerPaste and Outbound Clip Filtering settings](img/options-pasting.png)

These settings control how ClipMate pastes data to applications.

## Exploding PowerPaste

While PowerPaste normally pastes one clip at a time, Exploding PowerPaste can break up formatted text and paste individual fields. For example, comma-delimited data can be pasted field by field.

### Configure Delimiters

Specify characters that separate fragments:
- Commas: `,`
- Tabs: `\t`
- Line breaks: `\n`
- Spaces: ` ` (space character)

Default delimiters: `,.;:\n\t`

### Options

| Option | Effect |
|--------|--------|
| **Strip Delimiter** | Remove the delimiter character from pasted data |
| **Strip Spaces/Control Characters** | Remove whitespace and control characters |

See [PowerPaste](../user-interface/main-toolbar/powerpaste.md) for details on how Exploding PowerPaste works.

## PowerPaste Loop

Enable looping to restart the PowerPaste sequence after completing, rather than stopping.

:::note Location
This option is on the main menu under **Edit**, not in this dialog. Click to toggle on/off.
:::

## PowerPaste Shield

PowerPaste Shield is on by default. With it on, only pasting into the program you're working in moves PowerPaste on to the next clip. Programs that read the clipboard in the background can't read the clip while PowerPaste is on, so they can't make it skip ahead. These include clipboard history, Remote Desktop and WSL clipboard sharing, and other clipboard managers. Once PowerPaste stops, they can read the clipboard again.

If pasting into a program doesn't move PowerPaste on, turn this option off. Then any program that reads the clipboard moves PowerPaste on, so it may skip clips while such a program is running.

:::note Windows clipboard history
You don't need to turn off clipboard history (**Windows key + V**) to use PowerPaste. While PowerPaste is on, clipboard history doesn't record the clips it offers, and it can't make PowerPaste skip ahead.
:::

## Filter Outbound Clips

Toggle [Outbound Clip Filtering](../user-interface/main-toolbar/outbound-clip-filtering.md) with two modes:

### Passive Filtering

Requires you to:
1. Bring ClipMate to the foreground
2. Dismiss to background
3. Clipboard updates with filtered clip as focus is lost

This is similar to ClipMate 6.5 behavior.

### Active Filtering

Updates the clipboard immediately when any new clip arrives:
1. Copy HTML from browser
2. ClipMate captures all formats (Text, RTF, HTML)
3. Within ½ second, clipboard is replaced with plain-text version
4. A "whoosh" sound plays
5. Paste as plain-text without interacting with ClipMate

:::caution File Operations
Outbound filtering (especially active mode) disables copy/paste of files in Windows Explorer. This is expected behavior—ClipMate is replacing clipboard contents with plain text as instructed.
:::

## See Also

- [PowerPaste](../user-interface/main-toolbar/powerpaste.md)
- [Outbound Clip Filtering](../user-interface/main-toolbar/outbound-clip-filtering.md)
- [Application Profile](application-profile.md)
