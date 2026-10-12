# Verification Evidence

The initial audit was correct for tasks 5.6, 8.1, and 8.3.
Checked task boxes did not establish their completion.
Task 8.6 makes the full host CI and remote SHA checks conditional on a requested push.
No push was requested.

## Cross-Layer And Precision Coverage

`tests/ChapterTool.Core.Tests/Editing/ExactFrameRateWorkflowTests.cs` imports real MPLS and IFO fixtures.
It edits a fractional frame and checks the candidate before commit.
It commits the reviewed candidate through the session kernel.
It converts Rational to Approximate and back.
It checks stable chapter identities and source-rate metadata.
It checks QPFile output with independent integer arithmetic.
Three Undo operations restore the imported document.
Three Redo operations restore the reviewed document and identical output.

The same test file runs 500 cycles for each rate-kind pair.
Each cycle has four operations: rate conversion, multiplication, division, and reverse rate conversion.
The original timestamps include `1`, `12345678`, `863999999999`, and `9007199254740993` ticks.
The assertion checks loss against one original-rate frame after every cycle.
It uses integer cross multiplication instead of production conversion helpers.

| Source kind | Target kind | Maximum loss in ticks | Maximum loss in source frames |
| --- | --- | ---: | ---: |
| Rational | Rational | 196666 | 0.47152687312687313 |
| Rational | Approximate | 196666 | 0.47152687312687313 |
| Approximate | Rational | 166835 | 0.400003596 |
| Approximate | Approximate | 166835 | 0.400003596 |

`artifacts/exact-frame-rate-domain/precision-results.xml` contains the six passing test results and measured output.
These cases do not establish reversibility for arbitrary lossy operations or arbitrary Lua scripts.

## Rendered Workflows

`tests/ChapterTool.Avalonia.Headless.Tests/Headless/ExactFrameRateHeadlessTests.cs` opens a real desktop window.
It loads a real MPLS result into the window workflow.
It checks unchanged frame-editor commit, candidate display refresh, apply, Undo, and Redo.
The unchanged-editor regression reproduced an 833-tick loss before the fix.
`MainView` now compares the editor text with its initial text before dispatching a content edit.

`tests/ChapterTool.Wasm.E2E/specs/exact-frame-rate-domain.spec.ts` uses actual file input, cell editors, and downloads.
It uses real browser key events for document and field Undo.
It checks explicit-rate editing on Unknown content.
It checks pending and stale candidates and three-language selector refresh.
Frame preview values use two rows to keep decimal digits within the frame column.
The test checks available width and no line wrapping.
Time previews use separate rows for before, candidate, and delta values.
Each complete time value must stay on one line inside its cell.
The browser boundary assertion reproduced overflow with the previous published layout.

The Luna subagent added `tests/ChapterTool.Wasm.Tests/WasmFrameRateChoiceTests.cs`.
Its language regression reproduced a hard-coded Auto label before the fix.
The workspace now reads the existing `Main.AutoFrameRate` translation key.
The test checks selection, ticks, rate authority, and history during language changes.
Custom values use the public Core rate service because the workspace has no public arbitrary-rate import API.
The desktop Headless test verifies custom Rational and Approximate selection through the rendered ComboBox.

Review images are under `artifacts/exact-frame-rate-domain/`.
Desktop images use `en-US/`, `zh-CN/`, and `ja-JP/` subdirectories.
Each desktop language has `desktop-default.png`, `desktop-wide.png`, `desktop-narrow.png`, and `desktop-editor.png`.
Web images use `web-default.png`, `web-wide.png`, and `web-narrow.png`.
The matching `web-*-time.png` images show the complete time column.
Additional Web images record empty, invalid, stale, and localized states.
Existing `main-diagnostics-layout.spec.ts`, `layout-behavior.spec.ts`, and desktop Headless tests cover stateful geometry and accessible controls.
Screenshots support visual review and do not replace assertions.

Two existing browser assertions depended on synthetic 24/1 authority for text imports.
They now verify Unknown time-only evaluation and explicit all-row display.
One preliminary browser run overlapped site preparation and had transient resource 404 errors.
The final run must use a stable prepared site.

## Public Documentation

`src/ChapterTool.Core/README.md` documents canonical rates, retained major-version-23 boundaries, the invalid numeric assignment exception, Auto policy, mixed-rate limits, and rounding.
`src/ChapterTool.CommandLine/README.md` documents `--frame-rate-ratio`, mutual exclusion, diagnostics, imported authority, and Lua limits.
`packages/chaptertool/README.md` documents `tool.exact`, complete wire examples, migration, nullable duration, segments, overrides, and rounding boundaries.
`docs/code-map/core.md`, `docs/code-map/avalonia.md`, and `docs/code-map/testing.md` identify canonical ownership and primary regressions.

## Checks

The final .NET solution run uses separate sequential test processes:

```text
dotnet test ChapterTool.slnx --no-restore --max-parallel-test-modules 1 --timeout 10m
```

The final solution run passed 1622 tests and skipped two of 1624 tests.
The skips cover an opt-in full disc check and a non-Windows registry check.
The final Headless project has 147 passing tests.
The final Web workspace project has 90 passing tests.

The maintained CI entry point ran these checks:

- `python scripts/ci/run.py --stage resources`: resource generation drift, Python lint, 47 runner tests, and PowerShell validation passed.
- `python scripts/ci/run.py --stage node --step build-node-js-package`: optimized runtime and TypeScript declarations built.
- `python scripts/ci/run.py --stage node --step type-check-node-js-package --step test-built-node-js-package`: types and 45 package tests passed after the build.
- `python scripts/ci/run.py --stage pack-node`: installed tarball consumer passed.
- `python scripts/ci/run.py --stage pack-nuget`: net8.0/net9.0/net10.0 Core packages and installed Core/CLI consumers passed.
- Browser restore, Release publish, dependency installation, Pages preparation, and engine installation ran through `scripts/ci/run.py --stage browser` step selection.
- `python scripts/ci/run.py --stage browser --browser-suite chromium --step type-check-browser-tests --step run-chromium-e2e`: browser types and all 76 Chromium tests passed against the final prepared site.

`openspec validate exact-frame-rate-domain --strict` passed.
`openspec validate --all` passed all 45 items.

The final exact workflow suite passed all nine tests across Chromium, Firefox, and WebKit.
It ran through `npm run test:e2e -- exact-frame-rate-domain.spec.ts` against the prepared Release site.

Full default host CI and remote job verification remain conditional on a future push request.
This change has not been pushed or archived.
