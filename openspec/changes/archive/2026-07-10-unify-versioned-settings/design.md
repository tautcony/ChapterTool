## Context

`AppSettingsStore`, `ThemeSettingsStore`, and `FontSettingsStore` currently duplicate JSON load/save, temporary-file, corruption, caching, and locking behavior while writing separate files. They are consumed through typed store contracts by the Avalonia composition root, settings ViewModel, main-window settings flow, and external-tool locator. The change must preserve narrow typed boundaries, avoid Avalonia dependencies in Infrastructure, and handle three section saves that can occur back-to-back or concurrently.

Configuration is user-owned local data. Migration should recover known values when practical, preserve source files for manual recovery, and prefer an explicit failure over destructive downgrade when a document was written by a newer application.

## Goals / Non-Goals

**Goals:**

- Make `settings.json` the sole active configuration document with `schemaVersion`, `application`, `theme`, and `font` top-level properties.
- Provide a reusable document-level store so future settings areas become sections rather than new persistence implementations.
- Upgrade known older unified shapes through ordered, testable version steps and import the three current predecessor files on first use.
- Load and save all settings sections as one aggregate so a workflow that changes multiple sections performs one read and one file write.
- Preserve unrelated sections during isolated updates and serialize read-modify-write operations per canonical settings path.
- Keep malformed active-file preservation and typed runtime normalization behavior.

**Non-Goals:**

- Inferring arbitrary historical or future structures, downgrading newer documents, or guaranteeing lossless migration of unknown data.
- Deleting or renaming predecessor files after import.
- Migrating the intentionally unsupported `theme-colors.json` six-slot format.
- Changing UI settings controls, theme semantics, or font resolution.

## Decisions

### Use a typed current document with a JSON-DOM upgrade boundary

The current persisted model is `ChapterToolSettings` with `SchemaVersion`, `Application`, `Theme`, and `Font` properties, serialized with the existing source-generated JSON context. Loading first parses a `JsonNode`/`JsonObject`, reads the version, applies ordered `vN -> vN+1` transforms, and only then deserializes the current typed document.

Current schema version is `1`. A unified document with no `schemaVersion` is treated as version `0`; the `0 -> 1` step validates that section-shaped content can be retained and stamps version `1`. This creates the version pipeline now without inventing a second obsolete public model. Future structural changes add one explicit transform and increment the current version.

The alternative was direct typed deserialization with optional properties. That handles additive fields but cannot express renames, moves, or reject future versions reliably. A DOM is confined to migration; consumers remain typed.

### Expose one aggregate store without section stores

`ChapterToolSettingsStore` is the only persistence implementation and implements `ISettingsStore<ChapterToolSettings>`. Application, theme, font, and future categories are child properties of `ChapterToolSettings`; `AppSettingsStore`, `ThemeSettingsStore`, `FontSettingsStore`, and `ISettingsSectionStore<TSection>` do not exist.

Consumers receive the aggregate store directly. A settings-screen load calls `LoadAsync` once and distributes the returned child values to UI fields. Saving constructs one updated aggregate and calls `SaveAsync` once, regardless of how many child sections changed. Application startup loads once and applies theme and font from the same snapshot. CLI and external-tool lookup select `Application` from an aggregate load.

`ISettingsStore<TSettings>` also exposes `UpdateAsync(Func<TSettings, TSettings>)` for isolated changes such as language or last-used save directory. The concrete store acquires the canonical-path lock, reads the latest aggregate once, applies the transform, and writes once. This prevents the read-then-save race that would otherwise lose concurrent changes.

`ChapterToolSettingsStore` caches the normalized aggregate together with the active file's last-write timestamp and length. Repeated loads through the shared runtime store return that snapshot without reopening or reparsing unchanged JSON. A changed file stamp invalidates the snapshot and triggers one fresh parse; successful writes refresh the cache immediately.

### Import predecessor files only when the unified file is absent

Under the same path lock, the first load or save checks for `settings.json`. If absent, it independently attempts to read:

- `appsettings.json` into `application`
- `theme-settings.json` into `theme`
- `font-settings.json` into `font`

Valid sections are normalized and combined with defaults for missing or malformed sections. If at least one predecessor file parses successfully, the store persists a version-1 unified document. It leaves every predecessor file untouched. If no predecessor exists, a read returns defaults without creating `settings.json`; the first save creates it. If predecessor files exist but none are usable, load returns defaults without creating a misleading successful migration.

Malformed predecessor files are skipped rather than moved to `.corrupt` because they are recovery inputs, not the active document. This is the “best effort” boundary: one bad predecessor section does not block import of the other two, and source data remains available for manual intervention.

### Treat future versions as unsupported, not corrupt

When `schemaVersion` is greater than the supported version, the backend throws `UnsupportedSettingsVersionException`, derived from `IOException`, and never writes or preserves the file as corrupt. Existing application fallback paths already handle I/O failures. A later aggregate save or update re-reads the document and fails the same way, preventing a settings screen that loaded defaults from overwriting newer data.

Missing, negative, non-integral, or otherwise invalid version metadata is treated as malformed active configuration and follows corrupt-file preservation. Known older versions are upgraded in memory and written in current form after a successful migration, because completing an explicit version step is different from ordinary normalization on read.

### Preserve only modeled current sections

Version-1 writes serialize the current typed document. Unknown properties are not promised round-trip preservation. This keeps the contract bounded and makes structural migrations deterministic. Future sections must be added to the document model and serializer context. Forward protection comes from rejecting newer schema versions, not from carrying unknown fields indefinitely.

## Risks / Trade-offs

- [A process crash between temporary-file creation and move leaves a temp file] -> Unique names prevent collision; failed operations best-effort delete their own temp file, and the active file remains intact.
- [Separate store instances race and lose another section] -> A shared canonical-path semaphore covers each aggregate save or update operation, including import.
- [A malformed predecessor section is silently omitted] -> Import is explicitly best-effort, leaves all predecessor files untouched, and tests verify other valid sections survive.
- [Version-0 detection could accept an unrelated JSON object] -> The migrator requires at least one recognized section and rejects invalid version metadata; the normal legacy import handles predecessor filenames separately.
- [Older application versions cannot read the new file] -> Predecessor files are retained, allowing rollback to use their last values; changes made only in `settings.json` after migration will not flow backward.
- [Static path locks accumulate for many test directories] -> The number is bounded in production to the settings roots used by the process; a concurrent dictionary is acceptable for the small test/process lifetime.

## Migration Plan

1. Ship the unified backend and adapters with schema version 1.
2. On first access, import independently valid predecessor sections and write `settings.json`; do not delete predecessor files.
3. Use only `settings.json` for all subsequent reads and writes.
4. For a future schema change, increment `CurrentSchemaVersion`, add a contiguous DOM transform, and add fixtures covering the prior shape, partial recoverability, idempotent current loads, and future-version refusal.
5. Rollback can restore the earlier binary; retained predecessor files provide pre-migration values, while `settings.json` remains ignored by that binary.

## Open Questions

None. The filename, version policy, import scope, and forward-version behavior are fixed by this change.
