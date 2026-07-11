## ADDED Requirements

### Requirement: Main-window async loads preserve anti-stale commits through the workspace
The main window ViewModel SHALL continue to ignore superseded load/append progress and results when using the chapter workspace session, matching the workspace revision contract rather than relying on ad-hoc fields that can be lost during refactor.

#### Scenario: Overlapping loads still ignore the older completion
- **WHEN** a newer load becomes current while an older load is still running
- **THEN** the older load's later progress and result SHALL NOT overwrite the newer session's path, rows, status, or progress

#### Scenario: Append after session replacement is discarded
- **WHEN** append-MPLS is in flight and a newer load replaces the session before append completes
- **THEN** the late append result SHALL NOT restore combine state or chapter rows from the superseded session

### Requirement: Main-window options bind as the single source of truth
The Avalonia main window SHALL treat ViewModel/workspace-bound properties as the authoritative state for path, save format, naming mode, expression application, expression text, order shift, frame-rate selection, and round-frames. Command handlers SHALL NOT push control values into the ViewModel immediately before execution as a substitute for bindings.

#### Scenario: Save uses already-bound options
- **WHEN** the user invokes save or save-to
- **THEN** the command path SHALL use the current ViewModel/workspace export and projection state
- **AND** the window SHALL NOT need a pre-command `ReadAdvancedOptions`-style control scrape to make save correct

#### Scenario: Preview and refresh use already-bound options
- **WHEN** the user invokes preview or row refresh
- **THEN** the command path SHALL use already-bound expression, naming, order-shift, and frame options
- **AND** the window SHALL NOT require imperative control-to-ViewModel copies to keep those operations consistent with the UI

#### Scenario: Path box remains synchronized through binding or one owned path property
- **WHEN** the user loads from browse, drag/drop, reload, or startup path
- **THEN** the displayed path and ViewModel current path SHALL stay synchronized through a single owned path property surface
- **AND** load SHALL NOT permanently rely on reading an unbound `PathBox.Text` as the only source of truth

### Requirement: Single command surface for main workflow actions
The Avalonia main window SHALL expose one primary command surface for workflow actions through the main ViewModel. Window-level command wrappers MAY exist only when they add view-only parameters such as picker results or selected row indexes, and SHALL NOT duplicate independent can-execute business rules.

#### Scenario: Business can-execute lives on the ViewModel
- **WHEN** save, combine, append MPLS, delete, insert, or related-media availability changes
- **THEN** the ViewModel commands SHALL raise the authoritative can-execute changes
- **AND** any window wrappers SHALL derive enabled state from those commands or equivalent ViewModel capability flags

#### Scenario: Keyboard routing does not fork business semantics
- **WHEN** a documented shortcut is pressed
- **THEN** the gesture SHALL route to the same ViewModel command path used by the corresponding visible control or menu action
- **AND** load/save shortcuts SHALL NOT maintain a separate incompatible command implementation

### Requirement: Chapter grid edits use stable column identity
Committed chapter-grid edits SHALL route to time, name, or frame commands through a stable column identity that does not depend on localized header text.

#### Scenario: Language switch does not break cell commit routing
- **WHEN** the UI language is Simplified Chinese, English, or Japanese
- **THEN** committing an edit in the time, name, or frame column SHALL route to the corresponding edit command

#### Scenario: Header text is not the sole edit discriminator
- **WHEN** a column header resource string changes or is reformatted
- **THEN** cell commit routing SHALL continue to work through tag, column id, binding path, or equivalent stable identity
- **AND** hard-coded bilingual header string matching SHALL NOT be required

### Requirement: Secondary tools depend on narrow session ports
Secondary tool ViewModels for language, expression, template names, forward shift, settings live-apply, and preview format selection SHALL depend on narrow workspace/session ports rather than the full main-window ViewModel type for unrelated capabilities.

#### Scenario: Tool construction does not require full main command surface
- **WHEN** a secondary tool ViewModel is unit-tested
- **THEN** it SHALL be constructible against a focused port/fake for its capability
- **AND** it SHALL NOT need a fully wired main-window ViewModel solely to set one preference or expression field

#### Scenario: Language selection remains available without duplicating ownership chaos
- **WHEN** the user changes UI language from the dedicated language tool or from Settings
- **THEN** both entry points SHALL persist and apply language through the same preference/session path
- **AND** neither path SHALL maintain a divergent culture-normalization or persistence rule

