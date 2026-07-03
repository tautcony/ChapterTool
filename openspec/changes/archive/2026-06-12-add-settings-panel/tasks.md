## 1. Settings Model and Persistence

- [x] 1.1 Extend `AppSettings` with durable default fields for save format and XML language.
- [x] 1.2 Update `AppSettingsStore` tests so old `appsettings.json` and legacy `chaptertool.json` continue loading when new fields are absent.
- [x] 1.3 Add persistence tests for MKVToolNix/mkvextract, eac3to, ffprobe, ffmpeg fallback, save directory, UI language, and the two output defaults.
- [x] 1.4 Keep `ThemeColorSettings` and `ThemeSettingsStore` as the source for the six appearance color slots and add coverage for saving through the new settings flow.

## 2. Settings Services and Validation

- [x] 2.1 Add a small settings validation model for external tool status values such as found, missing, invalid path, and unsupported.
- [x] 2.2 Reuse `IExternalToolLocator` or shared locator path-expansion rules to validate configured executable and directory values for `mkvextract`, `eac3to`, and `ffprobe`.
- [x] 2.3 Add clear/reset behavior that removes configured external tool overrides and restores PATH/platform discovery.
- [x] 2.4 Add picker abstractions or request hooks needed by the settings ViewModel for directories and executable files.

## 3. Settings ViewModel

- [x] 3.1 Create `SettingsToolViewModel` that loads `AppSettings` and `ThemeColorSettings` into editable grouped properties.
- [x] 3.2 Implement save/apply/reset commands for general, external tool, output default, and appearance settings.
- [x] 3.3 Apply runtime-safe settings to the owning main ViewModel, including UI language, save directory, default save format, default XML language, and appearance where supported.
- [x] 3.4 Add localized status and validation messages for save success, reset, invalid paths, missing tools, and unsupported platform integration.
- [x] 3.5 Add focused ViewModel tests for load, edit, save, reset, clear path, validation, and localization refresh behavior.

## 4. Avalonia UI Integration

- [x] 4.1 Add `SettingsToolView.axaml` with grouped sections or tabs for general, external tools, output defaults, appearance, and platform integration.
- [x] 4.2 Route a new `"settings"` window through `AvaloniaWindowService` with a dedicated view and ViewModel.
- [x] 4.3 Add `SettingsCommand` to `MainWindowViewModel` and expose it from the main shell through a compact command entry.
- [x] 4.4 Keep existing language and color commands compatible while making Settings the primary persistent preferences surface, and leave high-frequency current workflow controls on the main screen.
- [x] 4.5 Add or update localization resource keys for all settings labels, buttons, validation statuses, and section names across supported UI languages.

## 5. Verification

- [x] 5.1 Run `dotnet test tests\ChapterTool.Infrastructure.Tests\ChapterTool.Infrastructure.Tests.csproj --no-restore`.
- [x] 5.2 Run `dotnet test tests\ChapterTool.Avalonia.Tests\ChapterTool.Avalonia.Tests.csproj --no-restore`.
- [x] 5.3 Run `dotnet build src\ChapterTool.Avalonia\ChapterTool.Avalonia.csproj --no-restore`.
- [x] 5.4 Capture default, wide, and narrow settings-panel screenshots under `artifacts/` for visual verification.
- [x] 5.5 Run `openspec validate add-settings-panel --strict`.
