# ClipMate

A modern recreation of the classic ClipMate clipboard manager built with .NET 9 and WPF.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- Windows 10 (1809+) or Windows 11
- Visual Studio 2025+ or VS Code with C# Dev Kit (for development)

**Additional Libraries**
- **Emoji.Wpf 0.3.4**: Emoji rendering + custom color font support (vendored from https://github.com/samhocevar/emoji.wpf, WTFPL license)
  - **Typography**: OpenType font parsing (vendored submodule from https://github.com/LayoutFarm/Typography, MIT + various permissive licenses)
  - **UnicodeEmoji**: Official Unicode emoji data (vendored submodule, Unicode Inc. terms)
  - **See**: [ClipMate.EmojiWpf/VENDORED_LICENSES.md](Source/src/ClipMate.EmojiWpf/VENDORED_LICENSES.md) for complete attribution
- **WpfHexaEditor**: Binary content viewing (vendored from https://github.com/abbaye/WpfHexEditorControl v2.1.7, Apache 2.0)
- **WebView2**: HTML preview with Microsoft Edge WebView2
- **Tomlyn.Signed**: TOML configuration parsing (strong-named version)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

**Built with .NET 9, WPF, and DevExpress**  
*A modern recreation of the classic ClipMate clipboard manager*

Status: Active Development | Last Updated: December 25, 2025
