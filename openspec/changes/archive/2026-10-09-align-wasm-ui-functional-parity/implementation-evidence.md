# Implementation evidence

The portable changes and approved shared corrections are implemented.
Final solution and browser acceptance checks pass.
Platform differences remain outside this change.

## D01-D12 coverage

| Difference | Evidence |
| --- | --- |
| D01 | Naming, template, and numbering use draft, preview, Apply, and Cancel. Workspace tests cover cancellation, stale candidates, repeated submission, and one-step history. P01 and P08 cover the rendered workflow. |
| D02 | Settings and display changes preserve committed names, format, and history. Frame information is a derived projection in both hosts. P01 and P07 verify preference isolation. |
| D03 | FPS review shows the selected track's source and target rates, scope, and typed differences. Apply commits the captured candidate. Rational conversion uses consistent boundary quantization. P10 covers single-track and multi-track sources. Matching host fixtures verify Apply, Undo, and Redo. |
| D04 | Signed shifts use displayed FPS. Core rejects the whole candidate for negative times or duration violations. Zero preserves existing numbering and does not commit. Out-of-range values do not commit. Core, workspace, and matching host tests cover these cases. |
| D05 | Frame cells use Core parsing and captured chapter identity. Source FPS zero uses the detected display rate. Invalid, unchanged, and stale edits do not commit. P02 and matching host tests verify accepted edits. |
| D06 | Both numbering controls use 0..1000. Web rejects negative and fractional drafts. The rendered Avalonia integer-input regression passes. |
| D07 | Lua files use bounded UTF-8 reads. Shared authoring services provide highlighting, completion, and positioned diagnostics. Revision guards reject obsolete results. P04 and B20-B25 cover editing, cancellation, rapid input, and composition. |
| D08 | Settings use the shared shortcut catalog. Routing and hints use one active mapping. Validation includes browser restrictions, fixed aliases, synthesized Meta aliases, and F9. P03 and P12 cover configuration, command prerequisites, row commands, and clip bounds. |
| D09 | Settings compare normalized drafts. Save validates the aggregate before persistence and runtime activation. Failure retains active settings and the draft. P03, P07, and P11 cover discard, reset, storage failure, integer bounds, and runtime rollback. |
| D10 | Logs support filtering, explicit details, copy, clear, and captured JSON/CSV export. Workspace tests cover Unicode, multiline quoting, live append, and bounded eviction. P05 and P09 cover rendered actions and recovery. |
| D11 | Shared locale resources contain English, Chinese, and Japanese labels. Generated Web resources pass validation. XML choices show translated names and stable codes. P01, B09, and resource tests cover the labels. |
| D12 | Cell drafts support Enter, Tab, blur, Escape, and local Undo. Surviving identities retain selection. P02 verifies the edit lifecycle. P06 edits and exports the last of 1000 rows and checks five viewport sizes. |

## Approved shared corrections

- Avalonia frame refresh must not commit display preferences to content history.
- Frame cells and signed shifts must use displayed FPS in both hosts.
- Expression preparation can supply detected FPS to the selected track when source FPS is absent.
- Track duration must cover all retained segments. Reviewed conversion must update document bounds and preserve other tracks.
- Document and segment boundaries use the same frame quantization. Segment duration equals converted end minus converted start.
- Conversion preserves segment identities and source FPS metadata.
- CUE output uses the current track's media source in both hosts.

`PortableUiParityTests` runs matching naming, Unicode rename, frame edit, signed shift, expression, conversion, and history sequences.
It checks content fields, segment metadata, scope, transaction counts, and all nine export formats.
Eight formats use exact UTF-8 byte comparison.
XML comparison excludes generated `ChapterUID` and `EditionUID` values. It compares all remaining XML content.
The exporter already generates new random UIDs for each serialization. This change preserves that behavior.

## Checks

- `dotnet test ChapterTool.slnx --no-restore --max-parallel-test-modules 1`: 1405 pass, 2 expected skips, 0 failures.
- Web unit tests: 64 pass.
- Avalonia Headless tests: 106 pass in a separate process. Screenshot capture also passes.
- Release Web publish: passes with no warnings or errors.
- Browser TypeScript checking: passes.
- Single-track and multi-track FPS review: 6 pass across Chromium, Firefox, and WebKit. Source comparison uses the importer's six-decimal precision. Target comparison requires `25/1`.
- `npm run test:e2e` against the prepared Release site: 139 pass, 2 expected screenshot-only skips, 0 failures across Chromium, Firefox, and WebKit.
- Browser report: `artifacts/wasm-e2e/report-functional-parity-final/index.html`. JUnit results: `artifacts/wasm-e2e/junit-functional-parity-final.xml`.
- Locale generation and `--check`: pass.
- OpenSpec strict validation and `git diff --check`: pass.
- Linux visual baseline comparison is not run on this Windows host.
- Physical mobile keyboards are not verified by browser emulation.
- No change is archived or pushed. Deferred platform work and unrelated changes remain untouched.

## Screenshots

- Browser tools at 1280x800, 1920x1080, 390x844, 390x640, and 844x390: `artifacts/wasm-functional-parity/screenshots/chromium/`, `firefox/`, and `webkit/`. Each engine has 20 images.
- Avalonia numbering at default, wide, and narrow sizes: `artifacts/unify-sourcegit-design-system/wasm-functional-parity/main-default.png`, `main-wide.png`, and `main-narrow.png`.

Screenshots supplement behavior checks. They are not automated assertions.
