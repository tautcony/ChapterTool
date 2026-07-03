## Context

The .NET rewrite exposes importers through `IChapterImporter` and resolves them by extension in the Avalonia registry. OGM `.txt` import is currently handled by `OgmChapterImporter`, so Premiere `.txt` detection must fit without breaking existing OGM behavior.

Premiere marker exports are tabular text rather than media containers. The parser can live in `ChapterTool.Core.Importing.Text` with no UI or infrastructure dependency.

## Goals / Non-Goals

**Goals:**
- Import Adobe Premiere Pro chapter marker lists from `.csv`.
- Auto-detect Premiere marker tables in `.txt` files before OGM parsing.
- Keep diagnostics structured and parser behavior deterministic.
- Support common delimiter and quoting patterns from exported marker tables.

**Non-Goals:**
- Export Premiere marker lists.
- Add UI-specific parsing logic or WinForms compatibility code.
- Infer a project frame rate from external metadata.

## Decisions

- Add a dedicated `PremiereMarkerListImporter` in Core.
  Rationale: Premiere parsing is a text format concern and should remain testable without Avalonia. Alternative considered: extend `OgmChapterImporter`, but that would mix unrelated formats and make failure diagnostics less clear.

- Add a `TextChapterImporter` dispatcher for `.txt`.
  Rationale: `.txt` can contain either Premiere marker tables or OGM chapter pairs. A dispatcher can try Premiere detection first and then fall back to OGM while preserving the existing OGM importer.

- Use a small CSV/TSV splitter rather than a new package.
  Rationale: the supported input is line-oriented marker exports with simple quoted fields, so a local parser avoids dependency churn. Alternative considered: external CSV library, but this feature does not need streaming, schema mapping, or advanced CSV dialect handling.

- Parse frame-based timecodes with a conservative common-frame-rate guess.
  Rationale: marker exports may include `HH:MM:SS:FF` or `HH;MM;SS;FF`. Without project frame-rate metadata, using common frame-rate ceilings matches the legacy patch behavior and keeps imports usable.

## Risks / Trade-offs

- Ambiguous `.txt` files could resemble marker tables -> Mitigation: require a marker-related header and a recognizable time column before treating content as Premiere data.
- Frame timecodes are approximate without explicit FPS -> Mitigation: document the parser behavior through tests and prefer millisecond timestamps when provided.
- Multiline quoted CSV fields are not supported -> Mitigation: Premiere marker lists are expected to be one marker per line; malformed rows are skipped unless no chapters can be parsed.
