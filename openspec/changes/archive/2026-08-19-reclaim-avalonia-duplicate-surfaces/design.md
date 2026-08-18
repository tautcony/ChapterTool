## Context

The Avalonia shell already has a typed `IWorkspaceToolSession`, but `MainWindowViewModel` and `MainWindowToolSession` also expose the concrete `MainWindowPortAdapters`. Tests use that compatibility path even though the narrow interfaces contain every required member. Settings editing keeps saved and draft values in the ViewModel and coordinates several flags with `SettingsAppearanceViewModel`. Log projection code shares a file with subscription and filtering code. These shapes are internal and have no external compatibility requirement.

The change must preserve the existing workspace session contract, settings document format, live-apply policy, localization refresh, tool validation, and log rendering behavior. The repository has unrelated untracked `libbluray/` content that remains untouched.

## Goals / Non-Goals

**Goals:**

- Keep `IWorkspaceToolSession` as the only session access surface for secondary tools and tests.
- Remove forwarding-only path helpers and the tracked Finder metadata file.
- Move pure log projection types and helpers into a focused source file.
- Give settings saved and draft snapshots one lifecycle owner with explicit load, save, reset, discard, and live-apply transitions.
- Retain the current public properties, commands, persistence behavior, and test coverage.

**Non-Goals:**

- Do not change user-facing workflows, localization strings, settings schema, or package contents beyond removing ignored metadata.
- Do not add a new logging service, settings persistence layer, or dependency.
- Do not remove the `MainWindowPortAdapters` implementation types when they are still required to construct the session; only their concrete exposure is removed.

## Decisions

### Use the narrow session as the only access route

`MainWindowToolSession` will construct and retain one `MainWindowPortAdapters` instance, then expose its adapter members through the existing narrow interface properties. `MainWindowViewModel` will retain only `IWorkspaceToolSession ToolSession`. Tests will obtain `Expression`, `Preferences`, `ExportPreferences`, `NamingPreferences`, and `ChapterEdit` from `ToolSession`.

This keeps adapter lifetime window-scoped and removes the second representation. It is preferred over constructing each adapter directly in tests because that would bypass the production composition path.

### Normalize paths at the owning boundary

Settings property setters will call `ChapterSavePath.CleanOptionalPath` directly through one shared helper where a helper is still needed for property semantics. `PreferenceSinkAdapter` will normalize `SavingPath` directly with the Core API. The ViewModel forwarding method and duplicate `CleanDirectory` name will be removed.

### Separate log projection by file ownership

`LogEntryViewModel`, `LogStructuredNodeViewModel`, `LogPropertyViewModel`, and the private JSON/tree projection helpers will move to `LogEntryViewModel.cs` in the same namespace. `LogToolViewModel.cs` will retain only filter options, log subscription, collection synchronization, selection, clipboard, and disposal logic. No collection or service is duplicated.

### Centralize settings snapshot lifecycle

Add an internal `SettingsSnapshotCoordinator` that stores distinct `Saved` and `Draft` `ChapterToolSettings` snapshots and owns the `LiveApplyEnabled`, `IsApplyingSnapshot`, and load-failure lifecycle transitions. The coordinator will expose deterministic operations for:

- loading a normalized snapshot or defaults;
- updating the draft from ViewModel fields;
- comparing draft and saved snapshots;
- committing a saved snapshot after persistence;
- beginning and ending a discard/reset snapshot application.

The ViewModel remains the Avalonia binding owner. It will translate between bindable fields and the coordinator draft through existing `CurrentSettings`/`Apply...` methods, and it will continue to coordinate `SettingsAppearanceViewModel`. The coordinator will not merge saved and draft values, and it will not own localization, commands, tool discovery, or appearance services.

### Verify by behavior at existing boundaries

Update tests to drive the surviving interfaces and settings workflows. Keep log projection tests against the moved types. Add focused coordinator tests only for snapshot transitions if existing ViewModel tests do not cover them. Run package checks, Avalonia unit tests, Avalonia Headless tests, and the relevant build/type checks sequentially.

## Risks / Trade-offs

- [Concrete adapter removal could break an uncompiled internal caller] -> Search the full repository, migrate all tests, and retain the typed `IWorkspaceToolSession` members with the same concrete implementations.
- [Snapshot extraction could change live-apply or discard timing] -> Keep the existing `ApplyCurrentAppSettingsToOwner` and appearance application order, and run the existing load, save, reset, discard, and failure tests.
- [Moving log projection code could break XAML type resolution] -> Preserve the namespace and type names, and run log unit and Headless tool tests.
- [Ignoring `.DS_Store` could hide a file outside the package] -> Use a root rule that ignores Finder metadata while leaving tracked source files explicit; verify the package file list with `npm run pack:verify`.

## Migration Plan

1. Create the OpenSpec artifacts and record the implementation tasks.
2. Apply metadata, adapter, path, log, and settings changes in separate reviewable batches.
3. Run focused tests after each Avalonia batch, then run package checks and the broader solution validation.
4. Rollback requires reverting only this change; no persisted data or migration is introduced.

## Open Questions

None. The requested compatibility path removal and behavior-preserving trade-offs are explicit.
