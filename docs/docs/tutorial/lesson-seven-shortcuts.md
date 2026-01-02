---
sidebar_position: 7
title: "Lesson 7: Shortcuts"
---

# Lesson 7: Shortcuts

Shortcuts work like QuickPick but can pull clips from **any collection or database**. Think of them like desktop shortcuts to files—ClipMate shortcuts reference any clip in your system.

## Exercise: Create Shortcuts

Using the credit card clips from Lesson 4:

```
1234123412341234
1203
5678567856785678
1204
```

(Re-copy them if needed)

### Assigning Shortcuts

1. Select the first clip (card number)
2. Press **Ctrl+R** to open Rename Clip dialog
3. Set Title: `Visa Number`
4. Set Shortcut: `.CC.V.NBR`
5. Click OK

Repeat for other clips:

| Clip | Title | Shortcut |
|------|-------|----------|
| 1234... | Visa Number | `.CC.V.NBR` |
| 1203 | Visa Exp Date | `.CC.V.DT` |
| 5678... | MasterCard Number | `.CC.M.NBR` |
| 1204 | MasterCard Exp Date | `.CC.M.DT` |

:::tip Structure
Use periods to create hierarchies:
- `.CC` = Credit Cards
- `.CC.V` = Visa
- `.CC.M` = MasterCard
:::

:::tip Carry-Over Prefix
Enable "Carry-Over Last Prefix" to auto-fill `.CC.V.` when creating similar shortcuts.
:::

## Move to Safe Collection

Important clips deserve a permanent home:

1. Select the shortcut clips in ClipList (**Shift+Click** for range)
2. **Edit > Move To Collection**
3. Select **Safe**

Clips in Safe won't be automatically deleted.

## Using Shortcuts

Type `.` (period) to activate shortcuts. All shortcut clips appear at the top of the ClipList.

### Narrowing Results

| You Type | Result |
|----------|--------|
| `.` | All shortcuts |
| `.CC` | Only credit card clips |
| `.CC.V` | Only Visa entries |
| `.CC.V.D` | Just Visa Expiry Date |

**Backspace** widens the search. **Escape** cancels.

## Shortcuts + QuickPaste

The most powerful combination:

1. You're on a web form needing MasterCard details
2. Press **Ctrl+Shift+Q** (QuickPaste)
3. Type `.CC.M`
4. MasterCard clips appear
5. Select and press **Enter** to paste
6. **Ctrl+Shift+Q** again—your search is still active!
7. Select next item and **Enter**

## Organizing Shortcuts

Plan your hierarchy:
- `.CC` — Credit cards
- `.U` — URLs
  - `.U.N` — News URLs
  - `.U.J` — Joke URLs
- `.ADDR` — Addresses
- `.PWD` — Passwords (use encryption!)

## Related Topics

- [QuickPaste Options](../options/quickpaste.md)
- [QuickPick (Lesson 6)](lesson-six-quickpick.md)

## Congratulations!

You've completed the ClipMate tutorial! You now know:
- Basic clipboard capture and retrieval
- PowerPaste for sequential pasting
- Append to combine clips
- QuickPaste for ad-hoc pasting
- Text editing features
- QuickPick for filtering
- Shortcuts for instant access

Explore the rest of the documentation to discover even more features!
