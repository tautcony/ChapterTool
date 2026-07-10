## 1. Unified Document Foundation

- [x] 1.1 Add the current typed settings document, schema-version constants, and source-generated JSON metadata.
- [x] 1.2 Implement the shared path-serialized document store with atomic writes, active-file corruption preservation, current-version loads, and future-version rejection.
- [x] 1.3 Implement ordered version upgrades and best-effort first-use import from the three predecessor settings files without deleting them.

## 2. Typed Store Integration

- [x] 2.1 Introduce the unified document store and preserve application, theme, and font normalization within the aggregate model.
- [x] 2.2 Update the Avalonia composition root to share one unified document store across settings consumers.

## 3. Verification Coverage

- [x] 3.1 Replace predecessor-file persistence assertions with unified layout, section round-trip, missing-file, malformed-active-file, and normalization tests.
- [x] 3.2 Add migration tests for complete and partial predecessor import, version-zero upgrade, current-version no-rewrite, invalid/future versions, and retained predecessor files.
- [x] 3.3 Add concurrent-save tests proving independently constructed typed stores preserve all unified sections and clean up temporary files.
- [x] 3.4 Update Avalonia composition tests that arrange persisted font settings to use the unified store behavior.

## 4. Documentation And Validation

- [x] 4.1 Update `docs/code-map/` settings ownership and test lookup guidance for the unified versioned document and migration pipeline.
- [x] 4.2 Run the Infrastructure tests, focused Avalonia tests, Avalonia app build, full solution tests, and strict OpenSpec validation.

## 5. Single Store Correction

- [x] 5.1 Remove section-store contracts and application, theme, and font store implementations; route every runtime consumer through `ISettingsStore<ChapterToolSettings>`.
- [x] 5.2 Add aggregate `UpdateAsync` and refactor isolated application-setting changes to one lock-scoped read and one write.
- [x] 5.3 Refactor settings UI and startup appearance loading so each workflow loads the aggregate once and saves or applies all child content together.
- [x] 5.4 Replace section-store tests and fakes with aggregate-store coverage that counts one load and one save for multi-section workflows.
- [x] 5.5 Update code maps and rerun focused builds/tests, full solution tests, and strict OpenSpec validation.
