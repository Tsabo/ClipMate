---
sidebar_position: 2
title: Macro Pasting
---

# Macro Pasting

Similar to [Templates](templates.md), Macro Pasting allows dynamic substitution of elements within clip text. But where templates work via the clipboard, macros send **keystrokes** directly to the target application.

This allows:
- Navigation with **Tab** and **Enter**
- Key modifiers like **Ctrl**, **Shift**, and **Alt**
- Automated form filling and data entry

## Creating Macro Clips

Macro clips are plain text marked with a special "macro" flag. To mark a clip as a macro:
- Set the flag in **Clip Properties** dialog
- Use the right-click menu in ClipList
- Add the "Toggle Macro" button to the editor toolbar

Macro clips display with a special icon in the ClipList.

## Example

```
MyUserID{TAB}MyPassword{ENTER}
```

This macro:
1. Types "MyUserID"
2. Sends **Tab** to move to next field
3. Types "MyPassword"
4. Sends **Enter** to submit

## Key Modifiers

| Modifier | Key |
|----------|-----|
| `~` | Shift |
| `^` | Control |
| `@` | Alt |

To send these as literal characters, encase in braces:
```
me{@}my.com  → types: me@my.com
```

## Supported Key Names

Surround key names with `{}` braces:

**Navigation:**
- `{TAB}`, `{ENTER}`, `{ESC}`, `{ESCAPE}`
- `{UP}`, `{DOWN}`, `{LEFT}`, `{RIGHT}`
- `{HOME}`, `{END}`, `{PGUP}`, `{PGDN}`

**Editing:**
- `{BKSP}`, `{BS}`, `{BACKSPACE}`
- `{DEL}`, `{DELETE}`, `{INS}`
- `{CLEAR}`

**Function Keys:**
- `{F1}` through `{F16}`

**Other:**
- `{BREAK}`, `{CAPSLOCK}`, `{NUMLOCK}`, `{SCROLLLOCK}`
- `{PRTSC}`, `{HELP}`

### Repeat Keys

Follow the key name with a space and number to repeat:
```
{LEFT 6}  → sends Left arrow 6 times
```

## Replacement Tags

| Tag | Value |
|-----|-------|
| `#DATE#` | Date clip was captured |
| `#TIME#` | Time clip was captured |
| `#CREATOR#` | Source application |
| `#TITLE#` | Clip title |
| `#URL#` | Source URL (if available) |
| `#CURRENTDATE#` | Current date |
| `#CURRENTTIME#` | Current time |
| `#SEQUENCE#` | Sequence number (1, 2, 3...) |
| `#PAUSE#` | Pause for half a second |

## Important Notes

:::caution Line Breaks
Natural line breaks in text are **not** processed. Use `{ENTER}` after each line for the cursor to move down.
:::

## Using Macros

Macros must be invoked via **QuickPaste**:
1. Double-click the macro clip, OR
2. Select it and press **Enter**

The macro sends keystrokes to the target application.

## Sample Macros

Load the "samples" collection to see example macro clips.

## See Also

- [Templates](templates.md)
- [QuickPaste Options](../options/quickpaste.md)
- [PowerPaste](../user-interface/main-toolbar/powerpaste.md)
