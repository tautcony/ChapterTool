# Verification

## Automated checks

- `dotnet test ChapterTool.slnx --no-restore`: passed 1,353 tests. Four environment-gated tests were skipped. The two Matroska integration tests need `mkvextract`. The full-disc parity test needs `CHAPTERTOOL_RUN_FULL_DISC_PARITY=1`. The Windows registry test is not applicable on Windows.
- `dotnet test tests/ChapterTool.Core.Tests/ChapterTool.Core.Tests.csproj --no-restore`: passed 732 tests, including history scale measurements.
- `npm test` in `packages/chaptertool`: passed lint, type checking, build, and 28 tests across four files.
- `npm run doctor` and `npm run pack:verify` in `packages/chaptertool`: passed. The environment has .NET SDK 10.0.401. The `wasm-tools` workload is absent, so the build uses the unoptimized WebAssembly runtime. Vitest reported a missing source-map source file for a generated runtime bundle.
- `openspec validate unified-editing-09-remove-legacy-paths --strict`: passed.
- `openspec validate --all`: 33 items passed. The existing `log-tool-viewing` spec failed strict validation because requirement 7 has no `SHALL` or `MUST` keyword. This spec is outside this change and was not modified.

The repository has Node.js tests for the browser package but no browser automation project or configured browser test command. This verification did not exercise the WebAssembly package in a graphical browser.

## Screenshots

Headless capture passed with `CHAPTERTOOL_UI_SCREENSHOT_SET=unified-editing-09` and the `Capture_main_window_and_tool_views_when_requested` test.

- Default: `artifacts/unify-sourcegit-design-system/unified-editing-09/main-default.png`
- Wide: `artifacts/unify-sourcegit-design-system/unified-editing-09/main-wide.png`
- Narrow: `artifacts/unify-sourcegit-design-system/unified-editing-09/main-narrow.png`

The chapter grid and bottom options remain visible at all three widths. The narrow capture shows the edit history panel clipped vertically at the 512-pixel test height. The changed content view does not add overlapping controls. Screenshots show visual layout only; accessibility names and keyboard behavior are covered by existing UI tests.
