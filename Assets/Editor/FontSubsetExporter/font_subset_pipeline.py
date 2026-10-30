#!/usr/bin/env python3
import argparse
import os
import re
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path

from fontTools.merge import Merger
from fontTools.ttLib import TTFont, TTLibError


@dataclass
class FontSource:
    path: str
    font_index: int
    label: str
    cmap: set[int]


def main() -> int:
    parser = argparse.ArgumentParser(description="Export fallback font subsets for Unity.")
    parser.add_argument("--fonts-file", required=True)
    parser.add_argument("--text-file", required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--prefix", required=True)
    parser.add_argument("--family-name", default="")
    parser.add_argument("--style-name", default="Regular")
    parser.add_argument("--include-recommended-glyphs", action="store_true")
    parser.add_argument("--keep-layout-features", action="store_true")
    args = parser.parse_args()

    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    text = Path(args.text_file).read_text(encoding="utf-8-sig")
    codepoints = unique_codepoints(text)
    fonts = load_fonts(args.fonts_file)

    assignments: dict[int, list[int]] = {i: [] for i in range(len(fonts))}
    missing = []

    for codepoint in codepoints:
        font_index = find_font_for_codepoint(fonts, codepoint)
        if font_index is None:
            missing.append(codepoint)
            continue

        assignments[font_index].append(codepoint)

    output_file = output_dir / f"{safe_name(args.prefix)}.ttf"
    source_subsets = []
    with tempfile.TemporaryDirectory(prefix="unity-font-merge-") as temp_dir:
        for font_index, assigned in assignments.items():
            if not assigned:
                continue

            font = fonts[font_index]
            subset_file = Path(temp_dir) / f"{font_index:02d}_{safe_name(font.label)}.ttf"
            export_subset(font, assigned, subset_file, args.include_recommended_glyphs, args.keep_layout_features)
            source_subsets.append((font, subset_file, len(assigned)))

        if not source_subsets:
            raise RuntimeError("No glyphs were exported. Check the input text and font sources.")

        merge_subsets([subset_file for _, subset_file, _ in source_subsets], output_file)

    family_name = args.family_name.strip() or safe_name(args.prefix)
    style_name = normalize_style_name(args.style_name)
    apply_font_identity(output_file, family_name, style_name)

    print(f"Input unique code points: {len(codepoints)}")
    print(f"Merged source subsets: {len(source_subsets)}")
    print(f"Missing code points: {len(missing)}")
    print(f"Exported font: {output_file}")
    print(f"Font family/style: {family_name} {style_name}")

    if missing:
        formatted_missing = ", ".join(format_codepoint(codepoint) for codepoint in missing)
        print(f"Some characters were not found in any enabled font: {formatted_missing}", file=sys.stderr)

    return 0


def load_fonts(fonts_file: str) -> list[FontSource]:
    fonts = []
    with open(fonts_file, "r", encoding="utf-8") as handle:
        for line_number, line in enumerate(handle, 1):
            line = line.rstrip("\n\r")
            if not line:
                continue

            parts = line.split("\t")
            if len(parts) < 3:
                raise ValueError(f"Invalid font manifest line {line_number}: {line}")

            path = parts[0].lstrip("\ufeff")
            font_index = int(parts[1])
            label = parts[2] or Path(path).stem
            cmap = read_cmap(path, font_index)
            fonts.append(FontSource(path=path, font_index=font_index, label=label, cmap=cmap))

    if not fonts:
        raise ValueError("No enabled fonts were provided.")

    return fonts


def read_cmap(path: str, font_index: int) -> set[int]:
    try:
        font = TTFont(path, fontNumber=font_index, lazy=True)
    except TTLibError as exc:
        raise RuntimeError(f"Failed to read font: {path} index {font_index}: {exc}") from exc

    try:
        cmap = set()
        for table in font["cmap"].tables:
            if table.isUnicode():
                cmap.update(table.cmap.keys())
        return cmap
    finally:
        font.close()


def unique_codepoints(text: str) -> list[int]:
    seen = set()
    result = []
    for character in text:
        codepoint = ord(character)
        if codepoint in seen:
            continue
        seen.add(codepoint)
        result.append(codepoint)
    return result


def find_font_for_codepoint(fonts: list[FontSource], codepoint: int) -> int | None:
    if is_formatting_codepoint(codepoint):
        return 0 if fonts else None

    for index, font in enumerate(fonts):
        if codepoint in font.cmap:
            return index
    return None


def is_formatting_codepoint(codepoint: int) -> bool:
    return codepoint in (0x0009, 0x000A, 0x000D, 0x00A0, 0x200B, 0x200C, 0x200D, 0xFE0E, 0xFE0F)


def export_subset(font: FontSource, codepoints: list[int], output_file: Path, include_recommended: bool, keep_layout: bool) -> None:
    subset_text = "".join(chr(codepoint) for codepoint in codepoints)
    with tempfile.NamedTemporaryFile("w", encoding="utf-8", delete=False, suffix=".txt") as handle:
        handle.write(subset_text)
        text_file = handle.name

    try:
        command = [
            sys.executable,
            "-m",
            "fontTools.subset",
            font.path,
            f"--text-file={text_file}",
            f"--output-file={output_file}",
            "--passthrough-tables",
            "--ignore-missing-glyphs",
            "--no-hinting",
        ]

        if Path(font.path).suffix.lower() in (".ttc", ".otc"):
            command.append(f"--font-number={font.font_index}")

        if include_recommended:
            command.extend(["--recommended-glyphs", "--notdef-glyph", "--notdef-outline"])

        if keep_layout:
            command.append("--layout-features=*")

        completed = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8")
        if completed.returncode != 0:
            raise RuntimeError(f"fonttools failed for {font.path}\n{completed.stdout}")
    finally:
        try:
            os.remove(text_file)
        except OSError:
            pass


def merge_subsets(subset_files: list[Path], output_file: Path) -> None:
    normalized_files = [normalize_for_merge(path) for path in subset_files]

    if len(subset_files) == 1:
        output_file.write_bytes(normalized_files[0].read_bytes())
        return

    merger = Merger()
    merged = merger.merge([str(path) for path in normalized_files])
    try:
        merged["name"] = TTFont(str(normalized_files[0]))["name"]
        merged.save(str(output_file))
    finally:
        merged.close()


def normalize_for_merge(font_path: Path) -> Path:
    normalized_path = font_path.with_name(font_path.stem + "_normalized.ttf")
    font = TTFont(str(font_path))
    try:
        # These optional tables frequently conflict when merging unrelated fonts.
        # Removing them keeps the merged TTF valid for static glyph coverage.
        for table in ("GDEF", "GPOS", "GSUB", "MERG", "gasp", "meta", "vhea", "vmtx", "VDMX", "LTSH", "hdmx"):
            if table in font:
                del font[table]
        font.save(str(normalized_path))
    finally:
        font.close()
    return normalized_path


def apply_font_identity(font_path: Path, family_name: str, style_name: str) -> None:
    font = TTFont(str(font_path))
    try:
        is_bold = "bold" in style_name.lower()
        is_italic = "italic" in style_name.lower()
        full_name = family_name if style_name == "Regular" else f"{family_name} {style_name}"
        postscript_name = safe_postscript_name(f"{family_name}-{style_name}")

        name_table = font["name"]
        name_table.names = [
            record
            for record in name_table.names
            if record.nameID not in (1, 2, 3, 4, 6, 16, 17)
        ]

        version = name_table.getDebugName(5) or "Version 1.000"
        unique_id = f"{version};{postscript_name}"
        set_name(name_table, 1, family_name)
        set_name(name_table, 2, style_name)
        set_name(name_table, 3, unique_id)
        set_name(name_table, 4, full_name)
        set_name(name_table, 6, postscript_name)
        set_name(name_table, 16, family_name)
        set_name(name_table, 17, style_name)

        if "OS/2" in font:
            os2 = font["OS/2"]
            os2.usWeightClass = 700 if is_bold else 400
            os2.fsSelection = set_flag(os2.fsSelection, 0, is_italic)
            os2.fsSelection = set_flag(os2.fsSelection, 5, is_bold)
            os2.fsSelection = set_flag(os2.fsSelection, 6, not is_bold and not is_italic)

        if "head" in font:
            head = font["head"]
            head.macStyle = set_flag(head.macStyle, 0, is_bold)
            head.macStyle = set_flag(head.macStyle, 1, is_italic)

        if "post" in font and not is_italic:
            font["post"].italicAngle = 0

        font.save(str(font_path))
    finally:
        font.close()


def normalize_style_name(style_name: str) -> str:
    normalized = (style_name or "Regular").strip()
    if normalized.lower() == "bold":
        return "Bold"
    if normalized.lower() in ("bolditalic", "bold italic"):
        return "Bold Italic"
    if normalized.lower() == "italic":
        return "Italic"
    return "Regular"


def set_name(name_table, name_id: int, value: str) -> None:
    for platform_id, encoding_id, language_id in ((3, 1, 0x409), (1, 0, 0)):
        name_table.setName(value, name_id, platform_id, encoding_id, language_id)


def set_flag(value: int, bit: int, enabled: bool) -> int:
    mask = 1 << bit
    return value | mask if enabled else value & ~mask


def format_codepoint(codepoint: int) -> str:
    return f"U+{codepoint:04X}"


def safe_postscript_name(value: str) -> str:
    return re.sub(r"[^A-Za-z0-9-]+", "", value) or "Font-Regular"


def safe_name(value: str) -> str:
    value = re.sub(r"[^A-Za-z0-9._-]+", "_", value.strip())
    value = value.strip("._-")
    return value or "Font"


if __name__ == "__main__":
    raise SystemExit(main())
