## Why

The Avalonia rewrite currently has mixed Chinese and English UI, prompt, and log text, which makes the app feel inconsistent and makes future language support hard to maintain. This change introduces a proper i18n path for user-facing UI, prompts, and application log messages so Simplified Chinese, English, and Japanese are first-class supported languages.

## What Changes

- Add an Avalonia-native localization component that supports Simplified Chinese (`zh-CN` or blank/default), English (`en-US`), and Japanese (`ja-JP`) resources without extending the legacy `ILocalizationService` model.
- Replace hard-coded user-facing UI strings in Avalonia views, ViewModels, tool windows, dialogs, prompts, status text, and command labels with localization keys.
- Localize user-facing application log messages through message keys and formatting arguments while preserving structured diagnostic data for tests and troubleshooting.
- Localize user-facing prompts, including confirmation messages, error dialogs, empty-state text, placeholder guidance, unsupported-feature notices, and command feedback.
- Add language selection behavior that applies at runtime where practical and persists the selected language through settings.
- Add coverage to catch missing resource keys, untranslated visible strings, mojibake, and language-specific UI/log behavior.

## Capabilities

### New Capabilities

### Modified Capabilities
- `avalonia-ui-shell`: Visible UI text, commands, dialogs, prompts, status messages, and secondary tool windows must use localized resources across Chinese, English, and Japanese.
- `supporting-ui-platform-services`: Localization services, settings, resources, prompts, and application log formatting must support the three target languages consistently.

## Impact

- Affected projects: `src/ChapterTool.Avalonia`, `src/ChapterTool.Core`, `src/ChapterTool.Infrastructure`.
- Affected tests: `tests/ChapterTool.Avalonia.Tests`, `tests/ChapterTool.Infrastructure.Tests`, and focused ViewModel/resource tests.
- Likely additions: Avalonia resource dictionaries or generated resource accessors, an Avalonia-facing localization manager, log message key/argument handling, and packaged Avalonia resources.
- No breaking CLI or file format changes are expected.
