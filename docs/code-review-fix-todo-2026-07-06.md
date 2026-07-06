# Code Review Fix TODO - 2026-07-06

Source report: `docs/code-review-2026-07-06.md`

## Rules

- Fix one issue at a time.
- Run focused tests after each fix.
- Commit each completed fix separately.
- Do not include unrelated working tree changes, especially the pre-existing `scripts/publish.sh` modification.

## High Priority

- [x] Fix WebVTT import so cue end times and duration are preserved.
- [x] Remove command-string path interpolation from `ShellService.OpenTerminalAsync`.
- [x] Complete Windows file association registry behavior so registration writes an open command and unregistration protects existing associations.
- [ ] Add or explicitly defer a non-primary settings/command surface for file association.
- [x] Prevent preview from opening as an empty stub when no chapters are loaded.

## Medium Priority

- [x] Remove the fake injectable parser from `CueChapterImporter`.
- [x] Remove fake async behavior from `IfoChapterImporter` and handle `request.Content` consistently.
- [x] Remove the Infrastructure dependency from Core tests.
- [x] Validate external tools as executables, not just existing files.
- [x] Preserve and surface corrupt settings files instead of silently resetting.
- [x] Return or log shell service failures instead of swallowing them.
- [x] Clarify whether `FfmpegPath` means ffmpeg or ffprobe directory and validate accordingly.
- [x] Localize native file picker titles and file type labels.
- [x] Add accessible names for icon-only buttons.
- [ ] Refactor BDMV parsing so stdout is not passed through diagnostics.

## Low Priority

- [ ] Move production test-double services out of Infrastructure or mark them test-only.
- [ ] Replace fixed `Task.Delay` in `UiCommandTests`.
- [ ] Remove sync-over-async from Matroska integration setup.
- [ ] Strengthen screenshot tests with layout/content assertions.
- [ ] Handle quoted MKVToolNix `DisplayIcon` registry values.
- [ ] Hide eac3to export process windows unless visibility is required.
