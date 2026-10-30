# Font Subset Exporter

Unity editor plugin for exporting one merged `.ttf` from input text and a fallback font list.

## Requirements

- Unity 2022.3 or newer.
- Python 3 available on the machine.
- `fonttools` installed in that Python environment.

Install manually:

```bash
python3 -m pip install --upgrade fonttools
```

On Windows, this also usually works:

```bat
py -3 -m pip install --upgrade fonttools
```

You can also open the Unity menu item and press `Install/Update fonttools`.

## Usage

1. Open `Tools > Font Subset Exporter`.
2. Enter or load the text whose characters should be kept.
3. Add fallback font files or a folder of fonts.
4. Order fonts from most preferred to least preferred.
5. Choose an output folder and file prefix.
6. Press `Export Single TTF`.

The exporter scans every unique code point in the text. Each character is assigned to the first enabled font that contains it. The exporter creates temporary per-font subsets, merges them, then writes one final `.ttf`.

## Notes

- macOS and Windows are both supported because Unity only launches Python/fonttools.
- For `.ttc` font collections, set `Collection Font Index` to choose which font face to export.
- A single universal font does not exist. Use Noto font families or platform fonts as a fallback list.
- The tool creates one merged subset font. It does not design new glyph shapes for characters missing from every source font.
- Merging glyphs from unrelated fonts can produce inconsistent visual style and metrics. For production UI, prefer sources from the same family, such as Noto Sans plus Noto Sans CJK.
- To keep merging reliable, temporary source subsets are normalized before merge by dropping optional layout/metadata tables such as `GSUB`, `GPOS`, vertical metrics, and `meta`. This is suitable for static glyph coverage, but complex-script shaping and advanced typography can be reduced.
- For Chinese/Japanese/Korean, add a real CJK font such as Noto Sans CJK, Source Han Sans, Microsoft YaHei, PingFang SC, or Meiryo.
