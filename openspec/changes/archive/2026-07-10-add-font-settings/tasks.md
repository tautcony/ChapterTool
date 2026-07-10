## 1. Typed Font Settings And Persistence

- [x] 1.1 Add `FontSettings` with independent UI and monospace family values, stable empty/default representations, and a shared default instance.
- [x] 1.2 Implement `FontSettingsStore` for `font-settings.json` with cached loads, trimmed canonical values, atomic replacement saves, and existing corrupt-file preservation behavior.
- [x] 1.3 Register `FontSettings` in `AppJsonSerializerContext` source generation without adding Avalonia dependencies to Infrastructure.
- [x] 1.4 Add Infrastructure tests for missing-file defaults, non-default round trips, stable default values, trimming, malformed-file preservation, and no write during load.

## 2. System Font Catalog And Runtime Resolution

- [x] 2.1 Define `IFontFamilyCatalog` and immutable option/value types that expose canonical installed-family names independently from localized default labels.
- [x] 2.2 Implement the Avalonia system-font catalog using `FontManager.Current.SystemFonts`, filtering blank values, deduplicating case-insensitively, snapshotting once, and returning deterministic culture-aware ordering.
- [x] 2.3 Add a fixed/test catalog and unit coverage for duplicate removal, ordering inputs, explicit category defaults, and resolution that does not depend on host-installed fonts.
- [x] 2.4 Implement independent UI and monospace resolution so blank or unavailable values fall back only their own category and the monospace default chain ends in generic `monospace`.
- [x] 2.5 Preserve canonical font identity while lazily resolving installed-family display names by exact active culture, same language, then canonical fallback.

## 3. Semantic Font Application

- [x] 3.1 Add `ChapterTool.UiFontFamily` and `ChapterTool.MonospaceFontFamily` default application resources before any window is created.
- [x] 3.2 Define `IFontApplicationService` and implement `AvaloniaFontApplicationService` to resolve settings through the catalog and replace both semantic resources on the Avalonia UI thread.
- [x] 3.3 Ensure font application updates existing dynamic-resource consumers and preserves the other category when only one configured family is unavailable.
- [x] 3.4 Add application-service tests for defaults, independent valid selections, independent unavailable-family fallback, generic monospace fallback, and replacement of previously applied resource values.

## 4. Composition And Settings Lifecycle

- [x] 4.1 Construct one font store, catalog, and application service in `AppCompositionRoot`; apply defaults synchronously and load persisted values asynchronously with I/O, access, and corrupt-file fallback.
- [x] 4.2 Pass the same font dependencies through `AvaloniaWindowService` into each `SettingsToolViewModel` so startup and Settings use one runtime state boundary.
- [x] 4.3 Extend `SettingsToolViewModel` with UI/monospace option collections, canonical selected values, effective preview metadata, and normalized saved snapshots.
- [x] 4.4 Load font settings without writing, resolve unavailable saved values to visible defaults, and avoid marking Settings dirty solely because fallback occurred.
- [x] 4.5 Apply each selection immediately without changing the other category or writing the store, and include both categories in `HasUnsavedChanges` and close-confirmation behavior.
- [x] 4.6 Extend Save, Reset, full Discard, and appearance-only Discard so font snapshots are persisted or restored consistently with theme settings.
- [x] 4.7 Refresh localized default labels and preview metadata on culture changes while retaining both canonical selected family names.

## 5. Appearance UI And Localization

- [x] 5.1 Add independent UI-font and monospace-font selectors to the Appearance tab below the theme preset controls using responsive grid sizing, virtualized installed-family item templates, and per-family option rendering.
- [x] 5.2 Add stable-size non-editable previews containing Latin letters, digits, punctuation, and localized text; bind each preview to its effective family and localized accessible name.
- [x] 5.3 Add Simplified Chinese, English, and Japanese resources for selector labels, category defaults, preview samples, accessible names, and any font-load/fallback status shown to users.
- [x] 5.4 Verify long family names and localized labels trim or wrap without overlapping controls at default, wide, and narrow settings-window widths.
- [x] 5.5 Left-align the UI and monospace selector/preview groups with the theme controls and add rendered-coordinate coverage.

## 6. Semantic Font Consumer Migration

- [x] 6.1 Apply the semantic UI resource through window inheritance and targeted text-bearing control/popup styles without overriding the explicit family used by icon-library controls.
- [x] 6.2 Replace hard-coded monospace families in Settings and reusable XAML surfaces with a shared monospace style or dynamic resource reference.
- [x] 6.3 Update the code-created AvaloniaEdit `ExpressionEditor` to resolve `ChapterTool.MonospaceFontFamily` dynamically and refresh an existing editor after runtime changes.
- [x] 6.4 Update `TextToolView` content and generated line-number controls to use the semantic monospace resource while preserving current size, line height, wrapping, and syntax colors.
- [x] 6.5 Inspect remaining expression, script, preview, log, chapter/time, and tool surfaces; migrate only intentionally fixed-width content and leave normal UI on the UI resource.
- [x] 6.6 Apply the semantic monospace resource to chapter-table data cells and editing controls while keeping column headers on the UI resource; add Headless runtime-switch coverage for both roles.
- [x] 6.7 Apply the semantic monospace resource to the chapter-number shift numeric control while keeping its label on the UI resource; extend runtime-switch coverage.

## 7. Behavioral Verification

- [x] 7.1 Add Settings ViewModel tests for option ordering, category independence, live apply, Save, Reset, Discard, dirty state, unavailable saved families, load failures, and runtime language refresh.
- [x] 7.2 Add startup/composition coverage proving defaults exist before asynchronous load, persisted values replace defaults, and load failures keep the main shell usable.
- [x] 7.3 Add Avalonia Headless workflows that select distinct UI and monospace families and verify existing normal text, expression editor, and text preview surfaces resolve and refresh the correct effective families.
- [x] 7.4 Extend Headless workflows to verify default previews and accessible names, Save/Discard outcomes, unavailable-family fallback, and icon visibility after runtime font changes.
- [x] 7.5 Keep new tests behavior-based and place every class containing `[AvaloniaFact]` or `[AvaloniaTheory]` in `AvaloniaHeadlessTestCollection`.
- [x] 7.6 Add catalog and Settings ViewModel coverage for localized family-name preference, canonical selection stability, culture refresh, and canonical fallback.

## 8. Documentation And Final Validation

- [x] 8.1 Update `docs/code-map/infrastructure.md`, `docs/code-map/avalonia.md`, and `docs/code-map/testing.md` with font settings ownership, runtime wiring, semantic resources, consumers, and high-signal tests.
- [x] 8.2 Run `openspec validate "add-font-settings" --strict`.
- [x] 8.3 Build `src/ChapterTool.Avalonia/ChapterTool.Avalonia.csproj --no-restore` after resource, composition, and view changes.
- [x] 8.4 Run `dotnet test tests/ChapterTool.Infrastructure.Tests/ChapterTool.Infrastructure.Tests.csproj --no-restore`.
- [x] 8.5 Run `dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj --no-restore` after Infrastructure tests complete.
- [x] 8.6 Run `dotnet test ChapterTool.Avalonia.slnx --no-restore` before finalizing the change; do not run solution test processes in parallel.
- [x] 8.7 Capture manual Settings screenshots at default, wide, and narrow sizes under `artifacts/` and verify previews, long family names, icons, and controls do not overlap.
- [x] 8.8 Re-run strict OpenSpec validation and the full solution tests after adding chapter-table cell typography.
- [x] 8.9 Re-run strict OpenSpec validation and the full solution tests after localized font-family display and alignment updates.
