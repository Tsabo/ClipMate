---
sidebar_position: 1
title: Templates
---

# Templates

Templates allow you to paste a clip along with metadata like date/time, source URL, and sequence numbers. It works like a mail merge—you create a layout with "tags" that are replaced with clip data when pasting.

## Example

**Captured clip:**
```
Aspirin - good for headaches, fever, blood thinning. 
Hard on stomach.
```

**Template:**
```
Source: #URL#
Captured: #DATE# #TIME#
----------------------------------------------------
#CLIP#
----------------------------------------------------
Pasted: #CURRENTDATE# #CURRENTTIME#
```

**Result when pasting:**
```
Source: http://www.example.com/aspirin.htm
Captured: 9/2/2024 11:59:02 PM
----------------------------------------------------
Aspirin - good for headaches, fever, blood thinning. 
Hard on stomach.
----------------------------------------------------
Pasted: 9/3/2024 08:59:02 AM
```

## Available Tags

| Tag | Replaced With |
|-----|---------------|
| `#CLIP#` | The clip content |
| `#DATE#` | Date clip was captured |
| `#TIME#` | Time clip was captured |
| `#CREATOR#` | Source application |
| `#TITLE#` | Clip title |
| `#URL#` | Source URL (if from browser) |
| `#CURRENTDATE#` | Current date when pasting |
| `#CURRENTTIME#` | Current time when pasting |
| `#SEQUENCE#` | Sequence number (1, 2, 3...) |

## How It Works

Unlike QuickPaste Format Strings (which send keystrokes), templates pre-assemble the output before transferring to the clipboard. The merged result is placed on the clipboard as a single piece of text.

### Advantages
- More reliable than keystroke-based methods
- No "runaway macro" situations
- No keystroke compatibility problems

### Limitations
- Cannot send navigation keys (Tab, Enter)
- Only works with **plain text** clips
- HTML, RTF, Bitmap formats are not processed

## Creating Custom Templates

Templates are text files in the `Templates` directory:
```
C:\Program Files\ClipMate\Templates
```

To create a template:
1. Open the Templates menu
2. Select **Open Directory**
3. Create a new `.txt` file
4. Add your template text with tags
5. Restart ClipMate or refresh the list

The filename (without extension) becomes the menu item name.

## Using Templates

1. Add the Template button to the toolbar
2. Click the drop-down arrow to see available templates
3. Select a template to activate it
4. When you select or paste a clip, it's merged with the template

Templates remain active until you select another template, select "None," or quit ClipMate.

### Reset Sequence

The `#SEQUENCE#` tag increments with each paste. Reset it from the Templates menu.

## Compatibility

Templates work with:
- QuickPaste
- PowerPaste
- Exploding PowerPaste

## Troubleshooting

**Q: I see "#URL#" literally when pasting.**
A: You may have accidentally activated a template. Select "None" from the template menu.

**Q: Source URL isn't appearing.**
A: Source URL is only available when copying from browsers that supply it in the HTML data. Internet Explorer and Firefox support this; Opera may require DDE (enable in Advanced options).

## See Also

- [Templates Button](../user-interface/main-toolbar/templates.md)
- [QuickPaste Options](../options/quickpaste.md)
