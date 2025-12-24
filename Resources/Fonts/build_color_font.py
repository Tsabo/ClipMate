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
        
        print(f"Done! Font saved to: {output_path}")
    finally:
        if os.path.exists(temp_path):
            os.unlink(temp_path)


if __name__ == "__main__":
    if len(sys.argv) < 4:
        print(__doc__)
        sys.exit(1)
    
    build_font(sys.argv[1], sys.argv[2], sys.argv[3])