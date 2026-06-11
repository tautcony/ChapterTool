## 1. Avalonia Localization Infrastructure

- [x] 1.1 Add `ChapterTool.Avalonia.Localization` types for supported cultures, localized message values, resource lookup, formatting, and culture-change notification.
- [x] 1.2 Add Simplified Chinese, English, and Japanese Avalonia resource dictionaries with a stable key namespace and deterministic Simplified Chinese fallback.
- [x] 1.3 Register one shared localization manager in `AppCompositionRoot` and load the persisted language before the main window renders.
- [x] 1.4 Update settings handling so blank legacy language values behave as Simplified Chinese and new saves persist `zh-CN`, `en-US`, or `ja-JP`.
- [x] 1.5 Remove or isolate the historical dictionary-based `ILocalizationService` path from Avalonia UI, status, and log rendering.

## 2. UI Resource Migration

- [x] 2.1 Replace localized main-window literals in `MainWindow.axaml` with `DynamicResource` keys, including buttons, labels, menu items, tooltips, DataGrid headers, and option captions.
- [x] 2.2 Replace localized literals in tool views with `DynamicResource` keys, including preview, log, color settings, language, expression, template names, zones, and forward-shift surfaces.
- [x] 2.3 Localize `AvaloniaWindowService` titles and placeholder text through the shared localization manager.
- [x] 2.4 Update the language tool to list Simplified Chinese, English, and Japanese with localized display names and stable culture tags.
- [x] 2.5 Verify open windows refresh localized text when the active language changes where Avalonia dynamic resources support runtime refresh.

## 3. Prompts, Status, Diagnostics, and Logs

- [x] 3.1 Replace hard-coded `StatusText` assignments in Avalonia ViewModels with semantic localized message keys and formatting arguments.
- [x] 3.2 Replace ad hoc log string interpolation with structured log entries that store message keys, arguments, timestamps, and optional technical details.
- [x] 3.3 Add formatting so the log window renders existing entries in the active UI language and refreshes after language changes.
- [x] 3.4 Add diagnostic-code mapping for known user-facing diagnostics while preserving raw diagnostic messages and details for troubleshooting.
- [x] 3.5 Keep paths, external process output, exception text, and imported media metadata as raw technical values rather than translating them.
- [x] 3.6 Replace user-facing prompt text for dialogs, confirmations, unsupported-feature placeholders, empty states, and command feedback with localized message keys.

## 4. Tests and Verification

- [x] 4.1 Add resource completeness tests that assert all supported cultures contain the same required keys and compatible format placeholders.
- [x] 4.2 Add UTF-8/mojibake tests for Chinese and Japanese resource text.
- [x] 4.3 Add ViewModel tests for localized prompt/status messages, language persistence, and fallback behavior.
- [x] 4.4 Add log service/window tests proving existing entries format in the newly selected language after a language switch.
- [x] 4.5 Add focused Avalonia tests or compiled-XAML coverage for localized `DynamicResource` usage without raw `.axaml` string assertions as the primary validation.
- [x] 4.6 Run `dotnet test tests\ChapterTool.Avalonia.Tests\ChapterTool.Avalonia.Tests.csproj --no-restore`.
- [x] 4.7 Run `dotnet test ChapterTool.Avalonia.slnx --no-restore`.
