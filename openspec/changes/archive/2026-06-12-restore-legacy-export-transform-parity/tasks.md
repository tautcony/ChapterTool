## 1. Core Compatibility Baseline

- [x] 1.1 Add focused tests that capture legacy-compatible timestamp rounding for half-millisecond and frame-boundary cases.
- [x] 1.2 Document and preserve the legacy timestamp rounding policy while centralizing new frame-number rounding helpers.
- [x] 1.3 Add regression tests proving existing TXT/XML/QPF/timecode-style exports preserve the documented rounding behavior.

## 2. XML Export Parity

- [x] 2.1 Add Core XML export tests for XML declaration, legacy-compatible preamble/comment guidance, formatted output, valid non-trivial UIDs, and language preservation.
- [x] 2.2 Update the Matroska XML exporter to emit formatted legacy-compatible XML without hard-coding `EditionUID=1` and `ChapterUID=chapter.Number` as the default behavior.
- [x] 2.3 Preserve an explicit deterministic-ID option only if needed by existing tests or callers, and document the default user-facing behavior in code-level options.

## 3. ChangeFps Transform

- [x] 3.1 Add Core tests for frame-preserving ChangeFps time recalculation across source and target FPS values.
- [x] 3.2 Add Core tests for ChangeFps duration/end-time recalculation when chapter end data exists.
- [x] 3.3 Implement a UI-independent ChangeFps service/result API with structured diagnostics for invalid FPS input.
- [x] 3.4 Wire ChangeFps into the existing transform/export workflow surface where frame-rate conversion is expected.

## 4. Celltimes Conversion

- [x] 4.1 Add Core tests for celltimes output containing one integer frame per non-separator chapter start.
- [x] 4.2 Add tests for separator skipping, invalid FPS diagnostics, and rounding-edge behavior.
- [x] 4.3 Implement a UI-independent celltimes conversion service and output writer.
- [x] 4.4 Expose celltimes through a compact Avalonia command or tool entry that delegates to the conversion service.

## 5. Chapter2Qpfile Conversion

- [x] 5.1 Add Core tests for converting valid OGM chapter text to QPF entries.
- [x] 5.2 Add Core tests for timecode-backed frame mapping and invalid chapter/timecode diagnostics.
- [x] 5.3 Implement the Chapter2Qpfile conversion service using existing import/parsing and QPF formatting contracts where possible.
- [x] 5.4 Expose Chapter2Qpfile through an auxiliary Avalonia conversion tool or existing text tool path.

## 6. XML Language Selection

- [x] 6.1 Add a Core or application-level ISO XML language catalog with stable codes and display names, including quick defaults `und`, `zh`, `ja`, and `en`.
- [x] 6.2 Add validation tests for accepted and rejected XML language codes.
- [x] 6.3 Update the Avalonia XML language selector to allow selecting or entering less common ISO codes without cluttering the main workflow.
- [x] 6.4 Add focused Avalonia ViewModel/UI tests that valid selected codes are passed to XML export and invalid codes surface localized diagnostics.

## 7. Verification

- [x] 7.1 Run `openspec validate restore-legacy-export-transform-parity --strict`.
- [x] 7.2 Run focused Core and Infrastructure tests covering export, transform, and conversion behavior.
- [x] 7.3 Run focused Avalonia tests after UI command/language selector changes.
- [x] 7.4 Run `dotnet test ChapterTool.Avalonia.slnx --no-restore` before finalizing broader implementation.
