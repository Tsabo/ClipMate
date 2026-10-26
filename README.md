# ClipMate

[![Latest Release](https://img.shields.io/github/v/release/tsabo/clipmate?include_prereleases&style=for-the-badge&logo=github&logoColor=white)](https://github.com/Tsabo/ClipMate/releases)
[![Build](https://img.shields.io/github/actions/workflow/status/tsabo/clipmate/build.yml?branch=master&style=for-the-badge&logo=github&logoColor=white&label=build)](https://github.com/Tsabo/ClipMate/actions/workflows/build.yml)
[![CodeQL](https://img.shields.io/github/actions/workflow/status/tsabo/clipmate/codeql.yml?branch=master&style=for-the-badge&logo=github&logoColor=white&label=codeql)](https://github.com/Tsabo/ClipMate/actions/workflows/codeql.yml)
[![License: MIT](https://img.shields.io/github/license/tsabo/clipmate?style=for-the-badge)](LICENSE)
[![Documentation](https://img.shields.io/badge/docs-clipmate-blue?style=for-the-badge&logo=readthedocs)](https://jeremy.browns.info/ClipMate/)
[![Changelog](https://img.shields.io/github/v/release/tsabo/clipmate?include_prereleases&label=changelog&color=E05735&style=for-the-badge&logo=keepachangelog&logoColor=white)](CHANGELOG.md)

A modern recreation of the classic ClipMate clipboard manager built with .NET 10 and WPF.

> This project is a modern, open‑source tribute to the original [ClipMate](http://www.clipmate.com/clipmate7.htm)® by
> [Thornsoft Development](http://www.thornsoft.com/), which I have used and appreciated since 2004.

![ClipMate Explorer Window](Resources/Images/explorer-window.png)

> 🧪 **Beta Software** - ClipMate has reached feature parity with the original ClipMate and is now in beta testing. While all major features are implemented, you may encounter bugs as we focus on stability and polish. Please report any issues you find!

## Install via WinGet

[![WinGet](https://img.shields.io/winget/v/Tsabo.ClipMate?style=for-the-badge&logo=windowsterminal&logoColor=white)](https://github.com/microsoft/winget-pkgs/tree/master/manifests/t/Tsabo/ClipMate)

```
winget install Tsabo.ClipMate
```

## Download

[![GitHub Downloads (all assets, latest release)](https://img.shields.io/github/downloads-pre/tsabo/clipmate/latest/total?style=for-the-badge&logo=github&logoColor=white)](https://github.com/Tsabo/ClipMate/releases)


## Status

ClipMate has reached **99% feature parity** with the original ClipMate 7.5 and is now in **beta testing**. All core functionality is implemented and stable, with focus now on polish, performance, and bug fixes.

### Key Features
- **Clipboard Management** - Capture and organize text, images, RTF, HTML, and files with multi-format support
- **QuickPaste & PowerPaste** - Smart auto-targeting paste and macro automation for workflow efficiency
- **Organization** - Multi-database support with collections, folders, and virtual (SQL-based) collections
- **Search & Discovery** - Full-text search, saved queries, and SQL console for advanced filtering
- **Templates & Shortcuts** - Reusable content with tag replacement and nickname-based quick access
- **Text Editing** - Monaco editor integration with syntax highlighting, transformations
- **Encryption** - AES-256 protection with session-based key management and automatic cleanup
- **Printing** - Customizable reports with print preview and layout options
- **Diagnostics** - SQL console, event log, clipboard diagnostics, and paste trace tools
- **Import/Export** - XML and flat-file formats for backup and data migration

For complete feature documentation, see the [documentation site](https://jeremy.browns.info/ClipMate/).

### 🧪 Beta Testing Focus
- Cross-database operations (copy/move clips between databases)
- Large database performance (1000+ clips)
- Edge cases in clipboard format handling (RTF, HTML, File formats)
- QuickPaste with various target applications
- Macro execution across different application types

### 📚 Documentation Status (~80% Complete)

**Live Site**: [https://jeremy.browns.info/ClipMate/](https://jeremy.browns.info/ClipMate/)

**Remaining Work**:
- Screenshots and images for visual guides
- Final accuracy review against current implementation
- Missing feature documentation (encryption, printing)
- Video tutorials or animated GIFs for key workflows


## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- Windows 10 (1809+) or Windows 11
- Visual Studio 2026+ or VS Code with C# Dev Kit (for development)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

**Third-Party Components**: This project uses third-party libraries with various open-source licenses (MIT, Apache 2.0, BSD, WTFPL) and one commercial component (DevExpress). See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for complete license information and attribution.

---
## Acknowledgment

ClipMate (this project) is an independent, open‑source recreation inspired by the original **ClipMate® Clipboard Extender**, created by **Thornsoft Development, Inc.** and developed by Chris Thornton.

I have been a licensed ClipMate user since 2004, and this project was created as a tribute to the original application and the impact it had on my daily workflow. This project is not affiliated with, endorsed by, or connected to Thornsoft
Development in any way. All trademarks and copyrights for the original ClipMate® belong to their respective owners.

---

**Built with .NET 10, WPF, and DevExpress**  
*A modern recreation of the classic ClipMate clipboard manager*

Status: **Beta** | Version: 1.0.0-beta.1 | Last Updated: October 25, 2026
