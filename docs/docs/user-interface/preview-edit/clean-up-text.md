---
sidebar_position: 3
title: Clean up the text of clips
---

# Clean up the text of clips

Text copied from an email or a web page often arrives with quote marks, stray spaces and line breaks in the wrong places. The commands on the **Edit** menu fix this without opening the clip in the editor. Each one works on every clip you've selected in the ClipList.

| Command | Shortcut | What it does |
|---------|----------|--------------|
| **Clean-Up Text...** | | Opens **Text Cleanup Options**, then applies your choices |
| **Remove Linebreaks** | Ctrl+Alt+B | Joins the lines of each paragraph |
| **Strip Out Non-TEXT Data Formats** | Ctrl+Alt+T | Keeps the clip's plain text and removes everything else |

These commands save the change to the clip, and you can't undo it. Afterward, the first selected clip is on the clipboard, ready to paste.

:::caution Other formats are removed
All three commands leave a clip with only its plain text. Its HTML, rich text and pictures are removed, since they would still hold the old text. A clip with no text, such as a picture, and an encrypted clip stay as they are.
:::

## Remove Linebreaks

**Remove Linebreaks** joins the lines of each paragraph with a space and keeps the blank lines between paragraphs. It also removes spaces from the start of each line, and shortens runs of spaces to one, or two after a period.

You can change what it does by holding a key as you click the command:

| Hold | Result |
|------|--------|
| **Ctrl** | Also removes the line breaks between paragraphs, so the whole clip becomes one line |
| **Shift** | Joins lines with nothing between them, which repairs a long web address split across lines |

## Strip Out Non-TEXT Data Formats

Use this when a program pastes a clip with formatting you don't want. Pasting the clip then works as if you had typed the text. The clip's text and title don't change.

## Text Cleanup Options

**Clean-Up Text...** opens this window. ClipMate remembers your choices for the next time. At first, it's set to remove quote marks (**>**), spaces and tabs from the start of each line.

### Step 1: Strip Characters From Each Line

Tick the characters to remove under **Leading** (the start of each line), **Trailing** (the end of each line) and **Anywhere** (everywhere in the line). To remove characters that aren't listed, tick **Other:** and type them in the box.

### Step 2: Paragraph Formatting

| Option | What it does |
|--------|--------------|
| **No Change** | Leaves the line breaks |
| **Remove Unwanted Linebreaks Within Paragraph** | Joins the lines of each paragraph, like **Remove Linebreaks** |
| **Remove ALL Linebreaks** | Joins everything into one line |
| **Re-Break Lines At Column:** | Word-wraps the text at a column from 1 to 999. A word longer than that stays whole |
| **Start Each Line With:** | Adds this text to the start of every line, including blank ones. This happens last, so it can put quote marks back after you've rewrapped the text |

### Step 3: Change Case

Choose **lower case**, **UPPER CASE**, **Mixed Case**, **Sentence case.** or **iNVERT cASE**, or leave it at **No Change**. This also changes the clip's title.

### Step 4: Find and Replace

Type the text to look for in **Find What** and its replacement in **Replace With**. This also changes the clip's title. It runs after the case change, so look for the text as Step 3 leaves it. **Match case** makes it tell capitals from lowercase.

| Option | What it does |
|--------|--------------|
| **RegEx?** beside **Find What** | Treats **Find What** as a regular expression |
| **RegEx?** beside **Replace With** | Lets the replacement use a regular expression's groups, such as `$1`, and character codes, such as `\x09` for a tab |
| **Line By Line?** | Runs the regular expression on each line. Turn it off to run it on the whole clip, which is the only way to match a line break |

## Clean up in the editor

**Text Cleanup** on the [Text Tab](text-tab.md) toolbar opens the same window and applies your choices to the text in the editor.
