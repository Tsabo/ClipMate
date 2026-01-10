#!/usr/bin/env python3
"""
Build a color font from SVG files with full metadata support.

Usage:
    python build_color_font.py <config.json> <svg_directory> <output.ttf>

Example:
    python build_color_font.py config.json ./svgs/ ClipMate.ttf

Requirements:
    pip install nanoemoji fonttools
    choco install ninja -y

    $env:Path += ";%APPDATA%\Python\Python311\Scripts"
"""

import json
import subprocess
import tempfile
import os
import sys
import shutil

from fontTools.ttLib import TTFont


# Name table IDs
NAME_IDS = {
    "copyright": 0,
    "family": 1,
    "subfamily": 2,
    "unique_id": 3,
    "fullname": 4,
    "version": 5,
    "postscript_name": 6,
    "trademark": 7,
    "manufacturer": 8,
    "designer": 9,
    "description": 11,
    "vendor_url": 12,
    "designer_url": 13,
    "license": 14,
    "license_url": 15,
}


def load_config(config_path):
    with open(config_path, 'r', encoding='utf-8') as f:
        return json.load(f)


def build_with_nanoemoji(config, svg_dir, temp_output):
    """Generate color font using nanoemoji via TOML config."""
    fc = config["font"]
    metrics = config.get("metrics", {})
    color_format = config.get("colorFormat", "glyf_colr_1")
    glyphs_config = config.get("glyphs", [])
    
    # Build TOML config
    toml_content = f'''family = "{fc["name"]}"
upem = {metrics.get("unitsPerEm", 2048)}
ascender = {metrics.get("ascent", 1800)}
descender = {-abs(metrics.get("descent", 248))}
color_format = "{color_format}"
output_file = "{temp_output.replace(chr(92), '/')}"

[axis.wght]
name = "Weight"
default = 400

[master.default]
style_name = "Regular"

[master.default.position]
wght = 400
'''
    
    # Write temp TOML file
    toml_path = temp_output.replace(".ttf", ".toml")
    with open(toml_path, 'w', encoding='utf-8') as f:
        f.write(toml_content)
    
    # Find nanoemoji executable
    nanoemoji_exe = os.path.join(
        os.environ.get("APPDATA", ""),
        "Python", "Python311", "Scripts", "nanoemoji.exe"
    )
    
    if not os.path.exists(nanoemoji_exe):
        nanoemoji_exe = "nanoemoji"
    
    # Copy SVG files with codepoint-based names
    temp_svg_dir = tempfile.mkdtemp()
    try:
        for glyph in glyphs_config:
            cp = glyph["codepoint"]
            codepoint = int(cp, 16) if isinstance(cp, str) else cp
            src_path = os.path.join(svg_dir, glyph["file"])
            dst_filename = f"emoji_u{codepoint:04x}.svg"
            dst_path = os.path.join(temp_svg_dir, dst_filename)
            shutil.copy(src_path, dst_path)
        
        svg_files = [os.path.join(temp_svg_dir, f) for f in os.listdir(temp_svg_dir) if f.endswith('.svg')]
        
        cmd = [nanoemoji_exe, toml_path] + svg_files
        subprocess.run(cmd, check=True)
    finally:
        shutil.rmtree(temp_svg_dir)
        os.unlink(toml_path)


def set_name_record(name_table, name_id, value):
    """Set a name record for Windows and Mac platforms."""
    name_table.names = [r for r in name_table.names if r.nameID != name_id]
    name_table.setName(value, name_id, 3, 1, 0x409)  # Windows
    name_table.setName(value, name_id, 1, 0, 0x0)    # Mac


