## Why

Application, theme, and font preferences currently use three independently managed JSON files, which duplicates persistence behavior and makes each new settings area add another file and migration path. A single versioned document gives future configuration changes one stable boundary where known older shapes can be upgraded on a best-effort basis without promising fully automatic migration for every historical or future structure.

## What Changes

- Replace `appsettings.json`, `theme-settings.json`, and `font-settings.json` as active persistence targets with one `settings.json` document containing `application`, `theme`, and `font` sections.
- Add a top-level integer `schemaVersion`, a current-version contract, and ordered best-effort upgrade steps for known older unified-document shapes.
- On first use, best-effort import valid sections from the three existing files when no unified document exists, then persist the current unified shape without deleting the source files.
- Preserve defaults for missing or unusable legacy sections, preserve malformed active unified files through the existing corrupt-file mechanism, and refuse to overwrite documents whose schema version is newer than the running application supports.
- Expose only `ISettingsStore<ChapterToolSettings>` as the persistence boundary; application, theme, font, and future settings remain child content of that aggregate rather than separate stores or adapters.
- Load the aggregate once per settings workflow, reuse one cached aggregate while the file is unchanged, and commit all changed child content with one file write; isolated updates use one lock-scoped read-modify-write operation.

## Capabilities

### New Capabilities
- `versioned-settings-document`: Defines the unified settings document, section-safe persistence, schema-version handling, best-effort upgrades, and import from the three predecessor files.

### Modified Capabilities
- `supporting-ui-platform-services`: Application and theme preferences move from independent files to sections in the unified versioned settings document while preserving typed, injectable stores and runtime fallbacks.
- `theme-preset-management`: Theme preset identity is persisted in the unified document's theme section and predecessor theme settings are imported on first use.
- `font-settings-management`: Font family identities are persisted in the unified document's font section and predecessor font settings are imported on first use.

## Impact

- Infrastructure configuration models, JSON source generation, the whole-document store contract, corrupt-file handling, and concurrency control.
- Avalonia composition, ViewModels, CLI, and tool discovery consume the same aggregate settings store instead of section-specific stores.
- Infrastructure and composition tests must cover unified layout, first-run import, version upgrades, future-version rejection, malformed-file preservation, and concurrent section saves.
- Settings ownership and test lookup documentation under `docs/code-map/` changes to point to the unified store and migration pipeline.
