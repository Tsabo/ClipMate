# ClipMate

A modern recreation of the classic ClipMate clipboard manager built with .NET 10 and WPF.

> This project is a modern, open‑source tribute to the original [ClipMate](http://www.clipmate.com/clipmate7.htm)® by
> [Thornsoft Development](http://www.thornsoft.com/), which I have used and appreciated since 2004.

![ClipMate Explorer Window](Resources/Images/explorer-window.png)

> 🧪 **Beta Software** - ClipMate has reached feature parity with the original ClipMate and is now in beta testing. While all major features are implemented, you may encounter bugs as we focus on stability and polish. Please report any issues you find!

## Download

[![GitHub Downloads (all assets, latest release)](https://img.shields.io/github/downloads-pre/tsabo/clipmate/latest/total?style=for-the-badge&logo=github&logoColor=white)](/releases)


## Status

**Current Progress: ~99% Feature Complete**

### ✅ Implemented (99%)
- Clipboard capture and monitoring (text, images, RTF, HTML, files)
- Multi-database support with collections and folders
- Search with saved queries and SQL support
- QuickPaste with auto-targeting, formatting strings, and full menu controls
  - Lock QuickPaste Target, GoBack mode, Send Tab/Enter keys
  - Paste Now trigger, Reset Sequence counter
- PowerPaste automation with macro execution
- Templates with tag replacement (#DATE#, #TIME#, #SEQUENCE#, etc.)
- Macro execution with security validation and Windows SendInput API
- Shortcuts (nicknames) for quick clip access
- Import/Export (XML and flat-file formats)
- Application profiles for capture filtering
- Diagnostic tools (SQL console, event log, clipboard diagnostics, paste trace)
- Floating clip viewer with auto-follow functionality
- Text editing and transformation (case conversion, cleanup, line break removal)
- 6-format clip viewer (Text, HTML, RTF, Bitmap, Picture, Binary)
- Update checker with GitHub API integration
- Documentation site with 50+ pages (tutorials, UI, options, advanced features)
- Database maintenance (backup, restore, repair, cleanup)
- Multi-selection operations (copy/move/delete multiple clips)
- Undo system for clip operations
- Encryption with AES-256 and legacy ARC4 decrypt support
- Printing with DevExpress reports, customizable layouts, and print preview
- QuickPrint toggle and Print Options
- Re-establish clipboard connection (manual listener repair)
- Window management (close all windows, switch between Classic/Explorer)

### 🚧 Remaining (~1%)
- **Minor Utility Commands** - Low-priority text/format operations:
  - Strip Non-TEXT data formats, Convert File Pointer to Text, Unicode to ANSI conversion
  - Shift Left/Right (text indentation), Spell checking (deferred - Monaco incompatibility)
  - Select Collection dialog (Ctrl+G style quick picker)
  - Transparency slider, Visibility/Classic Options submenus

### 🧪 Needs Testing
- Cross-database operations (copy/move clips between databases)
- Large database performance (1000+ clips)
- Edge cases in clipboard format handling (RTF, HTML, File formats)
- QuickPaste with various target applications
- Macro execution across different application types

### 📚 Documentation Status (~80% Complete)

**Live Site**: [https://jeremy.browns.info/ClipMate/](https://jeremy.browns.info/ClipMate/)

**Completed**:
- 50+ documentation pages written
- 7 comprehensive tutorial lessons (basic operation → shortcuts)
- Complete UI reference (ClipMate Classic/Explorer, ClipList, ClipViewer, toolbars)
- All 11 options tabs documented
- Advanced features (templates, macros, search, data management)
- Glossary with terminology definitions
- GitHub Pages deployment with automatic builds

**Remaining Work**:
- Screenshots and images for visual guides
- Final accuracy review against current implementation
- Missing feature documentation (encryption, printing - when implemented)
- Video tutorials or animated GIFs for key workflows


## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- Windows 10 (1809+) or Windows 11
- Visual Studio 2026+ or VS Code with C# Dev Kit (for development)

## Third-Party Software

ClipMate uses the following open-source and commercial libraries:

**Vendored Libraries** (embedded in source):
- **Emoji.Wpf 0.3.4** - Emoji rendering (WTFPL license)
- **Typography** - OpenType font parsing (MIT + various permissive licenses)
- **UnicodeEmoji** - Unicode emoji data (Unicode Inc. terms)
- **WpfHexaEditor v2.1.7** - Binary hex editing (Apache 2.0)
- **Icons8** - Application icons (Icons8 license)

**Key Dependencies**:
- **DevExpress WPF 25.2.3** - UI controls and theming (**Commercial license required**)
- **Monaco Editor 0.52.0** - Code/text editing via WebView2 (MIT)
- **CommunityToolkit.Mvvm 8.4.0** - MVVM infrastructure (MIT)
- **Entity Framework Core 10.0** - Data access (MIT)
- **Serilog 4.3.0** - Structured logging (Apache 2.0)
- **Dapper 2.1.66** - Micro-ORM (Apache 2.0)
- **Tomlyn.Signed 0.20.0** - TOML parsing (BSD-2-Clause)
- **NAudio 2.2.1** - Sound playback (MIT)
- **ThrottleDebounce 2.0.1** - Debouncing for auto-save operations (Apache 2.0)
- **TUnit 1.12.43** - Testing framework (MIT)

**Build Tools**:
- **Cake Build 5.0.0** - Build automation (MIT)
- **nanoemoji** - Color font generation (Apache 2.0)
- **fonttools** - Font manipulation library (MIT)
- **Ninja** - Build system (Apache 2.0)

**Complete Attribution**: See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for full license texts and attribution details.

**DevExpress Notice**: This application requires a commercial DevExpress license for production use, modification, or redistribution. The MIT license of ClipMate's source code does not extend to DevExpress components. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for details.

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

Status: **Beta** | Version: 0.1.0 | Last Updated: January 24, 2026
