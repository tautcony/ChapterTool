## Why

ChapterTool already persists several settings, but they are exposed through scattered controls and small tool windows rather than a single place users can review and maintain. A unified settings panel will make language, save defaults, external tool paths, and theme colors discoverable while keeping the main chapter editing surface focused.

## What Changes

- Add a Settings command and Avalonia settings panel/window that groups configuration by purpose.
- Include settings discovered from the current codebase:
  - General: UI language, default save directory, main window location reset.
  - External tools: MKVToolNix/mkvextract path, eac3to path, ffprobe path, and ffmpeg directory fallback used for ffprobe.
  - Output defaults: default save format and default XML chapter language.
  - Appearance: the existing six theme color slots.
  - Platform integration: Windows-only file association entry shown only when supported or clearly marked unavailable.
- Add browse, clear, reset-to-default, save/apply, and validation behavior for settings where it is useful.
- Keep settings limited to durable preferences. Current working values that are already edited frequently on the main screen, such as naming mode, template use, order shift, expression, frame-rate choice, and round-frames, stay on the main workflow surface rather than moving into Settings.
- Keep existing focused tools usable where they still make sense, but route language and color settings through the unified panel as the primary settings surface.
- Extend typed settings only where needed for durable defaults, specifically default save format and default XML language.
- Add tests for settings loading, validation, persistence, localization refresh, tool path precedence, and settings panel ViewModel behavior.

## Capabilities

### New Capabilities

### Modified Capabilities

- `avalonia-ui-shell`: The shell must expose a unified Settings command and settings panel for durable application, tool, appearance, and platform preferences without pulling high-frequency current workflow controls out of the main screen.
- `supporting-ui-platform-services`: Typed settings and platform services must support editable external tool paths, UI language, save defaults, appearance settings, validation, migration, and platform-gated integration entries.

## Impact

- Affected projects: `src/ChapterTool.Avalonia`, `src/ChapterTool.Infrastructure`, and shared settings/service contracts in `src/ChapterTool.Core` if needed.
- Affected tests: `tests/ChapterTool.Avalonia.Tests` for ViewModel/settings-panel behavior and `tests/ChapterTool.Infrastructure.Tests` for settings persistence/migration/tool locator behavior.
- Likely UI additions: `SettingsToolView`, `SettingsToolViewModel`, localized resource keys, and a `SettingsCommand`/window route.
- Likely model additions: persisted default save format and default XML language beyond the existing `AppSettings` fields, while preserving compatibility with legacy `chaptertool.json` and `color-config.json`.
- No chapter import/export file format changes are expected.
