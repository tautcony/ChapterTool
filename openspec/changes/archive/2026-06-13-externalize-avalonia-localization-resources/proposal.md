## Why

Avalonia localization currently stores translated UI, prompt, status, and log strings in C# dictionaries, which mixes content with implementation and makes translation review, resource tooling, and future localization maintenance harder than necessary. The app should use a standard .NET resource format so localized text is data, not source code.

## What Changes

- Move Avalonia localization strings out of C# dictionaries into culture-specific `.resx` resource files.
- Keep the existing `IAppLocalizer` and runtime culture-switching behavior, but load values through .NET resource infrastructure instead of hard-coded dictionaries.
- Preserve Simplified Chinese fallback behavior and supported culture tags `zh-CN`, `en-US`, and `ja-JP`.
- Update localization tests so they validate compiled resources and placeholder compatibility without asserting over source files.

## Capabilities

### New Capabilities

### Modified Capabilities
- `supporting-ui-platform-services`: Avalonia localization resources must be externally maintained resource assets loaded through .NET resource infrastructure rather than embedded C# translation dictionaries.

## Impact

- Affected code: `src/ChapterTool.Avalonia/Localization`, Avalonia project resource items, and localization tests.
- No user-facing language or key changes are intended.
- No Core API changes are required.