### Requirement: Expression editor presentation is modular and theme-aware
The Lua expression editor control SHALL separate authoring analysis from presentation concerns and SHALL consume application theme/semantic resources for completion and diagnostic visuals instead of hard-coding a private permanent palette that ignores the active theme.

#### Scenario: Theme change updates expression editor chrome colors
- **WHEN** the active appearance preset changes while an expression editor is visible
- **THEN** completion category colors, diagnostic underlines, and editor chrome colors that represent themeable UI chrome SHALL resolve from application theme or semantic resources
- **AND** the control SHALL NOT keep an independent hard-coded palette that remains visually frozen across theme switches

#### Scenario: Authoring analysis remains reusable without the full control
- **WHEN** expression token/completion/diagnostic analysis is needed by tests or presentation code
- **THEN** analysis SHALL continue to come from the Core expression authoring service
- **AND** control-specific rendering helpers SHALL remain separable from analysis

### Requirement: Settings panel modules own distinct preference groups
The settings panel implementation SHALL modularize durable preference groups so output defaults, external tools, appearance, and about/runtime info are not permanently accumulated as one undifferentiated mega-ViewModel without internal ownership boundaries.

#### Scenario: Appearance remains a dedicated module
- **WHEN** theme or font settings change
- **THEN** appearance selection, preview metadata, and font catalogs SHALL continue to be owned by a dedicated appearance module/ViewModel

#### Scenario: External tool path editing is isolatable
- **WHEN** external tool browse/clear/validate/discover actions are exercised
- **THEN** those actions SHALL be implementable and testable as an external-tools settings module without requiring unrelated about-panel logic

## MODIFIED Requirements

### Requirement: Chapter grid interaction
The chapter table SHALL use observable row models and commands instead of UI row tags, and cell commits SHALL route through stable column identity rather than localized header text.

#### Scenario: Edit chapter cell
- **WHEN** a time, name, or frame cell is edited
- **THEN** the ViewModel SHALL delegate validation and conversion to Core services and refresh row display from returned model state

#### Scenario: Localized grid headers still route edits
- **WHEN** the chapter grid uses localized headers for time, name, and frame columns in any supported UI language
- **THEN** committed edits SHALL still route to the correct time, name, and frame commands through stable column identity

#### Scenario: Delete selected rows
- **WHEN** selected rows are deleted
- **THEN** the ViewModel SHALL update the underlying chapter list through Core operations and refresh numbering, time, and frame values

### Requirement: Main-window state is bound from XAML
The Avalonia main window SHALL bind visible state to ViewModel properties instead of synchronizing normal UI state through imperative control assignments or pre-command control scrapes.

#### Scenario: Bound controls update from ViewModel
- **WHEN** ViewModel state changes after loading, editing, clip switching, option changes, or save operations
- **THEN** bound text, progress, item sources, selections, checkboxes, visibility, enabled state, and option fields SHALL update through XAML bindings

#### Scenario: Code-behind remains view-specific
- **WHEN** `MainWindow.axaml.cs` is inspected or exercised
- **THEN** it SHALL only contain view-specific event adaptation, platform UI interactions, selection index extraction, keyboard gesture adaptation, constructor wiring, and responsive layout
- **AND** it SHALL NOT own routine status, progress, grid, clip, option, export-option, or command-state business synchronization
- **AND** it SHALL NOT scrape advanced-option or frame-option controls into the ViewModel immediately before save/preview/refresh as the normal option path

### Requirement: Preview and save use shared Lua projection
The Avalonia shell SHALL route preview, text preview, and save through the same Lua expression projection options and the same injected export/projection service path exposed by the main ViewModel/workspace.

#### Scenario: Main ViewModel builds one projection option set
- **WHEN** the main ViewModel previews or saves chapters with expression application enabled
- **THEN** it SHALL pass the same Lua expression/script text, preset/source metadata, order shift, naming, format, and language options into the Core projection/export path

#### Scenario: Save does not re-read external script files
- **WHEN** a user has loaded an external Lua script in the expression tool and later saves chapters
- **THEN** the ViewModel SHALL pass the already-loaded script text to Core
- **AND** save SHALL NOT require Core or the save service to re-read the external script path

#### Scenario: Preview matches save projection
- **WHEN** the user previews projected chapters and then saves without changing options
- **THEN** the times, generated numbers, generated names, and Lua diagnostics used for preview SHALL match the data used for save output

#### Scenario: Preview uses injected export services
- **WHEN** preview content is generated
- **THEN** the shell SHALL use the composition-injected export/projection services rather than constructing a separate default `ChapterExportService` that can diverge from the save path