def apply_metadata(font_path, config, output_path):
    """Apply full metadata to the generated font."""
    fc = config["font"]
    
    font = TTFont(font_path)
    name_table = font["name"]
    
    name = fc["name"]
    version = fc.get("version", "1.0")
    description = fc.get("description", "")
    keywords = fc.get("keywords", [])
    
    full_description = description
    if keywords:
        full_description += f"\nKeywords: {', '.join(keywords)}"
    
    postscript_name = name.replace(" ", "-")
    vendor_id = fc.get("vendorId", "NONE")[:4]
    unique_id = f"{vendor_id};{version};{postscript_name}"
    
    set_name_record(name_table, NAME_IDS["copyright"], fc.get("copyright", ""))
    set_name_record(name_table, NAME_IDS["family"], name)
    set_name_record(name_table, NAME_IDS["subfamily"], "Regular")
    set_name_record(name_table, NAME_IDS["unique_id"], unique_id)
    set_name_record(name_table, NAME_IDS["fullname"], name)
    set_name_record(name_table, NAME_IDS["version"], f"Version {version}")
    set_name_record(name_table, NAME_IDS["postscript_name"], postscript_name)
    set_name_record(name_table, NAME_IDS["manufacturer"], fc.get("vendor", ""))
    set_name_record(name_table, NAME_IDS["designer"], fc.get("designer", ""))
    set_name_record(name_table, NAME_IDS["description"], full_description)
    set_name_record(name_table, NAME_IDS["vendor_url"], fc.get("designerUrl", ""))
    set_name_record(name_table, NAME_IDS["designer_url"], fc.get("designerUrl", ""))
    set_name_record(name_table, NAME_IDS["license"], fc.get("license", ""))
    set_name_record(name_table, NAME_IDS["license_url"], fc.get("licenseUrl", ""))
    
    if "OS/2" in font:
        font["OS/2"].achVendID = vendor_id.ljust(4)
    
    font.save(output_path)
    font.close()


