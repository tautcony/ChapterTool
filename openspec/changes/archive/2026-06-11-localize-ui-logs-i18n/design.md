## Context

The current localization abstraction is a small dictionary-based `ILocalizationService` that was carried forward from the historical implementation. It can switch `CurrentUICulture`, but it is not shaped around Avalonia binding, `ResourceDictionary` lookup, runtime resource refresh, typed language choices, or the fact that most current strings live directly in `.axaml`, `MainWindowViewModel`, and `AvaloniaWindowService`.

Avalonia has no WinForms-style `ApplyResources` path, so the new design should treat localization as a presentation-layer concern. Core should continue to emit stable diagnostic codes and raw technical details; the Avalonia layer should decide how those codes become localized UI, prompts, status, and log text.

## Goals / Non-Goals

**Goals:**
- Support Simplified Chinese, English, and Japanese as first-class application UI languages.
- Replace scattered hard-coded UI, prompt, status, dialog, window title, and log strings with resource keys.
- Use Avalonia-friendly runtime resource updates so static XAML labels can refresh through bindings/resources.
- Keep Core independent from UI language selection and Avalonia resource mechanics.
- Preserve diagnostic codes and technical details for troubleshooting while localizing user-facing text.

**Non-Goals:**
- Localizing chapter file contents, imported chapter names, media metadata, external tool stdout/stderr, or exported chapter language codes.
- Recreating the legacy WinForms resource model.
- Adding machine translation or runtime editing of translation files.
- Requiring every low-level Core diagnostic message to become fully localized in the first pass when it is only technical detail; user-facing summaries must be localized.

## Decisions

1. Use an Avalonia-side localization manager instead of extending the historical `ILocalizationService`.

   Add a new `ChapterTool.Avalonia.Localization` component, for example `IAppLocalizer` plus `AppLocalizationManager`, owned by the Avalonia composition root. It exposes supported cultures, the current culture, a culture-changed event or observable notification, and lookup methods such as `GetString(key)` and `Format(key, args)`.

   Rationale: Avalonia UI strings need resource dictionary updates and binding refresh, which the current Core service does not model. Keeping the service in the Avalonia project prevents Core from depending on presentation language behavior.

   Alternative considered: expand `ChapterTool.Core.Services.ILocalizationService`. This keeps a familiar interface but continues the legacy shape and pushes UI resource concerns into a shared layer that should not know about Avalonia.

2. Store UI resources as Avalonia resource dictionaries with a stable key namespace.

   Add resource dictionaries such as:
- `Assets/Localization/Strings.zh-CN.axaml`
- `Assets/Localization/Strings.en-US.axaml`
- `Assets/Localization/Strings.ja-JP.axaml`

   `AppLocalizationManager` loads the selected dictionary into `Application.Current.Resources.MergedDictionaries`, keeps a deterministic fallback to Simplified Chinese, and exposes the same lookup table for code-side formatting. XAML uses `DynamicResource` for static text, headers, tooltips, menu items, and window labels.

   Rationale: `DynamicResource` is the closest fit for runtime Avalonia resource replacement and avoids binding every static label to ViewModel properties. The same key namespace can still serve code-side text.

   Alternative considered: `.resx` plus generated accessors only. This is strong for code but weaker for XAML runtime refresh unless wrapped by additional binding infrastructure.

3. Model prompts, status, and log entries as localized message keys with arguments.

   Replace ad hoc string interpolation at UI/prompt/log call sites with a small message model, for example `LocalizedMessage(string Key, IReadOnlyDictionary<string, object?> Args, string? TechnicalDetail = null)`. ViewModels set status via message keys, create dialog/prompt requests through localized message objects, and log through localized log methods such as `Log(key, args, technicalDetail)`.

   The log service stores timestamp, message key, arguments, and optional technical detail. Formatting happens when the log window asks for text, using the current localizer. This allows log text to update after a language change without losing troubleshooting details.

   Rationale: storing already-rendered English or Chinese strings makes runtime language switching impossible and mixes diagnostic data with display text.

   Alternative considered: localize at every `Log("...")` call before storing. This is simpler but freezes log language at write time and makes language-switch tests brittle.

4. Use localized diagnostic summaries at the Avalonia boundary.

   Core and Infrastructure importers may continue returning `ChapterDiagnostic` with stable `Code`, `Severity`, `Message`, `Location`, and `Details`. Avalonia adds a formatter that maps known diagnostic codes to resource keys, falls back to the diagnostic message for technical details, and includes raw details in logs where useful.

   Rationale: localizing the whole Core diagnostic surface in one pass would create broad churn. Mapping at the UI boundary gives users localized summaries while retaining precise technical information.

5. Persist language as an explicit culture tag.

   The settings value should use `zh-CN`, `en-US`, or `ja-JP`. Blank legacy language values migrate to `zh-CN` behavior without writing an invalid culture. The language tool presents localized display names and applies changes through the localization manager first, then persists settings.

   Rationale: explicit tags make resource selection predictable and add Japanese without overloading blank/default semantics.

## Risks / Trade-offs

- [Risk] Some XAML surfaces may not refresh if they use `StaticResource`, literal `Content`, or literal `Header`. → Mitigation: require `DynamicResource` for localized XAML text and add tests/static checks for remaining visible literals.
- [Risk] Formatting resources can drift between languages. → Mitigation: add tests that every supported culture has the same key set and that each format string accepts the required arguments.
- [Risk] Log entries that include paths, external tool output, or exception text can become unreadable if fully translated. → Mitigation: localize the message envelope and keep raw technical detail separate.
- [Risk] Replacing prompt/status strings with message keys can disturb existing ViewModel tests. → Mitigation: test semantic message keys where possible and add focused language-specific assertions at the formatting boundary.
- [Risk] The old `ILocalizationService` may remain unused and confusing. → Mitigation: remove it if no non-Avalonia consumer remains, or mark it obsolete during the implementation and delete it in the same change if tests allow.

## Migration Plan

1. Add the Avalonia localization manager, supported language model, resource dictionaries, and composition registration.
2. Migrate `App.axaml`, main-window XAML, tool views, DataGrid headers, context menus, window titles, placeholders, and language tool options to localized resources.
3. Introduce localized prompt/status/log helpers in Avalonia ViewModels and migrate visible prompt/status/log messages from interpolated strings to keys plus arguments.
4. Add diagnostic-to-localized-message mapping for known user-facing diagnostics while preserving raw details in logs.
5. Update settings migration so blank language behaves as Simplified Chinese and saved selections use explicit culture tags.
6. Remove or obsolete the historical `ILocalizationService` path if it is no longer used.
7. Add tests for resource completeness, language persistence, runtime refresh behavior, localized status/log output, UTF-8 Chinese/Japanese resources, and absence of mojibake.
