## Why

Adobe Premiere Pro can export chapter marker lists as CSV/tabular text, but the rewrite currently only imports OGM-style `.txt` and other existing chapter formats. Users need those marker exports to load as chapter sets without manually converting them first.

## What Changes

- Add an importer for Adobe Premiere Pro chapter marker list files.
- Support `.csv` files directly and detect Premiere marker lists in `.txt` files before falling back to OGM parsing.
- Parse common comma, semicolon, and tab separated marker exports with quoted fields.
- Preserve chapter marker names, fall back to comments/descriptions when names are blank, and ignore non-chapter marker rows when marker type is present.
- Document Adobe Premiere Pro marker list support in the supported formats list.

## Capabilities

### New Capabilities

### Modified Capabilities
- `chapter-importers-text-xml-matroska-vtt`: Add Adobe Premiere Pro chapter marker list import behavior to the text importer capability.

## Impact

- Affects core text importers and importer registry extension resolution.
- Adds focused parser coverage in `tests/ChapterTool.Core.Tests`.
- Updates supported format documentation.
