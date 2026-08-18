## 1. Repository metadata and path forwarding

- [x] 1.1 Delete the tracked `packages/chaptertool/.DS_Store` file and add a root `.gitignore` rule for `.DS_Store` metadata.
- [x] 1.2 Remove `SettingsToolViewModel.CleanDirectory` and use `ChapterSavePath.CleanOptionalPath` for save-directory normalization.
- [x] 1.3 Remove `MainWindowViewModel.NormalizeConfiguredDirectory` and normalize preference-sink save paths directly with `ChapterSavePath.CleanOptionalPath`.
- [x] 1.4 Search for deleted helper names and confirm no production or test references remain.

## 2. Session adapter surface

- [x] 2.1 Change `MainWindowToolSession` to retain concrete adapters privately while exposing only the existing `IWorkspaceToolSession` members.
- [x] 2.2 Remove `MainWindowViewModel.PortAdapters` and its constructor assignment while preserving the `ToolSession` facade.
- [x] 2.3 Migrate Avalonia unit and Headless tests from `PortAdapters` to the corresponding `ToolSession` interfaces.
- [x] 2.4 Search the repository for `PortAdapters` and confirm only the internal construction relationship remains.

## 3. Log projection ownership

- [x] 3.1 Move `LogEntryViewModel`, `LogStructuredNodeViewModel`, `LogPropertyViewModel`, and their pure JSON/tree projection helpers to `LogEntryViewModel.cs` in the same namespace.
- [x] 3.2 Keep `LogToolViewModel.cs` focused on log service subscription, incremental synchronization, filtering, selection, clipboard, and disposal without changing collection behavior.
- [x] 3.3 Run log projection and log tool unit tests plus the Avalonia Headless log tool tests.

## 4. Settings snapshot coordinator

- [x] 4.1 Add an internal `SettingsSnapshotCoordinator` that stores distinct saved and draft `ChapterToolSettings` values and owns live-apply, snapshot-application, and load-failure lifecycle flags.
- [x] 4.2 Refactor `SettingsToolViewModel` to synchronize bindable fields with the coordinator draft and delegate load, save, reset, discard, and unsaved-change comparisons without changing appearance or localization ownership.
- [x] 4.3 Preserve settings load failure fallback, live application, save confirmation, reset, discard, and appearance rollback behavior through the coordinator.
- [x] 4.4 Add or update focused tests for coordinator transitions and existing `SettingsToolViewModel` behavior, including load failure, live apply, save, reset, discard, and appearance rollback.

## 5. Verification and documentation

- [x] 5.1 Update the applicable `docs/code-map/` entry if the new coordinator or log projection file changes ownership or lookup paths.
- [x] 5.2 Run `npm run typecheck`, `npm run test:built`, and `npm run pack:verify` in `packages/chaptertool`.
- [x] 5.3 Run `dotnet test tests\\ChapterTool.Avalonia.Tests\\ChapterTool.Avalonia.Tests.csproj --no-restore`.
- [x] 5.4 Run `dotnet test tests\\ChapterTool.Avalonia.Headless.Tests\\ChapterTool.Avalonia.Headless.Tests.csproj --no-restore`.
- [x] 5.5 Run `dotnet build src\\ChapterTool.Avalonia\\ChapterTool.Avalonia.csproj --no-restore` and inspect `git diff --check` plus final symbol searches.
