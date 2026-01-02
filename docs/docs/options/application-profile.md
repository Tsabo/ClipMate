---
sidebar_position: 9
title: Application Profile
---

# Application Profile

Application Profiles prevent ClipMate from being overwhelmed by unwanted data. Many applications present the same data in multiple formats—Microsoft Excel, for example, can provide data in 21 different formats. Capturing everything is wasteful of time and space.

## How It Works

ClipMate's Application Profiles let you determine which formats to capture on an **application-by-application basis**:

1. When you first copy from any application, ClipMate examines the data formats
2. ClipMate creates an entry in the Application Profile list
3. Default settings are applied based on the data formats seen
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
