# Vendored Third-Party Libraries

This directory contains vendored (embedded) third-party libraries that have been integrated directly into the ClipMate source tree. All libraries use permissive open-source licenses.

## Emoji.Wpf

**Source**: https://github.com/samhocevar/emoji.wpf  
**Version**: 0.3.4  
**License**: WTFPL (Do What The F*ck You Want To Public License)  
**Authors**: Sam Hocevar and contributors  
**Vendoring Date**: December 2024  

**Purpose**: Provides emoji rendering support in WPF applications. We vendored this library to extend it with support for custom color fonts (COLRv0 tables) from ClipMate.ttf.

**License Text**: See [LICENSE.txt](LICENSE.txt)

**Modifications Made**:
- Removed Stfu package dependency (conflicted with .NET 9)
- Created custom implementations: BoolInverter, Behaviors, LinqExtensions
- Made EmojiTypeface.MakeGlyphPlanList() and DrawGlyph() public for external rendering
- Fixed .NET 9 GlyphRun constructor (added PixelsPerDip parameter)
- Updated OpenTypeFont.GetGlyphTypeface() to support pack:// URIs
- Created emoji-test.txt.gz compressed resource
- Converted projects from old-style to SDK-style for .NET 9

---

## Typography

**Source**: https://github.com/LayoutFarm/Typography (originally a git submodule of Emoji.Wpf)  
**License**: MIT (with additional permissive licenses for specific components)  
**Authors**: LayoutFarm, PaintLab, and contributors  
**Vendoring Date**: December 2024  

**Purpose**: Provides OpenType font parsing, glyph layout, and rendering capabilities. Required by Emoji.Wpf for reading COLR/CPAL color tables and performing text shaping.

**Components Used**:
- **Typography.OpenFont**: Reads .ttf/.otf/.woff/.woff2 font files, parses OpenType tables
- **Typography.GlyphLayout**: Performs advanced text layout (GSUB/GPOS, complex scripts)

**Full License Details**: See [Typography/LICENSE.md](Typography/LICENSE.md)

**Credits**:
- MIT License - Core Typography library
- Apache 2.0 - Font components from NRasterizer, PDFBox, AFDKO
- FreeType Project License (3-clause BSD) - FreeType-derived code
- BSD License - Anti-Grain Geometry (AGG) components
- Unicode License (BSD-style) - Unicode data processing

**Files Removed During Cleanup**:
- Demo projects (~50 MB)
- Documentation (~10 MB)
- UnicodeCLDR locale data submodule (~292 MB)
- Unused modules: PixelFarm, TextBreak, TextFlow, TextServices
- All git submodule metadata

**Size Optimization**: Reduced from ~440 MB to ~13 MB (97% reduction)

---

## UnicodeEmoji

**Source**: https://www.unicode.org/Public/emoji/ (originally a git submodule of Emoji.Wpf)  
**License**: Unicode, Inc. Terms of Use  
**Copyright**: © 2022 Unicode®, Inc.  
**Version**: Emoji 15.0  
**Vendoring Date**: December 2024  

**Purpose**: Provides official Unicode emoji test data files for emoji sequence detection, variation selectors, and ZWJ (Zero Width Joiner) sequences.

**Files Included**:
- `emoji-test.txt` - Master emoji test data (598 KB uncompressed, embedded as emoji-test.txt.gz)
- `emoji-data.txt` - Emoji property data
- `emoji-sequences.txt` - Basic emoji sequences
- `emoji-zwj-sequences.txt` - Zero Width Joiner sequences
- `emoji-variation-sequences.txt` - Variation selector sequences

**License Terms**: See [UnicodeEmoji/ReadMe.txt](UnicodeEmoji/ReadMe.txt)  
**Full Terms**: https://www.unicode.org/terms_of_use.html

**Notice**: Unicode and the Unicode Logo are registered trademarks of Unicode, Inc. in the U.S. and other countries.

---

## Summary

All vendored libraries use permissive licenses that allow redistribution and modification:

| Library | License | Commercial Use | Attribution Required |
|---------|---------|----------------|---------------------|
| Emoji.Wpf | WTFPL | ✅ Yes | ❌ No (but recommended) |
| Typography | MIT + Various | ✅ Yes | ✅ Yes |
| UnicodeEmoji | Unicode Terms | ✅ Yes | ✅ Yes |

**Total Size**: ~13 MB (after cleanup from original ~440 MB)

For complete license texts and detailed attribution, refer to the individual license files linked above.

---

**Vendoring Rationale**: We vendored these libraries rather than using NuGet packages to:
1. Enable custom extensions for ClipMate-specific functionality (custom color fonts)
2. Maintain full control over the codebase for security and compatibility
3. Avoid dependency conflicts (e.g., Stfu package conflicting with .NET 9)
4. Ensure long-term maintainability if upstream projects become inactive

Last Updated: December 25, 2025
