---
sidebar_position: 2
title: "Lesson 2: PowerPaste"
---

# Lesson 2: PowerPaste

PowerPaste allows you to paste a series of items rapidly. It's like a semi-automatic firearm that reloads after each shot—ClipMate reloads the clipboard after each paste.

## Exercise: Basic PowerPaste

Using the 5 food items from Lesson 1, with Notepad open:

1. Select the **"Apple"** item in ClipMate
2. Click the **PowerPaste button** until it shows the **up arrow** ⬆️
   - This means it will work UP the list
3. Go to Notepad and paste (**Ctrl+V**) — you get "Apple"
4. Paste again — you get "Banana"
5. Keep pasting until you reach "Egg"

That's PowerPaste!

### Direction Matters

- **Up Arrow ⬆️** — Works from bottom to top of the list
- **Down Arrow ⬇️** — Works from top to bottom

To PowerPaste from Egg to Apple, start with Egg and use the down arrow.

## Exercise: Exploding PowerPaste

Exploding PowerPaste breaks a single clip into fragments based on delimiters.

1. Copy this line:
   ```
   John Doe, 123 Main Street, Anytown, NY, 12345, USA
   ```

2. Enable **Edit > Explode Into Fragments** (toggles on/off)

3. Turn on PowerPaste

4. In Notepad, paste 6 times with **Ctrl+V**, pressing **Tab** or **Enter** after each paste

You should get each field separately:
- John Doe
- 123 Main Street
- Anytown
- NY
- 12345
- USA

### How It Works

ClipMate parses the clip and breaks it into fragments at delimiters (commas, periods, semicolons, colons, tabs, line feeds by default).

### Configure Delimiters

Customize delimiters in **Options > Pasting**.

:::tip Use Case
If you have formatted data with predictable delimiters, Exploding PowerPaste can turn an all-day data entry task into minutes.
:::

## For More Information

See [PowerPaste](../user-interface/main-toolbar/powerpaste.md) for complete documentation.

## Next Lesson

[Lesson 3: Append (Glue)](lesson-three-append.md) — Combine multiple copies into one clip.
