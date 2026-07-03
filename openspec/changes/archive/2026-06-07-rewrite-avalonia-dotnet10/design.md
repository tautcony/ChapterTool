## Context

The existing Time_Shift implementation mixes WinForms event handlers, chapter data models, importers, exporters, settings, external process calls, native DLL loading, registry access, and auxiliary windows. The rewrite targets Avalonia + .NET 10 while preserving legacy chapter behavior documented in `docs/avalonia-rewrite-spec.md` and `docs/modules/*.md`.

The repository currently has no archived OpenSpec specs, so this change introduces the initial requirement set. Existing WinForms sources and `Time_Shift_Test` samples are compatibility references, not the target architecture.

## Goals / Non-Goals

**Goals:**

- Create SDK-style .NET 10 projects with clear Core, Infrastructure, Avalonia, and test boundaries.
- Keep parsing, transformation, editing, and export logic UI-independent and TDD-covered.
- Isolate platform-specific behavior behind services so non-Windows builds can degrade cleanly.
- Preserve existing sample-based behavior through regression fixtures and golden snapshots.
- Enable parallel implementation by keeping write scopes and contracts explicit.

**Non-Goals:**

- Directly port WinForms Designer layouts, `System.Windows.Forms` controls, or HWND message hooks.
- Require all external tools to be installed on developer machines for unit tests.
- Redesign media formats beyond the documented ChapterTool behavior.
- Finalize a long-term auto-update distribution strategy beyond safe, testable service boundaries.

## Decisions

1. Use a layered solution.
   - `ChapterTool.Core` contains models, result/diagnostic contracts, parsers that do not need IO services, transformations, editing, and exporters.
   - `ChapterTool.Infrastructure` contains filesystem, settings, process runner, external tool locators, native dependency resolvers, shell, registry, and Windows-only implementations.
   - `ChapterTool.Avalonia` contains Views, ViewModels, resource dictionaries, and service composition.
   - Test projects mirror these boundaries.
   - Rationale: this prevents UI event handlers from becoming the new business logic layer.

2. Define contracts before importer/UI wiring.
   - Shared contracts include `Chapter`, `ChapterInfo`, `ChapterInfoGroup`, `ChapterImportResult`, `ChapterExportResult`, `Diagnostic`, `IChapterImporter`, `IChapterExporter`, `IProcessRunner`, and settings/window/dialog service interfaces.
   - Rationale: importer and UI work can proceed in parallel once contracts stabilize.

3. Keep external tool execution outside Core.
   - Matroska, BDMV, and native MP4 paths use injectable infrastructure adapters.
   - Core importers receive text/streams or adapter results and return structured diagnostics.
   - Rationale: tests must not depend on MKVToolNix, eac3to, registry, or native DLL availability.

4. Use compatibility fixtures as the first TDD asset.
   - Existing `Time_Shift_Test` samples become stable test assets.
   - Smoke tests are upgraded to assertions and exporter golden snapshots.
   - Rationale: the rewrite is behavior preservation first.

5. Treat Windows-only features as optional platform services.
   - File association, elevation, registry discovery, tray notifications, and hard-link fallback return unsupported results off Windows.
   - Rationale: Avalonia enables cross-platform UI, but legacy integrations are Windows-specific.

## Risks / Trade-offs

- Compatibility quirks may conflict with cleaner .NET behavior -> encode quirks as explicit tests before changing them.
- Open-ended packaging/update choices can block delivery -> implement publishable app and service boundaries first; finalize installer strategy after publish layout stabilizes.
- Parallel agents can collide on shared contracts -> keep Core contracts as a foundation task owned by the main integration path before dependent tasks edit importer/UI code.
- Native MP4 dependency may not fit cross-platform packaging -> document the chosen strategy and provide missing-dependency diagnostics until a replacement is selected.

## Migration Plan

1. Add the SDK-style solution and empty project/test structure.
2. Implement Core contracts, models, diagnostics, and compatibility fixture resolver.
3. Port Core transformations/exporters with golden tests.
4. Port pure importers, then dependency-backed adapters with fake services.
5. Implement Infrastructure settings/platform services and legacy config migration.
6. Implement Avalonia ViewModels and Views against mocked and then real services.
7. Add CI build/test/publish and package asset/version checks.

