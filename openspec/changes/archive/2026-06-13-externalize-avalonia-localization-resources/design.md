## Context

The Avalonia localization layer already has the right presentation-facing shape: `IAppLocalizer`, a shared `AppLocalizationManager`, supported culture tags, fallback behavior, and tests for key parity, placeholder parity, and mojibake. The weak point is storage: all translated strings live in `AppLocalizationResources.cs` as culture dictionaries.

That storage model makes translation content look like implementation code, increases merge friction, and bypasses the standard .NET resource pipeline. The replacement should preserve the public localizer API and runtime resource updates while moving the translation values into resource assets.

## Goals / Non-Goals

**Goals:**
- Store all Avalonia UI, prompt, status, and log translations in culture-specific `.resx` files.
- Load supported culture resources through `ResourceManager`/compiled .NET resources.
- Keep `IAppLocalizer`, semantic keys, supported culture tags, formatting behavior, and Simplified Chinese fallback stable.
- Keep tests behavior-focused by validating compiled resources rather than reading `.resx` or `.cs` files as text.

**Non-Goals:**
- Do not change translation wording except where required by resource escaping.
- Do not introduce a third-party localization package.
- Do not redesign XAML binding or the existing `IAppLocalizer` interface.
- Do not move domain/Core behavior under UI culture.

## Decisions

1. Use `.resx` plus `ResourceManager` for translation storage.

   `.resx` is the standard .NET resource format, is understood by MSBuild, supports culture-specific satellite resources, and keeps translators away from source syntax. The alternative, Avalonia `.axaml` resource dictionaries, works well for XAML resource lookup but is weaker for code-side log/status formatting and still requires a parallel lookup path. The alternative, JSON/YAML files, would require custom parsing, packaging, fallback, and validation logic.

2. Keep semantic string keys unchanged.

   Existing ViewModels, XAML resources, and log formatting use keys such as `Status.LoadedChapters`. Keeping those keys avoids a broad application rewrite. Because generated strongly typed `.resx` properties do not map cleanly to dotted keys, the localizer should enumerate resources by key through `ResourceManager.GetResourceSet` rather than rely on generated property names.

3. Keep `AppLocalizationResources` as a loader facade, not as translation storage.

   Tests and the manager already depend on `AppLocalizationResources.All` and `.Fallback`. Keeping that type as a thin resource loader minimizes churn while removing the hard-coded dictionaries. It may cache loaded dictionaries for deterministic tests and efficient lookup.

4. Continue pushing active resources into `Application.Current.Resources`.

   The current runtime language switch depends on updating Avalonia application resources and raising `CultureChanged`. The implementation should keep that behavior, using dictionaries loaded from `.resx`.

## Risks / Trade-offs

- [Risk] `.resx` XML escaping can alter strings containing braces, quotes, or non-ASCII text. -> Mitigation: migrate mechanically from the existing dictionaries and keep tests for placeholder compatibility and mojibake.
- [Risk] Satellite-resource fallback may hide a missing culture-specific key. -> Mitigation: tests must enumerate each explicit culture resource set and verify exact key parity against `zh-CN`.
- [Risk] Resource enumeration can return inherited fallback values if implemented incorrectly. -> Mitigation: load each explicit culture with `tryParents: false` when validating/parity-checking culture dictionaries.
- [Risk] Generated designer code could reintroduce translation-adjacent generated files. -> Mitigation: access resources by base name and culture through `ResourceManager`; do not require hand-maintained designer classes.
