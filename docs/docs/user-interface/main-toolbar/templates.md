---
sidebar_position: 14
title: Templates
---

# 📄 Templates Button

{/* ![Templates Dropdown](img/toolbar-templates.png) */}

The Templates button activates ClipMate's template feature, which allows you to paste clips along with metadata like date/time, source URL, and more.

## Usage

1. Add the Template button to your toolbar
2. Click the drop-down arrow to see available templates
3. Select a template to activate it
4. Now when you select or paste a clip, it's merged with the template

The template remains active until you select another template, select "None," or quit ClipMate.

## How Templates Work

Templates are text files containing replaceable tags. When you paste a clip with a template active, the tags are replaced with actual values from the clip:

| Tag | Replaced With |
|-----|---------------|
| `#CLIP#` | The clip content |
| `#DATE#` | Date of capture |
| `#TIME#` | Time of capture |
| `#CREATOR#` | Source application |
| `#TITLE#` | Clip title |
| `#URL#` | Source URL (if available) |
| `#CURRENTDATE#` | Current date |
| `#CURRENTTIME#` | Current time |
| `#SEQUENCE#` | Sequence number (1, 2, 3...) |

## Template Menu Options

The template menu includes:
- List of available templates
- **None** - Disable templates
- **Reset Sequence** - Reset the sequence counter to 1
- **Open Directory** - Open the templates folder

## Creating Custom Templates

Templates are stored as text files in the `Templates` folder under the ClipMate program directory.

1. Select **Open Directory** from the template menu
2. Create a new `.txt` file
3. Add your template text with tags
4. Save the file
5. Restart ClipMate or select **Refresh** from the template menu

The filename (without extension) becomes the template name in the menu.

## Compatibility

Templates work with:
- QuickPaste
- PowerPaste
- Exploding PowerPaste

Templates only apply to **plain text** clips. Complex formats like HTML, RTF, and Bitmap are not processed through templates.

## See Also

- [Templates (Full Documentation)](../../advanced/templates.md)
