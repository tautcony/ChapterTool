## 1. Shortcut Domain Model

- [x] 1.1 Add stable shortcut action identifiers, default gesture metadata, display-resource keys, and an immutable active-mapping model in the shared contracts/UI-independent layer; verify the catalog contains save, reload, log, preview, and clip-selection defaults with deterministic ordering.
- [x] 1.2 Implement gesture parsing, normalization, reserved-key validation, duplicate detection, unknown-action filtering, and draft-to-active conversion; verify focused unit tests cover valid gestures, empty bindings, malformed gestures, reserved keys, and conflicts.
- [x] 1.3 Add shortcut overrides to `ChapterToolSettings`, normalization, JSON serialization, and schema-upgrade handling while preserving unrelated sections; verify settings contract/store tests load old documents with catalog defaults and round-trip customized mappings without losing theme or font data.

## 2. Runtime Routing

- [x] 2.1 Refactor `ShortcutRouter` to consume the active shortcut mapping and dispatch by stable action identifier instead of a hard-coded gesture switch; verify existing default shortcut tests still invoke the same commands.
- [x] 2.2 Wire active-mapping updates from the preference/settings session into the main-window ViewModel and router, including removal of replaced gestures; verify a changed gesture invokes only its newly assigned command and the old gesture no longer does so.

## 3. Settings Shortcut Tab

- [x] 3.1 Extend `SettingsToolViewModel` with observable shortcut rows, draft values, validation messages, per-row reset, reset-all, dirty-state tracking, save, and discard integration with `SettingsSnapshotCoordinator`; verify ViewModel tests cover valid edits, reset isolation, conflict blocking, save failure rollback, and discard.
- [x] 3.2 Add a dedicated shortcuts Tab to `SettingsToolView.axaml` using responsive row layout, accessible names, gesture capture controls, validation visuals, and reset actions; verify compiled bindings build and Headless view construction finds the Tab and all action rows.
- [x] 3.3 Bind main-window menu `InputGesture` and shortcut hints to the active mapping rather than literals while preserving visible defaults; verify the rendered menu updates after a saved customization and still exposes all primary actions.

## 4. Localization and Resources

- [x] 4.1 Add localized labels, action names, validation errors, reset commands, and shortcut-tab text to every supported Avalonia locale; verify resource keys resolve in English, Chinese, and Japanese without mojibake.
- [x] 4.2 Regenerate locale JSON artifacts with `uv run --project scripts scripts/axaml-to-json.py` and run `uv run --project scripts scripts/axaml-to-json.py --check`; verify no generated locale diff is left uncommitted by the change.

## 5. Automated Verification

- [x] 5.1 Add non-Headless catalog, parser, conflict, settings-normalization, and router tests in the owning Avalonia or contracts test projects; verify with the affected `dotnet test ... --no-restore` commands.
- [x] 5.2 Add Avalonia Headless workflow coverage that opens the real settings view, records a custom gesture, saves it, reopens settings, checks the updated menu gesture, and triggers the target command; verify with `dotnet test tests/ChapterTool.Avalonia.Headless.Tests/ChapterTool.Avalonia.Headless.Tests.csproj --no-restore`.
- [x] 5.3 Add Headless scenarios for duplicate/invalid gesture rejection and discard/reset behavior, using the runner UI thread and deterministic `RunJobs` state; verify failed saves leave the prior active mapping unchanged.
- [x] 5.4 Run the affected non-Headless and Headless test projects sequentially, then run `openspec validate "customize-keyboard-shortcuts" --strict`; verify all tests and artifact validation pass without disabling assembly parallelization.

## 6. Documentation and Delivery

- [x] 6.1 Update `docs/code-map/avalonia.md` and `docs/code-map/testing.md` with shortcut ownership, settings-tab entry points, persistence boundaries, and Headless test locations; verify the documentation uses short active ASD-STE100 sentences and real repository paths.
- [x] 6.2 Review the change against every scenario in the three delta specs and record any intentional platform limitations in implementation notes or tests; verify the final OpenSpec status reports proposal, specs, design, and tasks as complete.
