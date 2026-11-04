# ClipMate user docs style guide

How to write and change the pages under `docs/docs`. This file sits outside `docs/docs`, so it isn't published.

## Keep the docs current

A change the user can see (a window, label, option, hotkey or behavior) updates its page and any screenshot it changes, in the same pull request. The site publishes on release, so the docs and the feature arrive together.

The site describes published releases, betas included, and has one version. Where it matters, note the first version with a feature: "Added in 1.1".

## Your reader

Write for someone who uses ClipMate and has never heard of ClipMate 7. They aren't a developer, and they came to the page to get something done.

## Voice

- Write in plain language, in the second person: "You can pin a clip", not "The user can pin a clip".
- Use US English.
- Write new text. Text from the ClipMate 7 manual gets rewritten, never copied, including from `Resources/User Manual/`, which is a reference for how features behave and isn't published.
- Say what internals mean for the user, or cut them: "Each Windows user has their own settings", not the registry hive or the table behind them.
- Keep file locations, port numbers and command-line switches to the pages where the reader needs them, usually Troubleshooting.

## Words

- Write UI labels exactly as ClipMate shows them, in bold: **Copy to local...**, **Share with Peers**. Where a label and the glossary spell a word differently, follow the label.
- Use the terms in [the glossary](docs/glossary.md), with the capitalization it gives. Add a term there when a page introduces one.
- Capitalize the names of features and things (Device, Peer, Remote Database) and leave plain verbs lowercase (pair, unpair). "Database", "collection" and "clip" are lowercase on their own.
- Explain Peer in one plain sentence on any page where it first appears: "A Peer is another of your Devices that you've paired with this one."
- ClipMate 7 Import and Import Clip(s) from XML are different features. Write each name in full, and never write "import" alone.

## ClipMate 7

- The upgrade story lives in one section, Coming from ClipMate 7. Other pages describe ClipMate as it is, with no comparisons to ClipMate 7.
- A feature ClipMate no longer has comes off the main pages. If an upgrader would miss it, it goes in the one list in that section.

## Page titles and structure

- Guide titles name what the reader wants to do: "Use ClipMate across your Devices", not "Peer Networking".
- Reference pages describe one window or Options tab, in the order it appears on screen.
- Troubleshooting pages open by saying when you'd use this.

## Links and addresses

- Link between pages with relative paths to the `.md` file, so links survive a move to another address.
- Help buttons in released versions open these addresses, so they have to keep working:
  - `/`, `/options/`, `/options/databases/`
  - `/user-interface/preview-edit/`, `/user-interface/cliplist/`, `/user-interface/main-toolbar/search/`
  - `/advanced/data-management/`
- A page that is removed or merged gets a redirect from its old address.

## Screenshots

- Use light theme at a fixed window size. The Appearance page also has one dark shot.
- Show made-up, everyday-looking data, never your own clips.
- Give every image alt text that describes what it shows.
- Each Options tab, each main dialog and each major step in a guide gets a screenshot. A toolbar button can use an icon or a sentence instead.
- Keep each PNG next to the page that uses it.
