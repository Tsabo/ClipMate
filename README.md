# ClipMate

A modern recreation of the classic ClipMate clipboard manager built with .NET 9 and WPF.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- Windows 10 (1809+) or Windows 11
- Visual Studio 2025+ or VS Code with C# Dev Kit (for development)

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
- **Entity Framework Core 9.0** - Data access (MIT)
- **Serilog 4.3.0** - Structured logging (Apache 2.0)
- **Dapper 2.1.66** - Micro-ORM (Apache 2.0)
- **Tomlyn.Signed 0.19.0** - TOML parsing (BSD-2-Clause)
- **NAudio 2.2.1** - Sound playback (MIT)
- **TUnit 1.5.80** - Testing framework (MIT)

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

**Built with .NET 9, WPF, and DevExpress**  
*A modern recreation of the classic ClipMate clipboard manager*

Status: Active Development | Last Updated: December 25, 2025
