## Context

SourceGit 2026 stores its reusable user interface foundation in three resource dictionaries. These files are `Resources/Icons.axaml`, `Resources/Themes.axaml`, and `Resources/Styles.axaml`. SourceGit does not contain a separate control-library project or a `Controls` directory.

The style dictionary contains a small number of bindings to SourceGit application preferences and Git-domain types. ChapterTool uses Avalonia 12.1.0. SourceGit uses Avalonia 11.3.18.

## Goals / Non-Goals

**Goals:**

- Port all icons and theme tokens.
- Port every reusable control style.
- Load the ported foundation across the Avalonia application.
- Keep the imported files easy to compare with the upstream files.
- Rebuild the log tool with the SourceGit master-detail composition.
- Preserve ChapterTool behavior, localization, accessibility, and logging boundaries.

**Non-Goals:**

- Do not import Git workflows, Git models, Git ViewModels, or Git commands.
- Do not add SourceGit branding to ChapterTool.
- Do not change ChapterTool domain behavior.
- Do not replace the browser user interface.

## Decisions

### 1. Treat the resource layer as the user interface component library

Copy the three complete SourceGit resource dictionaries into `Views/SourceGit`. Keep the original resource keys. Add a `NOTICE.md` file that records the source revision, license, excluded Git-only selectors, and Avalonia 12 adaptations.

The imported icon and theme dictionaries must retain all upstream entries. The ported style dictionary must retain all reusable selectors. It may remove only selectors or data templates that reference a SourceGit Git-domain type.

### 2. Adapt application bindings through resources

Replace `Preferences.Instance` font-size and scrollbar bindings with dynamic ChapterTool resources. Replace SourceGit converter references with Avalonia 12-compatible bindings or fixed semantic resources. Keep AvaloniaEdit styles because ChapterTool already uses AvaloniaEdit.

Load SourceGit styles after Fluent and AvaloniaEdit base styles. Load ChapterTool-specific styles after SourceGit styles. This order keeps the imported foundation global and permits product-specific constraints.

### 3. Map existing presets to SourceGit tokens

Keep ChapterTool preset identifiers and persistence unchanged. `AvaloniaThemeApplicationService` must set both ChapterTool semantic brushes and the SourceGit `Color.*` tokens. Open windows must update through dynamic resources.

### 4. Rebuild the log window as a master-detail tool

Use the SourceGit `ViewLogs` structure as the composition reference. The left pane contains a virtualized compact list. The right pane contains selectable technical details. The footer contains filtering, clear, and copy actions.

The ViewModel must snapshot retained entries, subscribe to live additions, marshal updates to the user-interface thread, and unsubscribe on disposal. It must format localized summaries at display time. It must use `IClipboardService` for copy operations.

### 5. Preserve license evidence

The imported resources remain under the SourceGit MIT license. The notice must include the copyright statement and the upstream repository address. ChapterTool's license file remains unchanged.

### 6. Use SourceGit form composition in settings

The settings view must use the global SourceGit input and button templates. Path actions must appear in the input right-content area. The footer must keep status and folder access on the left. Reset and save actions must form a stable right-aligned group.

Use a 32-pixel input height and a 48-pixel footer height. Use the same responsive one-to-four ratio for the label and editor columns on each form tab. Stretch primary editors to the right edge of the editor column. Keep primary and secondary actions visible at the minimum settings-window width.

## Risks / Trade-offs

- Avalonia 11 templates can fail under Avalonia 12. Compile and Headless tests must detect incompatible properties and template parts.
- Global styles can change layout metrics. Headless workflow tests must verify the main workflow zones and narrow tool-window behavior.
- Theme resource collisions can hide preset colors. Resource tests must verify representative light and dark values.
- Long log details can force layout growth. The detail pane must scroll in both directions.

## Migration Plan

1. Import and adapt the resource dictionaries.
2. Load the resource layer and map theme resources.
3. rebuild the log ViewModel and view.
4. Add attribution, code-map documentation, and tests.
5. Run unit tests, Headless tests, the Avalonia build, and the full solution tests in sequence.
