## Context

The current BDMV importer locates eac3to and parses `-showall` output to discover playlists, but then reads the `.mpls` files directly for chapters. Real eac3to supports exporting the chapter track from a selected title as OGM-style chapter text, which better matches the existing requirement that BDMV import delegates chapter text through the eac3to adapter.

The implementation must keep tests hermetic. Local eac3to installations and local disc folders are useful for manual verification only, not test data or spec content.

## Goals / Non-Goals

**Goals:**

- Use eac3to to enumerate BDMV titles and export chapter text for chapter-bearing candidates.
- Parse exported text through the existing OGM parser path while preserving BDMV metadata such as playlist source name, title, duration, frame rate, and media references where possible.
- Return structured diagnostics for missing dependency, invalid structure, list failure, export failure, unrecognized output, and no parsed chapters.

**Non-Goals:**

- Do not add bundled eac3to binaries or new runtime dependencies.
- Do not store user-specific eac3to paths or sample disc paths in OpenSpec artifacts, source, or automated tests.
- Do not redesign generic importer routing or the settings UI.

## Decisions

- Keep eac3to process calls inside `BdmvChapterImporter`.
  Rationale: BDMV chapter extraction is the only current caller, and the existing importer already owns eac3to discovery and process diagnostics. A separate service can be extracted later if another feature needs eac3to.

- Use `eac3to <root> -showall` for candidate discovery, then `eac3to <root> "<index>)" "1:<temp file>"` for chapter export.
  Rationale: verified eac3to output exposes the chapter track as track `1:` for BDMV title analysis and writes OGM-style chapter text. This keeps stdout parsing limited to title discovery and leaves chapter parsing to the existing OGM importer.

- Create temporary chapter files per candidate and delete them after reading.
  Rationale: eac3to writes chapter export to a file path rather than stdout. Temporary files avoid user-visible artifacts and keep process invocation close to eac3to's native contract.

- Use MPLS parsing as metadata enrichment, not the chapter source of truth.
  Rationale: eac3to-exported chapter text satisfies the adapter requirement, while MPLS metadata remains useful for FPS, duration fallback, source references, and disc titles.

- Pass optional load progress through `ChapterImportRequest` from the Avalonia load service.
  Rationale: eac3to BDMV import may enumerate, export, and parse several candidates, so the existing progress bar needs importer-level stage updates instead of only start/end states.

## Risks / Trade-offs

- eac3to output varies across versions or unusual discs -> Keep parsing tolerant, fail with diagnostics when no candidates can be recognized, and cover representative formats with fake-runner tests.
- Chapter track number may differ for non-standard eac3to outputs -> Start with track `1:` because BDMV title analysis identifies chapters as track 1; return export diagnostics if that contract fails.
- Exporting every chapter-bearing playlist can be slower than direct MPLS parsing -> Limit export to candidates that advertise chapters and have a corresponding playlist; preserve a per-import timeout.
- Progress is best-effort and importer-specific -> Clamp UI progress below completion until the load result is applied, then keep success/failure state authoritative.
