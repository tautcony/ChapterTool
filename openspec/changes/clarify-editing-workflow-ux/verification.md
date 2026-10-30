## Verification date

2026-10-30.

## Results

| Check | Result |
| --- | --- |
| Avalonia ViewModel and service tests | 302 passed |
| Avalonia Headless tests | 173 passed |
| Web workspace tests | 96 passed |
| Published Release Chromium workflows | 82 passed |
| Browser TypeScript check | Passed |
| Generated locale JSON check | Passed |
| OpenSpec strict validation | Passed |
| Git whitespace check | Passed |

The final Chromium run includes the new real-key shortcut workflow. It verifies remapped open and save gestures, Command aliases, browser default cancellation, and native field undo. It also covers exact and unknown frame rates, source and target review, localization, narrow layouts, and export outcomes.

The close tests exercise Cancel, Discard, and Export through a rendered window. They verify failed and canceled exports, other unexported tracks, repeated close requests, and edits before the queued close. The frame review tests verify that a template candidate replaces the conversion summary.

The follow-up UI pass removes the frame-accuracy information icon and persistent explanation. The integer-frame control carries concise hover guidance. Desktop conversion details appear in the conversion button tooltip while a conversion candidate is pending. Bottom expression actions use one right-aligned group and remain vertically aligned with the editor.

Follow-up checks passed: 17 frame-rate and UI design Headless tests, 96 ViewModel tests, and the Chromium hover/conversion workflow. The default, wide, and narrow desktop conversion images were refreshed. The tests verify tooltip content, absence of the standalone icon, visible conversion label, and review text, and right/baseline alignment of the bottom action group. The conversion button retains its accessible action name.

The expression action height correction sets the visible text actions to the editor's 32 px height. The conversion preview Headless workflow passes with explicit height equality assertions for Apply and Discard.

All desktop chapter headers now use centered horizontal content alignment. The UI design system Headless tests verify the alignment on rendered column headers.

## Primary commands

Run these commands from the repository root. Browser commands use Node.js 22.

```sh
dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj --no-restore
CHAPTERTOOL_CAPTURE_WORKFLOW_UX=1 dotnet test tests/ChapterTool.Avalonia.Headless.Tests/ChapterTool.Avalonia.Headless.Tests.csproj --no-restore
dotnet test tests/ChapterTool.Wasm.Tests/ChapterTool.Wasm.Tests.csproj --no-restore
python3 scripts/ci/run.py --stage browser --browser-suite chromium --step publish-release-wasm --step prepare-pages-shaped-site --step type-check-browser-tests
CI=1 python3 scripts/ci/run.py --stage browser --browser-suite chromium --step run-chromium-e2e
uv run --project scripts scripts/localization/axaml-to-json.py --check
openspec validate clarify-editing-workflow-ux --strict
git diff --check
```

## Visual evidence

`artifacts/clarify-editing-workflow-ux/desktop/` contains 21 PNGs. Eighteen images cover empty, normal, valid, invalid, pending, and stale states at default, wide, and narrow sizes. Three images show frame conversion review at those sizes. The tests check geometry separately from screenshot generation.

The browser report is `artifacts/wasm-e2e/report/index.html`. The final JUnit report is `artifacts/wasm-e2e/junit.xml`. Browser review images also use `artifacts/exact-frame-rate-domain/` and `artifacts/expression-preview/`.

## Intentional host differences

Desktop close can await an export decision. Browser refresh cannot await that desktop workflow. Browser history remains local to its current tab session.

The desktop window retains its supported minimum width of 760 pixels. At phone widths, the Web chapter table can scroll horizontally inside its own viewport. This preserves complete time and frame values.

This change does not add history persistence. It does not implement the separate export-preview redesign or a new editing toolbar. Existing dependency edits remain outside this change.
