---
sidebar_position: 6
title: Glossary
---

# Glossary

## Application Profile

The rule for which clipboard formats ClipMate captures from one application. An application gets a profile the first time you copy from it. See [Application Profile](options/application-profile.md).

## Bitmap

A pixel-based image format. Photographs and pixel-based images copied to the clipboard are captured as bitmaps. See [Bitmap Tab](user-interface/preview-edit/bitmap-tab.md).

## Clip

One thing you copied, kept by ClipMate. A clip holds every format the application offered when you copied it, such as plain text and HTML, along with details like when it was captured and which application it came from.

## Clipboard

A shared memory area in Windows that applications use to exchange data. ClipMate watches the clipboard and captures what you copy.

## ClipMate 7 Import

Bringing the clips of a ClipMate 7 database into a new or an existing database. See [ClipMate 7 Import](coming-from-clipmate-7/import.md).

## Collection

A folder within a database where clips are organized and stored. New databases start with these, among others:
- **Inbox**: where new clips go
- **Overflow**: the oldest clips trimmed from the Inbox
- **Safe**: clips that are never deleted automatically

See [Data Management](advanced/data-management.md).

## Copy to local

Keeping a clip from a Remote Database by storing it as a new clip in one of your own databases. See [Copy to local](options/devices.md#copy-to-local).

## Database

A self-contained store of clips and collections, kept as a single file. Each one is a top-level item in the collection tree, and ClipMate can have several open at once. See [Database Options](options/databases.md).

## Device

One installation of ClipMate, with its own identity. If you use ClipMate on a desktop and a laptop, you have two Devices. See [Devices Options](options/devices.md).

## Discovered Device

A Device on your local network that you haven't paired with yet. Seeing one doesn't let it connect to you. See [Discovered Devices](options/devices.md#discovered-devices).

## Fragment

One part of a clip's text that Exploding PowerPaste pastes on its own. ClipMate splits the clip into fragments at the delimiters set in [Pasting Options](options/pasting.md). See [Exploding PowerPaste](user-interface/main-toolbar/powerpaste.md#exploding-powerpaste).

## HTML

HyperText Markup Language. When you copy from a web browser, ClipMate captures the HTML format to preserve formatting. See [HTML Tab](user-interface/preview-edit/html-tab.md).

## Pairing

Two Devices each accepting the other's Device ID, so they trust each other. It completes only once both have accepted. See [Pairing](options/devices.md#pairing).

## Paste Target

The application window a clip is pasted into. Unless you lock one, QuickPaste uses the last suitable window you were working in before you switched to ClipMate.

## Peer

Another of your Devices that you've paired with this one. See [Peers](options/devices.md#peers).

## Picture (Metafile)

A vector-based image format used for drawings, clip art, and diagrams. Unlike bitmaps, pictures scale without quality loss. See [Picture Tab](user-interface/preview-edit/picture-tab.md).

## PowerPaste

Pasting a chosen sequence of clips one at a time: each time you paste, the next clip takes the previous one's place on the clipboard. See [PowerPaste](user-interface/main-toolbar/powerpaste.md).

## Purging Rules

The setting on a collection that decides when ClipMate removes its oldest clips: by number of clips, by age, or never.

## QuickPaste

Pasting a clip straight into the Paste Target by double-clicking it, or by selecting it and pressing **ENTER**. See [QuickPaste Options](options/quickpaste.md).

## Remote Database

A Peer's Shared Database, as it appears in your collection tree. You can browse and search it, but you can't change it. See [Shared and Remote Databases](options/devices.md#shared-and-remote-databases).

## Remote Search

A search of a Remote Database. Your criteria go to the Peer, which runs the search on its Shared Database and sends back the matches. See [Searching a Remote Database](options/devices.md#searching-a-remote-database).

## Rich Text Format (RTF)

A text format that preserves fonts, colors, and formatting. Word processors typically copy data as RTF. See [Rich Text Tab](user-interface/preview-edit/rich-text-tab.md).

## Shared Database

One of your databases that you've chosen to let your Peers read, by turning on **Share with Peers**. See [Shared and Remote Databases](options/devices.md#shared-and-remote-databases).

## Virtual Collection

A collection whose clips come from a saved search over its database, such as **Today** or **Images**. It never receives new clips and can't be reordered by hand. See [Data Management](advanced/data-management.md).
