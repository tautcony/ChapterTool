## Scope

Three `gpt-6-luna` agents compared and updated separate Web surfaces.
The coordinator integrated shared candidate APIs, localization, and verification.
The first review missed invalid main-expression layout and toolbar history placement.
The earlier successful checks did not establish complete UI parity.

| Difference | Verified result |
| --- | --- |
| Load actions required a context click | A visible menu exposes Reload and Append MPLS |
| Main options were separated across tools | XML language, template loading, and numbering are available in the main window |
| Main preview covered expression changes only | One Core candidate combines naming, numbering, and an unapplied expression |
| Candidate application could disturb presentation | Selection and scroll position survive one atomic Apply |
| History showed coarse summaries | Root rows, typed parameters, field comparisons, filters, and direct-child redo choices are available |
| Settings activated only on Save | Valid drafts apply live; Save persists once; Cancel restores runtime preferences |
| Tool details differed from desktop workflows | Export validation, exact content comparisons, and browser log inspection retain drafts and failure feedback |
| Narrow and landscape controls lost readable width | Five viewport checks protect reachable actions, labels, and control widths |

Applied expressions are derived from typed operations on the current history branch and track.
Naming and numbering do not execute the same applied expression again.
Undo and Redo preserve this distinction.
The script remains available in the editor.

An open expression dialog retains the main candidate behind the modal.
Its independent draft does not change background values or cell geometry.

## Initial verification

| Check | Result |
| --- | --- |
| WASM compiled tests | 87 passed |
| Avalonia localization tests | 16 passed |
| Avalonia and Web public-API parity tests | 3 passed |
| Chromium full browser suite | 68 passed |
| Chromium after the final numeric-width adjustment | 14 passed |
| WebKit workflow regressions | 38 passed initially; the remaining case passed on isolated recheck |
| Release WASM publish | Passed |
| Browser TypeScript check | Passed |
| Generated locale check | Passed |
| OpenSpec strict validation | Passed |
| Whitespace check | Passed |

Primary commands:

```text
dotnet test tests/ChapterTool.Wasm.Tests/ChapterTool.Wasm.Tests.csproj --no-restore
dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj --no-restore --filter FullyQualifiedName~LocalizationTests
dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj --no-restore --filter FullyQualifiedName~PortableUiParityTests
dotnet publish src/ChapterTool.Wasm/ChapterTool.Wasm.csproj --configuration Release --no-restore --output artifacts/wasm-e2e/publish -p:WasmBuildNative=false -p:WasmRunWasmOpt=false
npm run prepare:site
npm run typecheck
node node_modules/@playwright/test/cli.js test --config=playwright.config.ts --project=chromium
uv run --project scripts scripts/axaml-to-json.py --check
openspec validate sync-web-ui-with-avalonia --strict
```

Run the browser commands from `tests/ChapterTool.Wasm.E2E/`.
Windows invocations pass arguments to `node.exe` as an array.
The publish options match the browser CI profile.

The first WebKit run timed out while waiting for the initial Load button to become actionable in the viewport capture case.
The same case passed without a code change on isolated recheck.
The cause is not established.
The initial failure trace and the successful recheck report are retained.

WebKit command:

```text
node node_modules/@playwright/test/cli.js test --config=playwright.config.ts --project=webkit modal-layout.spec.ts unified-editing.spec.ts layout-behavior.spec.ts main-window-parity.spec.ts history-parity.spec.ts portable-tools-parity.spec.ts settings-localization.spec.ts repeating-frame-display.spec.ts
node node_modules/@playwright/test/cli.js test --config=playwright.config.ts --project=webkit main-window-parity.spec.ts --grep "main candidate actions"
```

Browser reports:

- `artifacts/wasm-e2e/report-parity-complete-chromium/`
- `artifacts/wasm-e2e/report-parity-layout-chromium/`
- `artifacts/wasm-e2e/report-parity-complete-webkit/`
- `artifacts/wasm-e2e/report-parity-webkit-load-recheck/`

Reviewed main-window images:

- `artifacts/web-ui-parity-review/main-default.png`
- `artifacts/web-ui-parity-review/main-wide.png`
- `artifacts/web-ui-parity-review/main-narrow.png`
- `artifacts/web-ui-parity-review/main-short.png`
- `artifacts/web-ui-parity-review/main-landscape.png`

## User-reported layout correction

