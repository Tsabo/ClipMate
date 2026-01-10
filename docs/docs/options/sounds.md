---
sidebar_position: 10
title: Sounds Options
---

# Sounds

{/* ![Sounds Options Tab](img/options-sounds.png) */}

ClipMate can play sounds for various events to provide audio feedback. You can enable/disable sounds and choose custom .WAV files.

## Configuring Sounds

- Use the **Test** button to preview each sound
- Select custom .WAV files for any event
- Enable/disable sounds individually

:::tip Keep Files Small
If using custom sounds, keep files small (8-16KB) to avoid tying up CPU or disk during clipboard events. Use Windows Sound Recorder to downsize larger files.
:::

## Sound Events

| Event | Description |
|-------|-------------|
| **New Data Captured** | Successfully captured from COPY or CUT operation |
| **Appending Data** | Append mode is on and new data is being combined |
| **Clipboard Erased** | Something has cleared the clipboard |
| **Data Ignored/Rejected** | Data rejected (no matching formats in Application Profile) |
| **PowerPaste Complete** | PowerPaste sequence ended (double-sound = looping) |
| **Network Update** | Another user updated the shared database |
| **Outbound Clip Filter** | Incoming clip was filtered and put back as plain-text |

## Understanding Sound Sequences

### Capture + Filter

When copying with outbound filtering enabled, you'll hear:
1. **Pop** - New data captured
2. **Whoosh** - Clip filtered and returned to clipboard as plain-text

## Troubleshooting Unexpected Sounds

If sounds play unexpectedly, some program is using the clipboard without your knowledge.

### Common Causes

- Programs updating clipboard when shutting down (fairly common)
- Programs using clipboard at startup (rude behavior!)
- Programs copying data internally (toolbar setup, etc.)

### Diagnosing

1. Note which sound plays
2. Use Sound Settings to identify the event type
3. Check the Event Log for correlation
4. Identify the offending application

## See Also

- [Event Log](../user-interface/main-toolbar/event-log.md)
- [Application Profile](application-profile.md)
