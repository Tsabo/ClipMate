---
sidebar_position: 9
title: Application Profile
---

# Application Profile

{/* ![Application Profile Editor](img/options-app-profile.png) */}

Application Profiles prevent ClipMate from being overwhelmed by unwanted data. Many applications present the same data in multiple formats—Microsoft Excel, for example, can provide data in 21 different formats. Capturing everything is wasteful of time and space.

## How It Works

ClipMate's Application Profiles let you determine which formats to capture on an **application-by-application basis**:

1. An application without a profile has every format it puts on the clipboard captured; copying from it doesn't create a profile
2. An application with a profile has each format checked against the profile, and the formats switched off aren't stored
3. When an application with a profile offers a format its profile doesn't list yet, ClipMate adds the format to the profile: text, Rich Text, HTML, images and file lists start on, any other format starts off
4. You can override settings at any time

**Example:** If your word processor sends Text, Rich Text Format, and Picture, but you only need Text, disable the other formats to save storage space.

## The Application Profile Dialog

Found in the Options dialog, the Application Profile shows a collapsible tree:

- **Top level** - Application names (branches)
- **Expanded** - Data formats from that application (leaves)

### Checkboxes

| Level | Effect |
|-------|--------|
| **Application level** | Turn off to ignore ALL data from that application |
| **Format level** | Turn off to ignore that specific format from the application |

### Hints Panel

As you select branches and leaves, hints may appear with guidance about certain applications or formats.

## Common Scenarios

### Reduce Storage

If an application copies data in multiple formats (Text + RTF + HTML), you can disable formats you don't use.

### Ignore Applications

Some applications copy frequently to the clipboard (like password managers). Disable them entirely in the Application Profile.

### Optimize Performance

Reducing captured formats speeds up clipboard processing, especially for applications that copy large amounts of data.

## Fine-Tuning and Diagnostics

Use these features to analyze what data applications actually send:

- **Capture Special** - See all available formats when copying
- **Paste Trace** - Analyze paste operations

## See Also

- [Capturing Options](capturing.md)