The Avalonia main expression row uses editor diagnostics and tooltips.
`MainView.axaml` keeps preview actions in the expression row.
`ExpressionEditor.axaml` defines a diagnostic popup outside normal layout.
The independent expression tool has a separate status row in its scrolling body.
That tool layout must not define the main-window diagnostic layout.
`HistoryToolView.axaml` owns Undo and Redo buttons.
The main toolbar exposes only the history entry.

The browser now uses an editor diagnostic overlay.
The overlay opens on hover or keyboard focus.
It appears above the browser editor to remain visible near the bottom viewport edge.
The editor exposes invalid state and the associated diagnostic text to accessibility APIs.
Pending and stale messages remain available without adding layout rows.
Stable preview action heights prevent status transitions from shrinking the grid.
The browser main toolbar no longer has Undo or Redo buttons.
The history dialog retains both actions.
Document shortcuts retain their existing editing boundaries.

The new browser regression failed before the fix.
Invalid syntax reduced the grid height by 64.78125 pixels at default and wide sizes.
It reduced the grid height by 82.78125 pixels at the narrow size.
The toolbar check also found the extra Undo button.
The first correction still changed action-row height during state transitions.
Its failed geometry assertions were retained.
The final correction makes those heights stable.

Correction evidence is stored in `artifacts/wasm-e2e/report-diagnostics-before/`.
The intermediate failure reports are retained under `report-diagnostics-after/` and `report-diagnostics-final/`.
The last intermediate failure used an incorrect expected label, `Refresh preview`.
The shared resource and rendered button use `Update preview`.
The test now verifies that existing label.

The desktop diagnostic hover test passed in its separate Headless process.
The initial command used unsupported `--filter` and ran no tests.
The successful command used the repository's xUnit filter option:

```text
dotnet test tests/ChapterTool.Avalonia.Headless.Tests/ChapterTool.Avalonia.Headless.Tests.csproj --no-restore --filter-method "*Expression_editor_opens_diagnostic_popup_when_pointer_hovers_a_diagnostic*"
```

The first complete correction run exposed a shortcut parameter bug.
The C# string array expanded into separate JS interop arguments.
The browser guard received `Ctrl+O` as a string and built a set of its characters.
Document Undo then ran together with native text Undo after a cell commit.
The reproduction changed `Opening` to `OpeningOpening`.
The fix passes the gesture array as one JS argument.
The regression verifies real keyboard event cancellation and exact document Undo/Redo results.
Field-level text Undo remains available.
Layout status assertions now target the status strip instead of matching two status regions.

A later shared-output publish produced incompatible assemblies during concurrent Core edits and builds.
Expression preview reported a missing `EditableChapterDocument` constructor.
That browser run was stopped after the mismatch was confirmed.
Verification now publishes an isolated source snapshot under `artifacts/web-ui-verification-source/`.
The snapshot includes current Core changes without editing them.
`artifacts/web-ui-verification-source-manifest.json` records each copied source hash.
The isolated publish uses its own `bin` and `obj` paths.
The published site is prepared with `CHAPTERTOOL_E2E_PUBLISH=artifacts/wasm-ui-isolated-publish/wwwroot`.

Final correction checks use the isolated publish:

| Check | Result |
| --- | --- |
| Chromium complete suite | 73 passed |
| WebKit main diagnostics, main options, history, and smoke workflows | 16 passed |
| Avalonia diagnostic hover interaction | 1 passed |
| Release isolated WASM publish | Passed |
| TypeScript check | Passed |
| OpenSpec strict validation | Passed |

The Chromium report is `artifacts/wasm-e2e/report-diagnostics-isolated-chromium/`.
The WebKit report is `artifacts/wasm-e2e/report-diagnostics-isolated-webkit/`.
Current `Home.razor` and `app.css` hashes match the tested snapshot.
Review images were refreshed from that Chromium run:

- `artifacts/web-ui-parity-review/diagnostic-default.png`
- `artifacts/web-ui-parity-review/diagnostic-wide.png`
- `artifacts/web-ui-parity-review/diagnostic-narrow.png`
- The five `main-*.png` images listed above.

The final browser commands are:

```text
node node_modules/@playwright/test/cli.js test --config=playwright.config.ts --project=chromium
node node_modules/@playwright/test/cli.js test --config=playwright.config.ts --project=webkit specs/main-diagnostics-layout.spec.ts specs/main-window-parity.spec.ts specs/history-parity.spec.ts specs/smoke.spec.ts
```

## Browser boundaries

Browser file input replaces local filesystem pickers.
Browser downloads replace native save paths.
Browser storage replaces the desktop settings file.
The browser does not start native media tools or enumerate system fonts.
Browser activity logs use actual browser records.
Native log directories remain a desktop capability.
