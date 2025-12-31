# Third-Party Software Notices and Information

ClipMate includes or makes use of certain third-party software components. The following is a list of such components along with their respective copyright notices and license information.

---

## Table of Contents

- [Vendored Libraries](#vendored-libraries)
- [NuGet Package Dependencies](#nuget-package-dependencies)
  - [UI Frameworks](#ui-frameworks)
  - [Data & Configuration](#data--configuration)
  - [Logging](#logging)
  - [Build Tools](#build-tools)
  - [Testing](#testing)
  - [Microsoft Libraries](#microsoft-libraries)

---

## Vendored Libraries

These libraries have been embedded directly into the ClipMate source tree with modifications.

### 1. Emoji.Wpf (v0.3.4)

**License**: WTFPL (Do What The F*ck You Want To Public License)  
**Source**: https://github.com/samhocevar/emoji.wpf  
**Copyright**: Sam Hocevar and contributors  
**Used for**: Emoji rendering and custom color font support

**License Text**:
```
DO WHAT THE FUCK YOU WANT TO PUBLIC LICENSE
Version 2, December 2004

Copyright (C) 2004 Sam Hocevar <sam@hocevar.net>

Everyone is permitted to copy and distribute verbatim or modified
copies of this license document, and changing it is allowed as long
as the name is changed.

DO WHAT THE FUCK YOU WANT TO PUBLIC LICENSE
TERMS AND CONDITIONS FOR COPYING, DISTRIBUTION AND MODIFICATION

0. You just DO WHAT THE FUCK YOU WANT TO.
```

**See**: [Source/src/ClipMate.EmojiWpf/VENDORED_LICENSES.md](Source/src/ClipMate.EmojiWpf/VENDORED_LICENSES.md)

---

### 2. Typography (Submodule of Emoji.Wpf)

**License**: MIT (with additional permissive licenses for components)  
**Source**: https://github.com/LayoutFarm/Typography  
**Copyright**: LayoutFarm, PaintLab, and contributors  
**Used for**: OpenType font parsing and glyph rendering

**Component Licenses**:
- Core library: MIT License
- Font components: Apache 2.0 (from NRasterizer, PDFBox, AFDKO)
- FreeType-derived code: FreeType Project License (3-clause BSD)
- AGG components: BSD License
- Unicode data: Unicode License (BSD-style)

**See**: [Source/src/ClipMate.EmojiWpf/VENDORED_LICENSES.md](Source/src/ClipMate.EmojiWpf/VENDORED_LICENSES.md)

---

### 3. UnicodeEmoji (Emoji 15.0)

**License**: Unicode, Inc. Terms of Use  
**Source**: https://www.unicode.org/Public/emoji/  
**Copyright**: © 2022 Unicode®, Inc.  
**Used for**: Official Unicode emoji test data and sequences

**Notice**: Unicode and the Unicode Logo are registered trademarks of Unicode, Inc. in the U.S. and other countries.

**Terms**: https://www.unicode.org/terms_of_use.html

**See**: [Source/src/ClipMate.EmojiWpf/VENDORED_LICENSES.md](Source/src/ClipMate.EmojiWpf/VENDORED_LICENSES.md)

---

### 4. WpfHexaEditor (v2.1.7)

**License**: Apache License 2.0  
**Source**: https://github.com/abbaye/WpfHexEditorControl  
**Copyright**: Derek John Abbotts (Abbaye) and contributors  
**Used for**: Binary content viewing and hex editing

**License Text**:
```
Apache License
Version 2.0, January 2004
http://www.apache.org/licenses/

Copyright [2024] [Derek John Abbotts (Abbaye)]

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
```

**See**: [Source/src/ClipMate.WpfHexEditor/NOTICE.txt](Source/src/ClipMate.WpfHexEditor/NOTICE.txt)

---

### 5. Icons8

**License**: Icons8 License  
**Source**: https://icons8.com  
**Copyright**: Icons8 LLC  
**Used for**: Application icons (clipboard icon)

**License Terms**: https://icons8.com/license

**Attribution**: Clipboard icon by Icons8 - https://icons8.com/icon/11864/clipboard

**Note**: Icons8 requires attribution for free usage. Commercial licenses are available for attribution-free usage.

---

## NuGet Package Dependencies

### UI Frameworks

#### CommunityToolkit.Mvvm (v8.4.0)

**License**: MIT  
**Source**: https://github.com/CommunityToolkit/dotnet  
**Copyright**: .NET Foundation and Contributors  
**Used for**: MVVM infrastructure, observable objects, messaging

**License**: https://github.com/CommunityToolkit/dotnet/blob/main/LICENSE

---

#### DevExpress WPF (v25.2.3)

**License**: Commercial (Developer Express Inc.)  
**Website**: https://www.devexpress.com/  
**Used for**: WPF UI controls, theming, data grids, ribbon, dialogs

**Note**: DevExpress is a commercial product. Users of ClipMate must obtain their own DevExpress license if they wish to modify or redistribute the application. The MIT license of ClipMate's source code does not extend to DevExpress components.

**License Information**: https://www.devexpress.com/Support/EULAs/

---

#### LinkingMountains.ImageZoom (v1.0.0)

**License**: MIT  
**Source**: https://github.com/LinkingMountains/ImageZoom  
**Copyright**: Linking Mountains  
**Used for**: Image zoom and pan functionality

---

#### Microsoft.Web.WebView2 (v1.0.3650.58)

**License**: Microsoft Software License Terms  
**Source**: https://www.nuget.org/packages/Microsoft.Web.WebView2  
**Copyright**: Microsoft Corporation  
**Used for**: HTML preview using Microsoft Edge WebView2

**License**: https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.3650.58/License

---

#### Monaco Editor (v0.52.0)

**License**: MIT  
**Source**: https://github.com/microsoft/monaco-editor  
**Copyright**: Microsoft Corporation  
**Used for**: Code and text editing with syntax highlighting, IntelliSense, and SQL editing

**License**:
```
MIT License

Copyright (c) 2016 - present Microsoft Corporation

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

#### ThrottleDebounce (v2.0.1)

**License**: MIT  
**Source**: https://github.com/ljfdev/ThrottleDebounce  
**Copyright**: Lee Falcon  
**Used for**: Debouncing auto-save operations in text editors

---

### Data & Configuration

#### Tomlyn.Signed (v0.19.0)

**License**: BSD 2-Clause License  
**Source**: https://github.com/xoofx/Tomlyn  
**Copyright**: Alexandre Mutel  
**Used for**: TOML configuration file parsing

**License**:
```
BSD 2-Clause License

Copyright (c) 2018-2023, Alexandre Mutel
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice,
   this list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF
THE POSSIBILITY OF SUCH DAMAGE.
```

---

#### Dapper (v2.1.66)

**License**: Apache License 2.0  
**Source**: https://github.com/DapperLib/Dapper  
**Copyright**: 2011-2024 Sam Saffron, Marc Gravell, Nick Craver  
**Used for**: Micro-ORM for database operations

**License**: https://github.com/DapperLib/Dapper/blob/main/License.txt

---

### Logging

#### Serilog (v4.3.0)

**License**: Apache License 2.0  
**Source**: https://github.com/serilog/serilog  
**Copyright**: 2013-2024 Serilog Contributors  
**Used for**: Structured logging framework

**License**: https://github.com/serilog/serilog/blob/dev/LICENSE

---

#### NAudio (v2.2.1)

**License**: MIT  
**Source**: https://github.com/naudio/NAudio  
**Copyright**: Mark Heath & Contributors  
**Used for**: Sound playback for system notifications

**License**: https://github.com/naudio/NAudio/blob/master/license.txt

---

### Build Tools

#### Cake Build (v5.0.0)

**License**: MIT  
**Source**: https://github.com/cake-build/cake  
**Copyright**: .NET Foundation and Contributors  
**Used for**: Build automation, CI/CD pipeline orchestration

**License**: https://github.com/cake-build/cake/blob/develop/LICENSE

---

#### Cake.MinVer (v4.0.0)

**License**: MIT  
**Source**: https://github.com/cake-contrib/Cake.MinVer  
**Copyright**: Cake Contributors  
**Used for**: Semantic versioning based on Git tags

**License**: https://github.com/cake-contrib/Cake.MinVer/blob/main/LICENSE

---

#### nanoemoji

**License**: Apache License 2.0  
**Source**: https://github.com/googlefonts/nanoemoji  
**Copyright**: Google LLC  
**Used for**: Building color emoji fonts from SVG files

**License**: https://github.com/googlefonts/nanoemoji/blob/main/LICENSE

---

#### fonttools

**License**: MIT  
**Source**: https://github.com/fonttools/fonttools  
**Copyright**: Just van Rossum and contributors  
**Used for**: Font file manipulation and metadata application

**License**: https://github.com/fonttools/fonttools/blob/main/LICENSE

---

#### Ninja (Build System)

**License**: Apache License 2.0  
**Source**: https://github.com/ninja-build/ninja  
**Copyright**: Google Inc.  
**Used for**: Fast build system required by nanoemoji

**License**: https://github.com/ninja-build/ninja/blob/master/COPYING

---

### Testing

#### TUnit (v1.5.80)

**License**: MIT  
**Source**: https://github.com/thomhurst/TUnit  
**Copyright**: Tom Longhurst  
**Used for**: Modern test framework for unit tests

**License**: https://github.com/thomhurst/TUnit/blob/main/LICENSE

---

#### Moq (v4.20.72)

**License**: BSD 3-Clause License  
**Source**: https://github.com/devlooped/moq  
**Copyright**: 2007-2024 Daniel Cazzulino  
**Used for**: Mocking framework for unit tests

**License**:
```
BSD 3-Clause License

Copyright (c) 2007, Clarius Consulting, Manas Technology Solutions, InSTEDD,
and Contributors. All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice,
   this list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

3. Neither the names of the copyright holders nor the names of its
   contributors may be used to endorse or promote products derived from this
   software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF
THE POSSIBILITY OF SUCH DAMAGE.
```

---

#### WpfPilot (v2.1.1)

**License**: MIT  
**Source**: https://github.com/WPF-Pilot/WpfPilot  
**Copyright**: WPF-Pilot Contributors  
**Used for**: WPF integration testing

**License**: https://github.com/WPF-Pilot/WpfPilot/blob/main/LICENSE

---

### Microsoft Libraries

The following Microsoft libraries are used under the MIT License:

- **Microsoft.EntityFrameworkCore.Sqlite** (v9.0.11) - Entity Framework Core SQLite provider
- **Microsoft.Data.Sqlite** (v9.0.11) - SQLite ADO.NET provider
- **Microsoft.Extensions.*** (v9.0.11) - Dependency injection, logging, hosting abstractions
- **Microsoft.Windows.CsWin32** (v0.3.264) - Windows API source generator

**Copyright**: Microsoft Corporation  
**License**: https://opensource.org/licenses/MIT

---

## Additional Information

### DevExpress Commercial License Notice

This application uses DevExpress WPF components, which are licensed under a commercial license from Developer Express Inc. The MIT license of ClipMate's source code **does not grant any rights** to DevExpress components.

**If you wish to:**
- Use ClipMate in a commercial setting
- Modify and redistribute ClipMate
- Build ClipMate from source

You **must obtain your own DevExpress license** from https://www.devexpress.com/

The use of DevExpress components is clearly separated in the codebase and can be replaced with alternative UI frameworks if needed.

---

## Summary Table

| Component | License | Attribution Required | Commercial Use |
|-----------|---------|---------------------|----------------|
| Emoji.Wpf | WTFPL | Recommended | ✅ Yes |
| Typography | MIT + Various | ✅ Yes | ✅ Yes |
| UnicodeEmoji | Unicode Terms | ✅ Yes | ✅ Yes |
| WpfHexaEditor | Apache 2.0 | ✅ Yes | ✅ Yes |
| Icons8 | Icons8 License | ✅ Yes | **License Available** |
| CommunityToolkit.Mvvm | MIT | ✅ Yes | ✅ Yes |
| DevExpress WPF | **Commercial** | ✅ Yes | **License Required** |
| Monaco Editor | MIT | ✅ Yes | ✅ Yes |
| Tomlyn.Signed | BSD-2-Clause | ✅ Yes | ✅ Yes |
| Dapper | Apache 2.0 | ✅ Yes | ✅ Yes |
| Serilog | Apache 2.0 | ✅ Yes | ✅ Yes |
| NAudio | MIT | ✅ Yes | ✅ Yes |
| Cake Build | MIT | ✅ Yes | ✅ Yes |
| nanoemoji | Apache 2.0 | ✅ Yes | ✅ Yes |
| fonttools | MIT | ✅ Yes | ✅ Yes |
| Ninja | Apache 2.0 | ✅ Yes | ✅ Yes |
| TUnit | MIT | ✅ Yes | ✅ Yes |
| Moq | BSD-3-Clause | ✅ Yes | ✅ Yes |
| Microsoft Libraries | MIT | ✅ Yes | ✅ Yes |

---

**Last Updated**: December 26, 2025

For the most up-to-date license information, please refer to the individual package repositories and official license files.