def generate_preview_html(config, output_dir):
    """Generate preview.html from config.json."""
    fc = config["font"]
    glyphs = config.get("glyphs", [])
    color_format = config.get("colorFormat", "glyf_colr_1")
    
    # Generate glyph cards HTML
    glyph_cards = []
    for glyph in glyphs:
        cp = glyph["codepoint"]
        codepoint = int(cp, 16) if isinstance(cp, str) else cp
        name = glyph.get("name", "unknown")
        glyph_cards.append(f'''        <div class="glyph-card" title="Click to copy codepoint">
          <span class="glyph-icon">&#x{codepoint:04X};</span>
          <div class="glyph-name">{name}</div>
          <div class="glyph-code">U+{codepoint:04X}</div>
        </div>''')
    
    glyph_cards_html = '\n'.join(glyph_cards)
    
    # Get first glyph for size demo
    first_glyph = glyphs[0] if glyphs else {"codepoint": "0xE000"}
    first_cp = first_glyph["codepoint"]
    first_codepoint = int(first_cp, 16) if isinstance(first_cp, str) else first_cp
    
    # Build test input with first 5 glyphs
    test_glyphs = []
    for i, glyph in enumerate(glyphs[:5]):
        cp = glyph["codepoint"]
        codepoint = int(cp, 16) if isinstance(cp, str) else cp
        test_glyphs.append(f'&#x{codepoint:04X};')
    test_value = ''.join(test_glyphs)
    test_hint = ' '.join([f'&amp;#x{int(g["codepoint"], 16) if isinstance(g["codepoint"], str) else g["codepoint"]:04X};' for g in glyphs[:5]])
    
    html_content = f'''<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>{fc["name"]} Preview</title>
  <style>
    @font-face {{
      font-family: '{fc["name"]}';
      src: url('ClipMate.ttf');
    }}

    * {{
      margin: 0;
      padding: 0;
      box-sizing: border-box;
    }}

    body {{
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
      min-height: 100vh;
      padding: 2rem;
      color: #333;
    }}

    .container {{
      max-width: 1200px;
      margin: 0 auto;
      background: white;
      border-radius: 1rem;
      box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
      overflow: hidden;
    }}

    header {{
      background: linear-gradient(135deg, #90caf9 0%, #03a9f4 100%);
      color: white;
      padding: 3rem 2rem;
      text-align: center;
    }}

    header h1 {{
      font-size: 2.5rem;
      margin-bottom: 0.5rem;
      font-weight: 700;
    }}

    header p {{
      font-size: 1.1rem;
      opacity: 0.9;
    }}

    .meta {{
      padding: 2rem;
      background: #f8f9fa;
      border-bottom: 1px solid #e9ecef;
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 1rem;
    }}

    .meta-item {{
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
    }}

    .meta-label {{
      font-size: 0.75rem;
      text-transform: uppercase;
      font-weight: 600;
      color: #6c757d;
      letter-spacing: 0.05em;
    }}

    .meta-value {{
      font-size: 1rem;
      color: #212529;
    }}

    .content {{
      padding: 2rem;
    }}

    .section-title {{
      font-size: 1.5rem;
      font-weight: 600;
      margin-bottom: 1.5rem;
      color: #212529;
      padding-bottom: 0.5rem;
      border-bottom: 2px solid #e9ecef;
    }}

    .glyph-grid {{
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
      gap: 1.5rem;
      margin-bottom: 3rem;
    }}

    .glyph-card {{
      background: #f8f9fa;
      border: 2px solid #e9ecef;
      border-radius: 0.5rem;
      padding: 1.5rem;
      text-align: center;
      transition: all 0.2s ease;
      cursor: pointer;
    }}

    .glyph-card:hover {{
      border-color: #667eea;
      box-shadow: 0 4px 12px rgba(102, 126, 234, 0.15);
      transform: translateY(-2px);
    }}

    .glyph-icon {{
      font-family: '{fc["name"]}';
      font-size: 64px;
      line-height: 1;
      margin-bottom: 1rem;
      display: block;
    }}

    .glyph-name {{
      font-size: 0.875rem;
      font-weight: 600;
      color: #495057;
      margin-bottom: 0.25rem;
      word-wrap: break-word;
    }}

    .glyph-code {{
      font-size: 0.75rem;
      color: #6c757d;
      font-family: 'Courier New', monospace;
    }}

    .size-demo {{
      display: flex;
      flex-wrap: wrap;
      gap: 2rem;
      justify-content: space-around;
      align-items: center;
      padding: 2rem;
      background: #f8f9fa;
      border-radius: 0.5rem;
      margin-bottom: 3rem;
    }}

    .size-sample {{
      text-align: center;
    }}

    .size-sample .icon {{
      font-family: '{fc["name"]}';
      display: block;
      margin-bottom: 0.5rem;
    }}

    .size-sample .label {{
      font-size: 0.75rem;
      color: #6c757d;
      font-weight: 600;
    }}

    .size-16 {{ font-size: 16px; }}
    .size-24 {{ font-size: 24px; }}
    .size-32 {{ font-size: 32px; }}
    .size-48 {{ font-size: 48px; }}
    .size-64 {{ font-size: 64px; }}
    .size-96 {{ font-size: 96px; }}
    .size-128 {{ font-size: 128px; }}

    .test-area {{
      background: #f8f9fa;
      border-radius: 0.5rem;
      padding: 2rem;
      margin-bottom: 3rem;
    }}

    .test-input {{
      width: 100%;
      padding: 1rem;
      font-family: '{fc["name"]}', sans-serif;
      font-size: 48px;
      border: 2px solid #e9ecef;
      border-radius: 0.5rem;
      text-align: center;
      transition: border-color 0.2s;
    }}

    .test-input:focus {{
      outline: none;
      border-color: #667eea;
    }}

    .test-hint {{
      text-align: center;
      margin-top: 1rem;
      font-size: 0.875rem;
      color: #6c757d;
    }}

    footer {{
      background: #f8f9fa;
      padding: 2rem;
      text-align: center;
      color: #6c757d;
      font-size: 0.875rem;
      border-top: 1px solid #e9ecef;
    }}

    footer a {{
      color: #667eea;
      text-decoration: none;
      font-weight: 600;
    }}

    footer a:hover {{
      text-decoration: underline;
    }}

    @media (max-width: 768px) {{
      header h1 {{
        font-size: 2rem;
      }}

      .glyph-grid {{
        grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
        gap: 1rem;
      }}

      .size-demo {{
        gap: 1rem;
      }}
    }}
  </style>
</head>
<body>
  <div class="container">
    <header>
      <h1>{fc["name"]}</h1>
      <p>{fc.get("description", "Color Emoji Icons")}</p>
    </header>

    <div class="meta">
      <div class="meta-item">
        <span class="meta-label">Font Name</span>
        <span class="meta-value">{fc["name"]}</span>
      </div>
      <div class="meta-item">
        <span class="meta-label">Version</span>
        <span class="meta-value">{fc.get("version", "1.0")}</span>
      </div>
      <div class="meta-item">
        <span class="meta-label">Designer</span>
        <span class="meta-value">{fc.get("designer", "Unknown")}</span>
      </div>
      <div class="meta-item">
        <span class="meta-label">Glyphs</span>
        <span class="meta-value">{len(glyphs)} icons</span>
      </div>
      <div class="meta-item">
        <span class="meta-label">Format</span>
        <span class="meta-value">{color_format.replace("glyf_colr_", "COLR v").replace("_", " ")}</span>
      </div>
      <div class="meta-item">
        <span class="meta-label">License</span>
        <span class="meta-value">{fc.get("license", "Unknown")}</span>
      </div>
    </div>

    <div class="content">
      <h2 class="section-title">All Glyphs</h2>
      <div class="glyph-grid">
{glyph_cards_html}
      </div>

      <h2 class="section-title">Size Demonstration</h2>
      <div class="size-demo">
        <div class="size-sample">
          <span class="icon size-16">&#x{first_codepoint:04X};</span>
          <span class="label">16px</span>
        </div>
        <div class="size-sample">
          <span class="icon size-24">&#x{first_codepoint:04X};</span>
          <span class="label">24px</span>
        </div>
        <div class="size-sample">
          <span class="icon size-32">&#x{first_codepoint:04X};</span>
          <span class="label">32px</span>
        </div>
        <div class="size-sample">
          <span class="icon size-48">&#x{first_codepoint:04X};</span>
          <span class="label">48px</span>
        </div>
        <div class="size-sample">
          <span class="icon size-64">&#x{first_codepoint:04X};</span>
          <span class="label">64px</span>
        </div>
        <div class="size-sample">
          <span class="icon size-96">&#x{first_codepoint:04X};</span>
          <span class="label">96px</span>
        </div>
        <div class="size-sample">
          <span class="icon size-128">&#x{first_codepoint:04X};</span>
          <span class="label">128px</span>
        </div>
      </div>

      <h2 class="section-title">Test Area</h2>
      <div class="test-area">
        <input type="text" class="test-input" placeholder="Type or paste codepoints here..." 
               value="{test_value}">
        <div class="test-hint">
          Try pasting: {test_hint}
        </div>
      </div>
    </div>

    <footer>
      <p>
        <strong>{fc["name"]}</strong> • {fc.get("copyright", "")}<br>
        Licensed under the <a href="{fc.get("licenseUrl", "#")}" target="_blank">{fc.get("license", "Unknown License")}</a> •
        <a href="{fc.get("designerUrl", "#")}" target="_blank">View on GitHub</a>
      </p>
    </footer>
  </div>

  <script>
    // Copy codepoint to clipboard on card click
    document.querySelectorAll('.glyph-card').forEach(card => {{
      card.addEventListener('click', function() {{
        const code = this.querySelector('.glyph-code').textContent;
        navigator.clipboard.writeText(code).then(() => {{
          const originalBorder = this.style.borderColor;
          this.style.borderColor = '#28a745';
          setTimeout(() => {{
            this.style.borderColor = originalBorder;
          }}, 500);
        }});
      }});
    }});

    // Allow HTML entities in test input
    const testInput = document.querySelector('.test-input');
    testInput.addEventListener('input', function(e) {{
      const value = this.value;
      const decoded = value.replace(/&#x([0-9A-Fa-f]+);/g, (match, hex) => 
        String.fromCharCode(parseInt(hex, 16))
      );
      if (decoded !== value) {{
        const start = this.selectionStart;
        this.value = decoded;
        this.setSelectionRange(start, start);
      }}
    }});
  </script>
</body>
</html>'''
    
    preview_path = os.path.join(output_dir, "preview.html")
    with open(preview_path, 'w', encoding='utf-8') as f:
        f.write(html_content)
    
    print(f"Preview page generated: {preview_path}")


def build_font(config_path, svg_dir, output_path):
    """Main build function."""
    config = load_config(config_path)
    
    with tempfile.NamedTemporaryFile(suffix='.ttf', delete=False) as tmp:
        temp_path = tmp.name
    
    try:
        print("Building color font...")
        build_with_nanoemoji(config, svg_dir, temp_path)
        
        print("Applying metadata...")
        apply_metadata(temp_path, config, output_path)
        
        print("Generating preview page...")
        output_dir = os.path.dirname(output_path)
        generate_preview_html(config, output_dir)
        
        print(f"Done! Font saved to: {output_path}")
    finally:
        if os.path.exists(temp_path):
            os.unlink(temp_path)


if __name__ == "__main__":
    if len(sys.argv) < 4:
        print(__doc__)
        sys.exit(1)
    
    build_font(sys.argv[1], sys.argv[2], sys.argv[3])