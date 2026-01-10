---
sidebar_position: 4
title: Capturing Options
---

# Capturing Options

{/* ![Capturing Options Tab](img/options-capturing.png) */}

These settings control how ClipMate captures clipboard data.

## Startup Capture

### Enable Auto Capture at Startup

By default, ClipMate captures everything copied to the clipboard. Disable this to start with capture turned off.

:::tip Temporary Toggle
To turn capture on/off temporarily without changing this setting, use **Tools** > **Auto Capture**.
:::

### Capture Existing Clipboard Data at Startup

When enabled, if the clipboard already contains data when ClipMate starts, it will be captured rather than lost.

## Clipboard Connection

### Use Vista Clipboard Notification

Windows Vista and later include a new clipboard API that eliminates the troublesome "clipboard notification chain." Enable this on Vista and newer for more reliable clipboard monitoring.

When enabled, the "keepalive" and "reestablish clipboard connection" functions become obsolete.

## HDROP Expansion

When you copy files from Windows Explorer, a format called HDROP enables cut/copy/paste operations. However, HDROP contains no human-readable file listing.

Enable this option to automatically convert HDROP data to plain text showing file paths:

```
C:\Documents\JohnDoe\hello.txt
```

This is equivalent to using **Edit** > **Convert File Pointer To Text** automatically.

## Image Storage

While all images are captured from the clipboard as Bitmap, they are stored compressed as either:

| Format | Description |
|--------|-------------|
| **PNG** | Superior image quality, comparable compression (recommended) |
| **JPEG** | Good compression, some quality loss |

:::tip Recommendation
Use PNG unless you need to share clips with users on ClipMate 6.
:::

## Appending Options

When ClipMate combines clips using the Append command, you can control what's inserted between them.

### Append Separator

Default is a line feed, but you can use:
- `\t` for tab
- `\n` for line break
- Any displayable characters

### Strip Trailing Line Feed

When enabled, ClipMate removes trailing line feeds from the preceding clip before appending (these are common in copied text).

## See Also

- [Application Profile](application-profile.md)
- [Append Button](../user-interface/main-toolbar/append.md)
