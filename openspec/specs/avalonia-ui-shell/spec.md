# avalonia-ui-shell Specification

## Purpose
TBD - created by archiving change rewrite-avalonia-dotnet10. Update Purpose after archive.
## Requirements
### Requirement: Main window ViewModel state
The Avalonia main window SHALL be driven by a ViewModel rather than by direct control state.

#### Scenario: Start without source
- **WHEN** the application starts without a source argument
- **THEN** `CurrentPath` SHALL be empty, chapter rows SHALL be empty, clip selection SHALL be hidden, advanced panel SHALL be collapsed, and save type SHALL default to TXT

#### Scenario: Source load result updates state
- **WHEN** a load service returns a successful chapter result
- **THEN** the ViewModel SHALL update current path, display path, clip options, current chapter rows, status text, and progress from the result

#### Scenario: Older load results are ignored
- **WHEN** a source load is still running and a newer source load starts
- **THEN** progress and result updates from the older load SHALL NOT overwrite the newer load's current path, chapter rows, status, or progress state after the newer load has become current

### Requirement: Main window load progress
The main window SHALL present bounded progress during source loading when the load pipeline reports intermediate progress, and SHALL NOT present an empty progress indicator at idle.

#### Scenario: Importer reports intermediate progress
- **WHEN** a load operation reports progress before returning its import result
- **THEN** the main-window view model SHALL update the progress value to a bounded intermediate value
- **AND** completion or failure handling SHALL remain responsible for the final progress state

#### Scenario: Idle state hides the progress indicator
- **WHEN** no load or save operation is running
- **THEN** the status-strip progress indicator SHALL NOT be visible

### Requirement: Legacy-inspired cross-platform UX
The Avalonia main window SHALL preserve the original ChapterTool workflow density while using cross-platform Avalonia controls and services.

#### Scenario: Main window uses modern responsive layout
- **WHEN** the main window is opened at its default size
- **THEN** it SHALL use responsive Avalonia layout panels rather than absolute coordinates, preserving the original workflow zones without attempting a 1:1 WinForms geometry clone

#### Scenario: Main window avoids absolute layout
- **WHEN** the main window XAML is inspected
- **THEN** the primary layout SHALL NOT use `Canvas`, `Canvas.Left`, or `Canvas.Top` to position normal workflow controls

#### Scenario: Main window text is readable
- **WHEN** the main window XAML is inspected or rendered
- **THEN** visible Chinese labels SHALL be stored as valid UTF-8 text and SHALL NOT appear as mojibake strings such as `杞藉叆` or `淇濆瓨`

#### Scenario: Main surface matches legacy workflow zones
- **WHEN** the main window is rendered
- **THEN** it SHALL present an intuitive light tool-style surface with Load and Save actions, frame rounding controls, a central editable chapter grid, and a bottom options panel for save format, XML language, naming, order shift, expression, and log/status controls

#### Scenario: Auxiliary actions remain discoverable without visual clutter
- **WHEN** optional actions such as preview, refresh, color, language, template, zones, forward shift, related media, or append MPLS are available
- **THEN** they SHALL be reachable from compact buttons or context menus on the relevant workflow area rather than from an always-visible marketing-style navigation strip

#### Scenario: Load variants are reachable from a visible control
- **WHEN** the load action offers the Reload and Append MPLS variants
- **THEN** the variants SHALL be reachable from a visible split-style control on the Load action
- **AND** the variants SHALL NOT be reachable only through a right-click context menu on a button

#### Scenario: Frame-rate change action has a visible entry point
- **WHEN** the Change FPS action is available for the frame-rate selector
- **THEN** it SHALL be reachable from a visible control next to the selector

#### Scenario: Keyboard shortcuts are displayed
- **WHEN** a menu item or primary action has a keyboard shortcut
- **THEN** the menu item SHALL display the shortcut as an input gesture, or the control tooltip SHALL include the shortcut text

#### Scenario: Platform-specific integration is gated
- **WHEN** a workflow needs file picking, directory picking, clipboard, shell-open, settings, or file association
- **THEN** the UI SHALL use platform service abstractions and SHALL NOT require direct Windows registry access for normal cross-platform operation

#### Scenario: Registry-dependent actions are not primary controls
- **WHEN** the normal cross-platform main window is rendered
- **THEN** registry-dependent integrations such as `.mpls` file association SHALL NOT be exposed as always-visible primary controls

### Requirement: Command surface
The UI shell SHALL expose documented main-window actions through commands.

#### Scenario: Commands exist
- **WHEN** the main window ViewModel is constructed
- **THEN** it SHALL expose commands for load, reload, append MPLS, dropped path load, save, save directory, refresh, clip selection, combine, chapter editing, delete, zones, forward shift, insert, preview, log, color settings, language, expression, template names, and file association

#### Scenario: Save delegates to service
- **WHEN** save is invoked
- **THEN** the ViewModel SHALL synchronize current rows and call the save service with selected save type, language, naming, template, order shift, expression, and directory options

#### Scenario: Save write failures return diagnostics
- **WHEN** the runtime save service cannot create, write, move, or replace the target output file because of an I/O, path, or permission failure
- **THEN** save SHALL return a structured failure diagnostic
- **AND** the shell SHALL keep the application usable rather than allowing the file-system exception to escape the command

### Requirement: Keyboard and menu routing
The Avalonia shell SHALL route the active, user-configurable shortcut mapping and preserve context menu actions.

#### Scenario: Global shortcuts route to commands
- **WHEN** the main window has focus and an active mapped gesture is pressed
- **THEN** the command associated with that gesture SHALL execute

#### Scenario: Default mapping preserves legacy behavior
- **WHEN** no shortcut overrides exist
- **THEN** `Ctrl+O`, `Ctrl+S`, `Alt+S`, `Ctrl+R`, `F5`, `Ctrl+L`, and `F11` SHALL invoke the corresponding ViewModel commands

#### Scenario: Context menus use capability flags
- **WHEN** load, clip, or chapter-row context menus open
- **THEN** entries such as append MPLS, merge chapters, related media, zones, forward translation, and insert SHALL be enabled only when the ViewModel capability flags allow them

#### Scenario: MPLS clip merge is a checked toggle
- **WHEN** an MPLS source exposes multiple clip options and the user invokes merge chapters from the clip or chapter-row context menu
- **THEN** the menu item SHALL show a checked state and the current chapter rows SHALL represent all clips combined into one chapter set
- **AND** invoking the same checked menu item again SHALL clear the checked state and restore the individual clip options and selected clip rows

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

### Requirement: Chapter grid empty-state visual
The Avalonia main window SHALL render the existing chapter empty SVG as a centered visual state when the chapter table has no rows.

#### Scenario: Empty chapter grid shows SVG
- **WHEN** the main window is rendered before any chapter source has been loaded
- **THEN** the chapter grid area SHALL contain a visible centered image loaded from `Assets/Images/chapter-empty.svg`
- **AND** the chapter grid SHALL remain present as the command and layout surface

#### Scenario: Loaded chapters hide SVG
- **WHEN** a chapter source is loaded and chapter rows are displayed
- **THEN** the empty-state SVG SHALL be hidden
- **AND** the chapter rows SHALL remain displayed in the grid

### Requirement: No WinForms coupling
The Avalonia project and ViewModels MUST NOT require WinForms for main-window behavior.

#### Scenario: UI project dependency check
- **WHEN** the Avalonia project is built
- **THEN** main-window code SHALL NOT reference `DataGridView`, `ToolStrip`, `MessageBox`, `Application.DoEvents`, or WinForms forms

### Requirement: Observable main-window ViewModel
The Avalonia main-window ViewModel SHALL expose UI-facing scalar state and command availability as observable properties.

#### Scenario: Scalar state changes notify bindings
- **WHEN** status text, progress, selected clip index, frame rounding, frame-rate selection, save format, option flags, visibility flags, or capability flags change
- **THEN** the ViewModel SHALL raise property change notifications for the changed properties

#### Scenario: Command availability follows state
- **WHEN** load state, selected rows, selected clip, save options, or platform capability state changes
- **THEN** affected commands SHALL raise command availability changes without requiring the window to call a manual refresh method

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

### Requirement: Typed compiled bindings
The Avalonia shell SHALL use typed data contexts and compiled bindings for the main window and migrated typed views.

#### Scenario: Main window bindings are checked at build time
- **WHEN** the Avalonia app project is built
- **THEN** main-window bindings to ViewModel properties and commands SHALL be validated by typed or compiled Avalonia binding support

#### Scenario: Binding regressions fail tests or build
- **WHEN** a bound ViewModel property or command is renamed or removed
- **THEN** the build or focused UI/static tests SHALL fail before runtime manual testing is required

### Requirement: Unified design system
The Avalonia UI SHALL use one shared design system based on the imported SourceGit style layer for every view, and the resource layer SHALL NOT keep duplicate or unused style content.

#### Scenario: Views share one style vocabulary
- **WHEN** the main window and the tool views are inspected
- **THEN** buttons, inputs, toolbars, and footers SHALL use shared style classes from the shared style layer
- **AND** views SHALL NOT define local styles that duplicate a shared class

#### Scenario: Surface brushes use one vocabulary
- **WHEN** a view paints a surface, border, or foreground
- **THEN** it SHALL reference an imported `Brush.*` token or a dedicated semantic `ChapterTool.*` brush
- **AND** alias brushes that only mirror another token SHALL NOT exist

#### Scenario: Dead style content is absent
- **WHEN** the style resources are audited
- **THEN** style classes referenced by views SHALL have a definition
- **AND** defined selectors, brushes, and colors SHALL have at least one consumer

### Requirement: Integer sizing scale
The Avalonia UI SHALL define control sizes, spacing, and font sizes with integer device-independent pixel values on a shared scale.

#### Scenario: Sizes are integer values
- **WHEN** the view XAML and shared styles are inspected
- **THEN** font sizes, control dimensions, margins, and spacing SHALL use integer values

#### Scenario: Text meets the minimum readable size
- **WHEN** any visible text is rendered
- **THEN** its font size SHALL be at least the shared small font token (12)

### Requirement: Frame-accuracy indicator rendering
The Frames column SHALL indicate frame accuracy through the dedicated semantic frame colors using a single text layer.

#### Scenario: Accuracy states use semantic colors
- **WHEN** a chapter row is frame-accurate, inexact, or neutral
- **THEN** the Frames cell text SHALL use the matching dedicated semantic brush

#### Scenario: The indicator renders one text layer
- **WHEN** the Frames cell template is inspected
- **THEN** it SHALL contain one text element for the frames value
- **AND** it SHALL NOT apply bitmap effects such as drop shadows

### Requirement: Hidden command shims are removed
The UI shell SHALL NOT use hidden buttons or invisible controls as command hosts or state hosts for main-window actions.

#### Scenario: Main actions are reachable through real command surfaces
- **WHEN** save, append MPLS, combine, open media, color, expression, template, zones, forward shift, and similar actions are available
- **THEN** they SHALL be exposed through visible buttons, menu items, context menu items, key bindings, or directly testable ViewModel commands

#### Scenario: Hidden shim controls are absent
- **WHEN** the main-window XAML is inspected
- **THEN** controls whose only purpose is to hide a command binding or a state binding from the visible UI SHALL NOT be present
- **AND** tests SHALL drive source-path state through the ViewModel instead of a hidden text box

### Requirement: Async commands are observed
The UI shell SHALL execute asynchronous commands through an abstraction or event pattern that awaits work, observes exceptions, and exposes execution state when needed.

#### Scenario: Async command failures are handled
- **WHEN** a load, save, edit, clip, combine, or auxiliary command fails asynchronously
- **THEN** the exception SHALL be observed and routed to the ViewModel or dialog/status error path instead of being lost as fire-and-forget work

#### Scenario: Event handlers await command work
- **WHEN** grid edits, shortcut handlers, clip selection, insert/delete operations, or combine actions trigger asynchronous command behavior
- **THEN** the event adaptation code SHALL await the command task or call an async command API that tracks completion

### Requirement: Auto frame-rate detection in the UI
The Avalonia main window SHALL expose an `Auto` entry as the first item in the frame-rate selector and SHALL surface detection feedback when Auto is the active selection.

#### Scenario: Auto entry appears in the frame-rate selector
- **WHEN** the main window renders
- **THEN** the frame-rate ComboBox SHALL include `Auto` as its first item, followed by the documented seven valid frame-rate rows in the same order as before

#### Scenario: Auto detection updates status text
- **WHEN** the user picks `Auto` and the chapter set has at least one non-separator chapter
- **THEN** the ViewModel SHALL run frame-rate detection and SHALL update `StatusText` to a string of the form `Detected {DisplayName} (confidence: {Confidence})` that reflects the chosen frame rate and the confidence band

#### Scenario: Auto remains selected after detection
- **WHEN** Auto detection completes
- **THEN** `SelectedFrameRateIndex` SHALL stay on the Auto row so subsequent edits or refreshes re-run the detector

#### Scenario: Manual frame-rate choice does not emit detection status
- **WHEN** the user picks any non-Auto frame-rate row
- **THEN** the ViewModel SHALL NOT overwrite `StatusText` with a `Detected` message, and SHALL apply the manually selected frame rate directly

### Requirement: Avalonia UI text is localized through resources
The Avalonia UI shell SHALL render user-facing static text through Avalonia localization resources for Simplified Chinese, English, and Japanese.

#### Scenario: Main window static text uses localized resources
- **WHEN** the main window is rendered in any supported UI language
- **THEN** visible labels, button content, menu headers, tooltips, DataGrid headers, tab labels, and option captions SHALL come from localized resources rather than hard-coded mixed-language literals

#### Scenario: Secondary tool static text uses localized resources
- **WHEN** preview, log, color settings, language, expression, template names, zones, or forward-shift tools are opened in any supported UI language
- **THEN** window titles, labels, buttons, placeholders, and option captions SHALL come from localized resources for the active language

#### Scenario: Runtime language switch refreshes visible resources
- **WHEN** the user changes the UI language from the language tool
- **THEN** open Avalonia views SHALL refresh localized static text without requiring an application restart where Avalonia resource refresh is supported

#### Scenario: Unsupported language falls back predictably
- **WHEN** settings contain an unsupported or blank UI language value
- **THEN** the UI shell SHALL use the Simplified Chinese resource set and SHALL NOT render localization keys as normal visible text

### Requirement: UI prompts and state messages are localized by semantic key
The Avalonia shell SHALL represent user-facing prompts, status, and command feedback as semantic localized messages instead of storing hard-coded English or Chinese display strings in ViewModels.

#### Scenario: Load status is localized
- **WHEN** a source is loaded successfully
- **THEN** `StatusText` SHALL display the localized active-language equivalent of the loaded-chapter count message with the chapter count formatted into the message

#### Scenario: Save status is localized
- **WHEN** chapters are saved successfully or saving fails
- **THEN** `StatusText` SHALL display a localized success or failure message in the active UI language

#### Scenario: Dialog prompts are localized
- **WHEN** the shell displays confirmation, error, unsupported-feature, empty-state, placeholder, or command-feedback prompts
- **THEN** each visible prompt title, message, and action caption SHALL use the active UI language resource set

#### Scenario: Frame-rate detection status is localized
- **WHEN** auto frame-rate detection updates status text
- **THEN** the status message SHALL be localized while preserving the detected frame-rate display name and confidence value

#### Scenario: Technical diagnostics are mapped for users
- **WHEN** a known diagnostic code reaches the Avalonia shell
- **THEN** the shell SHALL display a localized user-facing summary for that code and retain the original diagnostic message as technical detail for logs

### Requirement: Language tool supports the target languages
The Avalonia language tool SHALL allow users to choose Simplified Chinese, English, and Japanese with localized display names.

#### Scenario: Language options are complete
- **WHEN** the language tool is opened
- **THEN** it SHALL show Simplified Chinese, English, and Japanese options with stable culture tags `zh-CN`, `en-US`, and `ja-JP`

#### Scenario: Language selection persists
- **WHEN** the user applies a language selection
- **THEN** the selected culture tag SHALL be persisted to settings and applied to the current application localization manager

### Requirement: Expression editor authoring experience
The Avalonia shell SHALL use a dedicated Lua expression/script editor for all expression inputs instead of a plain text box or the previous custom formula grammar editor, while allowing simple arithmetic expressions without requiring an explicit `return`.

#### Scenario: Main expression input uses the Lua expression editor
- **WHEN** the main window is rendered
- **THEN** the expression input SHALL provide Lua syntax highlighting for expression script tokens
- **AND** it SHALL expose Lua-aware completion candidates based on the caret token, including provided globals, safe helper functions, and discoverable `preset.*` entries

#### Scenario: Expression tool uses the same Lua editor
- **WHEN** the expression tool window is opened
- **THEN** its expression input SHALL use the same editor behavior as the main window

#### Scenario: Completion items are visually categorized
- **WHEN** the Lua expression completion popup is open
- **THEN** completion items SHALL visually identify whether they are variables, functions, keywords, or presets using category labels and distinct colors

#### Scenario: Tab accepts Lua completion
- **WHEN** a completion popup is open for a Lua prefix
- **THEN** pressing `Tab` SHALL insert the selected completion
- **AND** focus SHALL remain in the expression editor

#### Scenario: Lua syntax errors show correction guidance
- **WHEN** the user enters an invalid Lua expression script
- **THEN** the editor SHALL display the specific Lua syntax or validation problem
- **AND** it SHALL display a correction suggestion derived from the diagnostic

#### Scenario: Valid Lua script clears errors
- **WHEN** the user corrects an invalid Lua expression script to a valid script
- **THEN** the error and suggestion feedback SHALL be cleared

### Requirement: Lua expression script authoring
The Avalonia shell SHALL allow users to author and apply Lua expression transforms with built-in presets and external script selection. Expression text SHALL remain an operation draft until the user explicitly applies its candidate.

#### Scenario: Expression tool exposes Lua script editing
- **WHEN** the expression tool window is opened
- **THEN** it SHALL present Lua script editing as the expression authoring surface
- **AND** it SHALL NOT require users to choose or understand the previous formula/postfix grammar

#### Scenario: Built-in Lua preset can populate script text
- **WHEN** the user selects a built-in Lua script preset in the expression tool or accepts a `preset.*` completion in the editor
- **THEN** the tool SHALL show or insert the preset script text
- **AND** it SHALL prepare a live candidate preview without changing the document
- **AND** it SHALL apply that script to the document only after the user confirms Apply

#### Scenario: External Lua script can be selected
- **WHEN** the user chooses an external `.lua` script from the main expression input or expression tool
- **THEN** the UI SHALL expose the load action as a button at the right side of the Lua expression input
- **AND** it SHALL use the file picker service abstraction to select and read the script text
- **AND** loading the script SHALL prepare a preview without changing the document
- **AND** applying the loaded script SHALL pass its text into the candidate operation without requiring Core to read the file path

#### Scenario: Lua expression or script is applied as a document operation
- **WHEN** the user edits or loads a Lua expression script
- **THEN** the UI SHALL refresh a read-only candidate preview and surface Lua diagnostics through the status/log diagnostic path
- **AND** format preview and save SHALL use committed chapter values and SHALL NOT apply the expression draft
- **WHEN** the user applies a valid candidate
- **THEN** the UI SHALL commit the candidate as one undoable transaction

#### Scenario: Simple arithmetic remains approachable through Lua
- **WHEN** the user enters a simple Lua arithmetic expression such as `t + 1`
- **THEN** the live candidate preview SHALL apply the transform without requiring `return`, a function wrapper, or any legacy postfix expression syntax

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

### Requirement: Unified settings command
The Avalonia shell SHALL expose a unified Settings command that opens a settings panel for durable application, external tool, appearance, and platform preferences.

#### Scenario: Settings command exists
- **WHEN** the main window ViewModel is constructed
- **THEN** it SHALL expose a Settings command that can be invoked by the shell and tested without creating platform UI directly

#### Scenario: Settings panel opens as a secondary tool window
- **WHEN** the user invokes Settings
- **THEN** the window service SHALL show a dedicated settings view and ViewModel instead of building the settings UI imperatively inside the window service

#### Scenario: Settings entry stays compact
- **WHEN** the main window is rendered
- **THEN** the Settings entry SHALL be reachable from a compact command surface without adding a large preferences section to the primary chapter workflow

### Requirement: Settings panel groups durable configurable features
The settings panel SHALL organize the durable configurable features discovered from the current app into general, external tools, output defaults, appearance, and platform integration groups.

#### Scenario: General settings are editable
- **WHEN** the settings panel is opened
- **THEN** it SHALL expose UI language and default save directory controls

#### Scenario: External tool settings are editable
- **WHEN** the settings panel is opened
- **THEN** it SHALL expose MKVToolNix/mkvextract and ffprobe path controls with browse, clear, and validation status behavior

### Requirement: Async load updates observable UI state safely
The Avalonia shell SHALL keep file IO and parsing separate from observable UI state mutation during asynchronous loads.

#### Scenario: Import work completes asynchronously
- **WHEN** a load service or importer completes asynchronously after performing file IO or parsing
- **THEN** the main-window ViewModel SHALL update `Rows`, `ClipOptions`, selected clip state, status, and progress through its command flow
- **AND** background import code SHALL NOT mutate UI-bound `ObservableCollection` instances directly

#### Scenario: Load progress does not bypass ViewModel state
- **WHEN** an importer reports intermediate progress during a load
- **THEN** progress updates SHALL be surfaced through ViewModel state or command-owned progress handling
- **AND** the view SHALL NOT rely on importer callbacks to update controls directly

### Requirement: Main window ViewModel stays independent of Avalonia windows
The main-window ViewModel SHALL remain independent of Avalonia `Window`, storage-provider, and control instances.

#### Scenario: File picking remains view or service responsibility
- **WHEN** a user browses for a source file, MPLS file, chapter-name template, or save directory
- **THEN** the selection SHALL be performed by a file-picker service or view adapter
- **AND** the ViewModel SHALL receive the selected path or cancellation result without holding an Avalonia `Window`

#### Scenario: Routine state does not require manual window refresh
- **WHEN** clip selection, combine state, save availability, selected rows, frame options, naming options, expression options, or current chapter data changes
- **THEN** the ViewModel SHALL raise observable property or command-availability notifications sufficient for bound controls to update
- **AND** the window SHALL NOT need to run a routine manual refresh method to synchronize that state

### Requirement: Import formats are routed through the importer registry
The Avalonia shell SHALL route source loading through the load service and importer registry rather than constructing importers from ViewModel or window extension switches.

#### Scenario: New source format is added
- **WHEN** support for a new chapter source format is introduced
- **THEN** runtime selection SHALL be added through an `IChapterImporterRegistry` implementation or registered importer path
- **AND** `MainWindowViewModel` SHALL continue to call the load service without branching on the file extension

#### Scenario: Import fallback diagnostics are logged structurally
- **WHEN** the primary importer cannot be invoked and a fallback importer is used
- **THEN** the load pipeline SHALL return or log a structured diagnostic identifying the primary importer, fallback importer, source path context, and reason for fallback

#### Scenario: Output defaults are editable
- **WHEN** the settings panel is opened
- **THEN** it SHALL expose default save format and default XML chapter language rather than the current working values being edited on the main screen

#### Scenario: Appearance settings are editable
- **WHEN** the settings panel is opened
- **THEN** it SHALL expose a preset-first theme selector rather than the legacy six-slot color editor
- **AND** the available built-in presets SHALL include `Avalonia Default` and the documented `Solarized`, `Gruvbox`, and `Ayu` families
- **AND** it SHALL NOT expose manual theme color editors in the first preset-selection release
- **AND** it SHALL expose independent UI and monospace font-family selectors without exposing font-size controls

#### Scenario: High-frequency main workflow controls stay on the main screen
- **WHEN** the settings panel is opened
- **THEN** high-frequency current working controls such as naming mode, template use, order shift, expression, frame-rate choice, and round-frames SHALL remain on the main workflow surface instead of being duplicated as settings

#### Scenario: Platform integration is gated
- **WHEN** file association or another platform-specific integration is shown in settings
- **THEN** it SHALL be hidden, disabled, or clearly marked unsupported when the current platform cannot perform it

### Requirement: Settings changes apply predictably
The settings panel SHALL save, apply, reset, validate, and discard changes in a way that keeps the main ViewModel, visible runtime state, and persisted settings consistent.

#### Scenario: Runtime-safe settings apply immediately
- **WHEN** the user changes a runtime-safe setting in the settings panel
- **THEN** the running shell SHALL update language, default save directory, save defaults, frame accuracy tolerance, selected appearance preset, selected UI font, and selected monospace font without waiting for Save
- **AND** the typed settings stores SHALL NOT be written solely because the value changed in the panel

#### Scenario: Save persists currently applied settings
- **WHEN** the user saves settings after making live changes
- **THEN** the current settings panel values SHALL be written to the typed settings stores
- **AND** the running shell SHALL continue using those values without requiring a restart
- **AND** the settings panel SHALL no longer be considered dirty

#### Scenario: Closing with unsaved live changes requires confirmation
- **WHEN** the user closes the settings window after changing settings that have not been saved
- **THEN** the shell SHALL show a localized confirmation prompt before closing
- **AND** canceling the prompt SHALL keep the settings window open with the live changes still applied

#### Scenario: Discarding unsaved live changes restores saved settings
- **WHEN** the user confirms discarding unsaved settings changes while closing the settings window
- **THEN** the shell SHALL restore the last loaded or saved settings to the running main ViewModel and appearance services
- **AND** the settings window SHALL be allowed to close
- **AND** the typed settings stores SHALL remain unchanged

#### Scenario: Reset restores defaults
- **WHEN** the user resets a settings group to defaults
- **THEN** the panel SHALL restore the same defaults used by a fresh application start
- **AND** reset values SHALL apply immediately to the running shell while still requiring Save for persistence
- **AND** appearance reset SHALL select the `Avalonia Default` theme preset, system UI font default, and system monospace font default

#### Scenario: Invalid settings are surfaced
- **WHEN** a setting value is invalid or an external tool path cannot be resolved
- **THEN** the settings panel SHALL show a localized validation message and SHALL NOT silently discard the user's input

### Requirement: Settings exposes two simple font selectors
The Avalonia settings panel SHALL present UI and monospace font-family choices as two independent selectors in the Appearance section.

#### Scenario: Font selectors list defaults and installed families
- **WHEN** the Appearance section is rendered
- **THEN** each font selector SHALL show its localized default option followed by the available installed family names
- **AND** choosing an option in one selector SHALL NOT change the other selector

#### Scenario: Installed font options render progressively in their own family
- **WHEN** installed font options are realized in either selector's visible viewport
- **THEN** each installed family name SHALL render using that family
- **AND** off-screen font-specific option rendering SHALL remain deferred until scrolling realizes those items

#### Scenario: Font previews follow selection
- **WHEN** the user changes either font selection
- **THEN** a stable-size non-editable preview for that category SHALL update using the effective selected family
- **AND** the preview SHALL contain representative Latin letters, digits, punctuation, and localized text
- **AND** the preview SHALL expose a localized accessible name identifying its category and effective selection

#### Scenario: Font labels follow runtime language changes
- **WHEN** the UI language changes while the settings window is open
- **THEN** font labels, default option labels, preview text, and accessible names SHALL refresh from Simplified Chinese, English, or Japanese resources
- **AND** installed font options SHALL prefer a family name matching the active culture, then another name for the same language, then the canonical family name
- **AND** localized font metadata SHALL remain lazily resolved for realized options rather than eagerly loading every font
- **AND** the canonical selected family names SHALL remain unchanged

#### Scenario: Unavailable saved fonts appear as defaults
- **WHEN** Settings loads a saved family that is unavailable in the current catalog
- **THEN** the affected selector and preview SHALL show that category's effective default
- **AND** the settings panel SHALL NOT become dirty solely because fallback resolution occurred

### Requirement: Avalonia surfaces consume semantic font resources
The Avalonia shell SHALL apply UI and monospace typography through separate semantic dynamic resources.

#### Scenario: UI font covers normal application surfaces
- **WHEN** a UI font is applied
- **THEN** normal windows, controls, menus, table headers, dialogs, settings content, and tool surfaces SHALL use the semantic UI font resource
- **AND** existing visible surfaces SHALL refresh without reopening their windows

#### Scenario: Monospace font covers fixed-width content
- **WHEN** a monospace font is applied
- **THEN** expression and script editors, chapter-table data cells, text previews, logs, and other content intentionally using fixed-width alignment SHALL use the semantic monospace font resource
- **AND** existing visible editors and text surfaces SHALL refresh without reconstruction

#### Scenario: Chapter table separates cell and header typography
- **WHEN** different effective UI and monospace fonts are applied while the chapter table is visible
- **THEN** displayed and editing data cells SHALL use the semantic monospace font resource
- **AND** column headers SHALL continue using the semantic UI font resource
- **AND** existing cells and headers SHALL refresh without reopening or rebuilding the table

#### Scenario: Chapter-number shift uses monospace numeric entry
- **WHEN** the chapter-number shift control is visible and a monospace font is applied
- **THEN** its numeric input and displayed value SHALL use the semantic monospace font resource
- **AND** its descriptive label SHALL continue using the semantic UI font resource

#### Scenario: Font categories remain visually independent
- **WHEN** different effective families are selected for UI and monospace content
- **THEN** normal UI text SHALL resolve the UI family
- **AND** fixed-width content SHALL resolve the monospace family rather than inheriting the UI family

#### Scenario: Icon glyphs retain their icon family
- **WHEN** either semantic font resource changes
- **THEN** icon-library controls SHALL retain the font family required to render their glyphs
- **AND** settings and main-workflow command icons SHALL remain visible

#### Scenario: Newly opened surfaces use current fonts
- **WHEN** a window, popup, editor, preview, or log surface opens after font settings were applied
- **THEN** it SHALL resolve the current semantic UI or monospace resource according to its content role

### Requirement: Startup font application is resilient
The Avalonia application SHALL establish usable font resources before the main window is created and then apply persisted choices without blocking startup.

#### Scenario: Defaults exist before asynchronous load completes
- **WHEN** application composition starts before font settings have loaded
- **THEN** both semantic font resources SHALL contain their defaults
- **AND** the main window SHALL be able to render immediately

#### Scenario: Persisted settings replace startup defaults
- **WHEN** valid persisted font settings finish loading
- **THEN** the application SHALL apply both effective selections to the semantic resources
- **AND** already-created surfaces SHALL refresh through dynamic resource resolution

#### Scenario: Font settings load failure keeps the shell usable
- **WHEN** loading font settings fails because of malformed data, I/O, or access errors
- **THEN** the application SHALL retain both defaults
- **AND** the main window SHALL continue opening and remain usable

### Requirement: Settings appearance presets use semantic surface coverage
The Avalonia settings panel SHALL define theme appearance through semantic surface coverage rather than through implementation-oriented legacy slot names.

#### Scenario: Semantic theme fields describe UI responsibilities
- **WHEN** the selected theme preset is applied
- **THEN** the resolved theme tokens SHALL map to `WindowBackground`, `PanelBackground`, `ControlBackground`, `ControlForeground`, `MutedForeground`, `Accent`, `AccentForeground`, `Border`, `HoverBackground`, and `ActiveBackground`
- **AND** each field SHALL control a predictable group of shell, tool-window, input, border, and interaction surfaces

#### Scenario: Preset base variant follows the selected palette
- **WHEN** a light or dark theme preset is applied
- **THEN** the application's Avalonia theme variant SHALL switch to the preset's declared light or dark base variant
- **AND** Fluent controls, DataGrid, popups, and editor chrome SHALL use the same base variant as the semantic palette

#### Scenario: DataGrid column headers follow the selected preset
- **WHEN** a theme preset is applied while the chapter grid is visible
- **THEN** every `DataGridColumnHeader` SHALL use semantic background, foreground, border, hover, and pressed colors from the selected preset
- **AND** header text and sort glyphs SHALL remain readable against the header background
- **AND** switching between representative light and dark presets SHALL update existing column headers without reopening the window
- **AND** no column header SHALL retain a stale Fluent default or previously selected preset brush

### Requirement: Settings theme preset selection stays simple
The Avalonia settings panel SHALL keep theme preset selection in a single simple selector rather than splitting family and variant into separate controls.

#### Scenario: Preset selector lists variants directly
- **WHEN** the appearance settings section is rendered
- **THEN** the preset selector SHALL list each built-in theme variant directly as a selectable option
- **AND** the user SHALL NOT be required to choose family and variant from separate selectors

#### Scenario: Palette preview follows selection
- **WHEN** the user selects a different preset
- **THEN** a compact, non-editable palette preview SHALL update to represent that preset's semantic colors
- **AND** the preview SHALL expose an accessible name derived from the localized preset name

#### Scenario: Preset names follow runtime language changes
- **WHEN** the UI language changes while the settings window is open
- **THEN** preset display names and appearance labels SHALL refresh from Simplified Chinese, English, or Japanese resources
- **AND** the stable selected preset id SHALL remain unchanged

### Requirement: Frame accuracy is visual state
The Avalonia shell SHALL render frame accuracy as visual styling rather than as `K` or `*` characters in frame text.

#### Scenario: Accurate rounded frames use the semantic accurate color
- **WHEN** a chapter row has rounded frame display and the frame calculation error is within tolerance
- **THEN** the frame cell SHALL show only the numeric frame text
- **AND** the frame text SHALL use the dedicated semantic accurate brush
- **AND** the cell SHALL render one text layer
- **AND** the cell SHALL NOT apply bitmap effects such as drop shadows

#### Scenario: Inexact rounded frames use the semantic inexact color
- **WHEN** a chapter row has rounded frame display and the frame calculation error exceeds tolerance
- **THEN** the frame cell SHALL show only the numeric frame text
- **AND** the frame text SHALL use the dedicated semantic inexact brush
- **AND** the cell SHALL render one text layer
- **AND** the cell SHALL NOT apply bitmap effects such as drop shadows

#### Scenario: Unrounded frames are neutral
- **WHEN** frame rounding is disabled
- **THEN** the frame cell SHALL show the unrounded numeric frame text
- **AND** the frame text SHALL use the dedicated semantic neutral brush rather than accurate or inexact styling

#### Scenario: Frame edits use numeric text
- **WHEN** a user edits the frame cell
- **THEN** the committed value SHALL be interpreted as numeric frame text without requiring or preserving `K` or `*` suffixes

#### Scenario: Frame accuracy tolerance is configurable
- **WHEN** the user opens Settings
- **THEN** the settings panel SHALL expose frame accuracy tolerance as a continuous slider from `0.01` through `0.30`
- **AND** the slider SHALL show recommended tick marks at each `0.05` value
- **AND** values within `0.01` of a recommended tick SHALL snap to that recommended value
- **AND** the current tolerance value SHALL be displayed adjacent to the slider
- **AND** saving settings SHALL persist that tolerance for future frame accuracy classification

#### Scenario: Frame accuracy tolerance has a recommended default
- **WHEN** settings have no frame accuracy tolerance or reset to defaults
- **THEN** the shell SHALL use `0.15` as the default tolerance value

#### Scenario: Invalid frame accuracy tolerance is normalized
- **WHEN** settings contain a non-positive or excessive frame accuracy tolerance
- **THEN** the shell SHALL normalize it to the supported `0.01` through `0.30` range before applying frame accuracy classification

### Requirement: Restored conversion tools are reachable
The Avalonia shell SHALL expose restored legacy conversion tools through compact command surfaces without coupling conversion logic to the view.

#### Scenario: Celltimes conversion is discoverable
- **WHEN** a chapter set is loaded and a valid frame rate is selected
- **THEN** the UI SHALL provide a compact command or tool entry for exporting or generating celltimes output

#### Scenario: Conversion commands delegate to services
- **WHEN** a restored conversion command is invoked
- **THEN** the ViewModel SHALL delegate conversion to Core or application services and display success or structured diagnostics through the existing localized status/dialog path

### Requirement: XML language selection supports ISO language codes
The Avalonia shell SHALL allow XML export language selection from an ISO language-code catalog comparable to the legacy language list.

#### Scenario: Common XML language defaults remain quick
- **WHEN** XML language selection is shown
- **THEN** common values including `und`, `zh`, `ja`, and `en` SHALL remain available without typing a custom code

#### Scenario: Less common ISO language can be selected
- **WHEN** a user needs an ISO language code outside the short common list
- **THEN** the UI SHALL allow selecting or entering a valid ISO language code and SHALL use that code for XML export

#### Scenario: Invalid XML language is rejected
- **WHEN** a user enters an invalid XML language code
- **THEN** the UI SHALL prevent save or show a localized validation diagnostic instead of silently exporting the invalid value

### Requirement: Main-window selectors expose readable display content
The Avalonia main window SHALL render clip and XML language selector options with user-readable display content while preserving the existing underlying selection values used by commands, import/export, settings, and shortcuts.

#### Scenario: Clip selector displays main content with remarks
- **WHEN** a source load result contains multiple clip, playlist, program-chain, or edition options
- **THEN** the clip selector SHALL display each option with the primary source content first and secondary details such as chapter count as remark-style supporting content
- **AND** selecting an option SHALL continue to update `SelectedClipIndex` and the current chapter rows exactly as before

#### Scenario: XML language selector displays readable language names
- **WHEN** XML language selection is shown
- **THEN** the selector SHALL display each language option with both the language code and a readable language name
- **AND** changing the selector SHALL continue to update `XmlLanguage` to the selected ISO code used for XML export

### Requirement: Main-window orchestration uses workspace-consuming coordinators
The Avalonia main shell SHALL implement load/append/save, clip editing, projection/row refresh, and status/diagnostic presentation as dedicated orchestration components that consume `ChapterWorkspace` (or equivalent session owner) rather than permanently accumulating all orchestration as one undifferentiated multi-thousand-line ViewModel. The main ViewModel SHALL remain the bindable command and property façade for XAML.

#### Scenario: Load and save orchestration is not only partial methods on the ViewModel type
- **WHEN** a maintainer inspects the production load, append, and save flow after this change
- **THEN** the async load/append commit and save coordination logic SHALL live in a named workflow/coordinator type that uses workspace revision commit APIs
- **AND** the ViewModel SHALL invoke that workflow rather than owning the full implementation only as private partial methods with no extracted owner

#### Scenario: Projection and row refresh share a projection façade
- **WHEN** expression, naming mode, order shift, or frame display options change and rows refresh
- **THEN** projection application and row materialization SHALL route through a dedicated projection façade/coordinator shared with preview/save option building
- **AND** the façade SHALL read projection/export state from the workspace rather than a second parallel field set

#### Scenario: User-visible load/edit/save behavior is preserved
- **WHEN** the user loads a source, edits a cell, toggles combine, applies expression projection, and saves
- **THEN** those workflows SHALL remain available with the same observable outcomes as before the coordinator extraction for successful paths covered by existing unit and Headless tests

### Requirement: Settings modules are real ownership or removed
Settings durable preference groups that claim modular ownership (output defaults, external tools, about/runtime info, appearance) SHALL either own their state and related actions used by the settings tool, or those unused module types SHALL be removed. The repository SHALL NOT keep permanently unreferenced “ownership module” types as documentation substitutes.

#### Scenario: No dead settings module types remain
- **WHEN** the settings tool implementation is complete for this change
- **THEN** every `Settings*Module` type shipped under the Avalonia settings ViewModels folder SHALL be referenced by the settings tool composition or production code path
- **OR** unused module types SHALL be deleted from the tree

#### Scenario: Appearance ownership stays dedicated
- **WHEN** theme or font settings change in Settings
- **THEN** appearance selection and font catalogs SHALL continue to be owned by a dedicated appearance ViewModel/module used by the settings tool

#### Scenario: External tools actions remain testable without about-panel logic
- **WHEN** external tool browse, clear, validate, or discover actions run
- **THEN** those actions SHALL remain implementable without requiring about-panel-only state to execute

### Requirement: Expression editor uses composition-owned authoring services
Production construction of the Lua expression editor SHALL obtain `IExpressionAuthoringService` (or an equivalent authoring analysis service) from application composition or an injected dependency, rather than permanently constructing a private default authoring service that can diverge from the composition-root expression engine policy.

#### Scenario: Production editor shares engine policy with the shell
- **WHEN** the main window or expression tool hosts an expression editor in the running application
- **THEN** authoring analysis SHALL use the composition-owned expression engine/authoring service path
- **AND** the control SHALL NOT be the only place that constructs a separate default Lua engine for production analysis

#### Scenario: Both XAML editor hosts receive the composed authoring service
- **WHEN** the main-window or expression-tool XAML creates an `ExpressionEditor`
- **THEN** its authoring-service property SHALL be bound to a service supplied by the composition root
- **AND** the expression-tool path SHALL carry that service through its tool-window creation context

#### Scenario: Editor presentation remains separable from analysis
- **WHEN** token coloring, completion chrome, or diagnostic underlines render
- **THEN** analysis results SHALL still come from the Core authoring service contract
- **AND** control-specific rendering helpers SHALL remain separable from analysis

### Requirement: Single command surface for main workflow actions
The Avalonia main window SHALL expose one primary command surface for workflow actions through the main ViewModel. Window-level command wrappers MAY exist only when they add view-only parameters such as picker results or selected row indexes, and SHALL NOT duplicate independent can-execute business rules or re-implement load/save/combine/delete business semantics.

#### Scenario: Business can-execute lives on the ViewModel
- **WHEN** save, combine, append MPLS, delete, insert, or related-media availability changes
- **THEN** the ViewModel commands SHALL raise the authoritative can-execute changes
- **AND** any window wrappers SHALL derive enabled state from those commands or equivalent ViewModel capability flags

#### Scenario: Keyboard routing does not fork business semantics
- **WHEN** a documented shortcut is pressed
- **THEN** the gesture SHALL route to the same ViewModel command path used by the corresponding visible control or menu action
- **AND** load/save shortcuts SHALL NOT maintain a separate incompatible command implementation

#### Scenario: Window wrappers stay view adapters only
- **WHEN** `MainWindow.axaml.cs` defines a command for browse-load, save-to, delete selected, or insert selected
- **THEN** that command SHALL only gather view parameters (picker paths, selected indexes) and forward to the ViewModel command or method surface
- **AND** it SHALL NOT maintain a second independent business status/progress pipeline for those actions

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
The settings panel implementation SHALL modularize durable preference groups so output defaults, external tools, appearance, and about/runtime info are not permanently accumulated as one undifferentiated mega-ViewModel without internal ownership boundaries. Saved and draft settings snapshots SHALL have one explicit lifecycle owner separate from the Avalonia binding properties.

#### Scenario: Appearance remains a dedicated module
- **WHEN** theme or font settings change
- **THEN** appearance selection, preview metadata, and font catalogs SHALL continue to be owned by a dedicated appearance module/ViewModel

#### Scenario: External tool path editing is isolatable
- **WHEN** external tool browse/clear/validate/discover actions are exercised
- **THEN** those actions SHALL be implementable and testable as an external-tools settings module without requiring unrelated about-panel logic

#### Scenario: Settings snapshots preserve edit lifecycle
- **WHEN** settings are loaded, changed, live-applied, saved, reset, or discarded
- **THEN** a dedicated snapshot coordinator SHALL keep the saved snapshot distinct from the current draft
- **AND** the ViewModel SHALL preserve the existing `HasUnsavedChanges`, load-failure, appearance rollback, and live-apply behavior

### Requirement: Desktop application uses the imported user interface foundation
The Avalonia application SHALL load the imported theme and reusable control-style resources. It SHALL use the registered FontAwesome provider for product icons.

#### Scenario: Application starts with imported resources
- **WHEN** the Avalonia application starts
- **THEN** every imported theme token and product FontAwesome icon SHALL resolve
- **AND** every reusable imported control style SHALL compile under Avalonia 12.1

#### Scenario: Product control uses the global style layer
- **WHEN** a ChapterTool window displays a standard control
- **THEN** the control SHALL receive the ported SourceGit base style
- **AND** a later ChapterTool-specific style MAY refine product constraints

#### Scenario: Migration excludes Git-domain user interfaces
- **WHEN** the imported resource layer is compared with its SourceGit source
- **THEN** the migration SHALL exclude only selectors that require SourceGit Git-domain types
- **AND** the migration notice SHALL identify each exclusion

### Requirement: Imported work retains attribution
ChapterTool SHALL keep an attribution record for the imported SourceGit resources.

#### Scenario: Reviewer inspects third-party evidence
- **WHEN** a reviewer opens the imported theme resource directory
- **THEN** the directory SHALL identify the upstream repository and source revision
- **AND** `Themes.axaml` and `Styles.axaml` SHALL each include the SourceGit copyright and MIT license notice

### Requirement: Global styles preserve workflow usability
The ported style layer SHALL keep ChapterTool workflows usable at supported window sizes. Preview readiness, diagnostics, and inline comparison SHALL NOT add rows to the bottom options form or change the allocated chapter viewport bounds at a fixed window size and input layout. Narrow comparison cells MAY use two labeled lines without reducing the allocated viewport.

#### Scenario: Main window uses the global styles
- **WHEN** the main window opens
- **THEN** the load and save area, chapter grid, options area, and status strip SHALL remain available
- **AND** controls SHALL not overlap

#### Scenario: Tool window uses a narrow supported size
- **WHEN** a tool window is resized to its minimum width
- **THEN** primary actions SHALL remain visible
- **AND** text SHALL remain inside its control bounds

#### Scenario: Inline comparison preserves chapter space
- **WHEN** a fixed-size main window moves through empty, computing, ready, invalid, unchanged, and stale preview states
- **THEN** its allocated chapter viewport bounds SHALL remain equal within layout rounding tolerance
- **AND** no detailed result panel SHALL appear below the options form
- **AND** responsive comparison SHALL remain readable without overlapping values

### Requirement: Settings window uses compact imported form composition
The settings window SHALL use the imported input and action styles.

#### Scenario: Settings form displays path input
- **WHEN** a settings tab displays a path input
- **THEN** the input SHALL use a consistent 32-pixel height and imported border states
- **AND** browse and clear actions SHALL appear inside the input right-content area

#### Scenario: Settings footer displays actions
- **WHEN** the settings window is open
- **THEN** folder access and status SHALL remain on the left side of the footer
- **AND** reset and save actions SHALL form a right-aligned group with equal height

#### Scenario: Settings window uses its minimum width
- **WHEN** the settings window is resized to its supported minimum width
- **THEN** footer actions SHALL remain inside the window
- **AND** form inputs and embedded actions SHALL not overlap

#### Scenario: Settings tabs use a consistent form ratio
- **WHEN** the user changes between settings form tabs
- **THEN** each form SHALL use the same responsive label and editor column ratio
- **AND** primary editors SHALL share the same left and right boundaries

### Requirement: Main view receives embedded presentation explicitly
The shared `MainView` SHALL receive an explicit `IEmbeddedToolPresenter` when the host supports embedded tool presentation. It SHALL not discover that capability by casting the auxiliary-tool host.

#### Scenario: Native-window host supplies no embedded content
- **WHEN** the desktop Native Window host constructs `MainView`
- **THEN** it SHALL supply a no-content presenter
- **AND** `MainView` SHALL keep the `ToolContentHost` region hidden without inspecting the concrete auxiliary-tool host type

#### Scenario: Embedded host changes current tool content
- **WHEN** an Embedded host changes the active tool content
- **THEN** the presenter SHALL notify `MainView`
- **AND** `MainView` SHALL update content and visibility without changing the main ViewModel

### Requirement: Main shell commands use stable tool identifiers
Main-shell commands that open auxiliary tools SHALL use a stable typed tool identifier and the shared auxiliary-tool host contract.

#### Scenario: Tool command opens a registered tool
- **WHEN** the user invokes Preview, Settings, Language, Expression, Log, Template Names, Zones, or Forward Shift
- **THEN** the command SHALL pass the corresponding stable tool identifier
- **AND** it SHALL not pass the concrete `MainWindowViewModel` through an `object?` parameter

#### Scenario: Unknown tool identifier is safe
- **WHEN** a host receives an unregistered tool identifier
- **THEN** it SHALL return a safe no-op or localized placeholder result
- **AND** it SHALL not terminate the main shell

### Requirement: Secondary tools keep narrow workspace dependencies
Secondary tool ViewModels SHALL receive only the narrow workspace or host ports required by their behavior. They SHALL not depend on the concrete `MainWindowViewModel` type.

#### Scenario: Preview tool changes format
- **WHEN** the preview tool changes its export format
- **THEN** it SHALL use the export preference port
- **AND** it SHALL not require unrelated main-window commands

#### Scenario: Expression tool applies a script
- **WHEN** the expression tool loads or applies a script
- **THEN** it SHALL use the expression session port and host file-picker port
- **AND** it SHALL not require the concrete main-window ViewModel

#### Scenario: Tool changes refresh the main shell
- **WHEN** the expression tool applies a script
- **THEN** the session facade SHALL refresh the main-shell expression fields, row grid, and status through its notification port
- **AND** the tool SHALL not reach the concrete main-window ViewModel for notification

### Requirement: Main shell behavior remains host-neutral
Host selection SHALL change platform effects and tool presentation without changing workspace revision, clip session, projection, export, localization, or command-state behavior.

#### Scenario: Same load workflow runs in two hosts
- **WHEN** two Avalonia hosts invoke the same typed source load command with equivalent source documents
- **THEN** both shells SHALL apply the same workspace commit and projection rules
- **AND** only their host-specific source and surface effects may differ

### Requirement: Avalonia expression editing provides a live read-only preview
Avalonia expression inputs SHALL refresh a read-only candidate preview while the user edits the script or selects a preset. The preview SHALL remain separate from the editable chapter grid and SHALL require an explicit Apply action to change the document.

#### Scenario: Main expression input refreshes the candidate preview
- **WHEN** the user edits the expression in the main window or selects an expression preset
- **THEN** the UI SHALL refresh the candidate from the current committed document
- **AND** it SHALL display changed chapter values or a no-change result
- **AND** the main chapter grid SHALL continue to display committed values

#### Scenario: Expression tool shows live differences and diagnostics
- **WHEN** the expression tool is open and the user edits its Lua script
- **THEN** the UI SHALL refresh a read-only before-and-after preview
- **AND** it SHALL show current Lua and document validation errors
- **AND** it SHALL disable Apply while the candidate is invalid

#### Scenario: User applies or cancels the live candidate
- **WHEN** the user applies a valid current expression candidate
- **THEN** the UI SHALL commit one history transaction and refresh the grid from the committed document
- **AND** format preview and save SHALL use those committed values without rerunning the expression
- **WHEN** the user cancels the candidate
- **THEN** the UI SHALL clear preview data without changing document content or history

#### Scenario: Expression text is a draft without a persistent enable toggle
- **WHEN** the user edits expression text or selects a preset
- **THEN** the UI SHALL treat that value as an unapplied operation draft
- **AND** it SHALL require an explicit Apply action to commit it
- **AND** it SHALL NOT expose a persistent checkbox that silently enables expression projection during preview or save

### Requirement: History opens in a button-invoked dialog
Avalonia 编辑历史 MUST 由历史按钮打开独立模态弹框。主窗口 MUST NOT 常驻显示历史列表或为历史预留侧栏空间。历史弹框 MUST 使用当前文档会话的历史和命令。打开、关闭或调整弹框尺寸 MUST NOT 创建历史事务。

#### Scenario: Load without opening history
- **WHEN** 用户加载章节文档但没有点击历史按钮
- **THEN** 主窗口不显示历史列表或历史侧栏占位
- **AND** 章节工作区使用完整中央宽度
- **AND** 历史按钮具有本地化的可访问名称

#### Scenario: Open and dismiss history
- **WHEN** 用户点击历史按钮
- **THEN** 一个属于主窗口的模态历史弹框打开
- **AND** 弹框显示当前节点、撤销、重做、分支导航和当前会话生命周期提示
- **AND** 主窗口后台点击、快捷键和拖放不能通过弹框修改文档
- **WHEN** 用户通过关闭按钮、窗口关闭或 Escape 退出弹框
- **THEN** 弹框关闭并归还焦点到历史按钮
- **AND** 仅关闭弹框不会回滚弹框内已执行的历史导航
- **AND** 主窗口章节区域不因开关弹框而改变分配尺寸

#### Scenario: Navigate history in the dialog
- **WHEN** 用户在弹框中执行撤销、重做或选择保留的另一分支
- **THEN** 操作调用当前会话的既有历史命令
- **AND** 弹框更新当前节点、按钮可用性和操作描述
- **AND** 主窗口章节数据刷新为对应历史状态
- **AND** 关闭并重新打开后显示相同的当前历史状态

#### Scenario: Reopen history and release its resources
- **WHEN** 用户反复打开和关闭历史弹框
- **THEN** 同一时刻最多存在一个该会话的历史弹框
- **AND** 已关闭弹框释放内容树及会话订阅
- **AND** 无可用文档会话时历史按钮禁用

#### Scenario: Inspect a long branching history
- **WHEN** 历史包含大量条目、多个分支和长操作描述
- **THEN** 用户能通过虚拟化列表滚动及键盘导航访问保留节点
- **AND** 当前节点和分支关系不只依赖颜色或空格识别
- **AND** 撤销、重做和关闭操作保持可达

### Requirement: Chapter grid fills its allocated workspace
章节表格 MUST 填充顶部工具区与底部选项之间的中央分配区。该区域 MUST NOT 为已隐藏的空状态或历史保留独立空白行。空状态 MUST 在相同工作区居中显示。列标题、值和编辑控件 MUST 保持可读。

#### Scenario: Loaded table uses the full central area
- **WHEN** 用户在 760×600、1280×800 或 760×520 的窗口加载足够多的章节
- **THEN** 表格边界覆盖中央分配区的完整可用宽度和高度
- **AND** 表格下方没有额外的等高空白区域
- **AND** 用户能滚动到最后一个章节并提交单元格编辑
- **AND** 表格虚拟化和扩展选择继续工作

#### Scenario: Empty and loaded states share the same area
- **WHEN** 窗口尚未加载章节
- **THEN** 空状态图像和提示在章节工作区居中
- **AND** 空状态不会阻止加载或拖放操作
- **WHEN** 章节加载成功
- **THEN** 空状态隐藏且表格使用同一完整工作区

#### Scenario: Resize with long chapter values
- **WHEN** 用户调整窗口尺寸并编辑长章节名称、时间或帧值
- **THEN** 列保留合理最小宽度，标题和内容不相互覆盖
- **AND** 必要的滚动保持可用
- **AND** 编辑按稳定列身份提交，选择和滚动位置在身份仍有效时保持

### Requirement: Main-window content previews have a bounded review region
表达式及主窗口其他待确认内容操作 MUST 显示在独立结果区域。该区域 MUST 显示操作名称、作用范围、完整变更计数和可检查的结果。状态条 MUST 只显示简短状态，不能作为唯一的差异展示。Apply 和 Cancel MUST 位于结果滚动区之外。没有预览时，结果区域 MUST 折叠。

#### Scenario: Preview an expression without overlapping options
- **WHEN** 用户输入有效表达式 `t + 1` 并得到候选
- **THEN** 预览出现在独立结果区域
- **AND** 保存格式、XML 语言、命名、编号偏移和表达式输入不被结果文字覆盖
- **AND** 用户能检查预览并点击 Apply 或 Cancel

#### Scenario: Review another content operation
- **WHEN** 用户准备命名、编号偏移或帧率变换候选
- **THEN** 同一结果区域显示该操作的名称、范围和修改前后值
- **AND** 用户不需要从状态条的技术摘要判断将要提交的内容
- **AND** Apply 绑定该区域所展示的候选

#### Scenario: Inspect all differences from a compact result
- **WHEN** 候选包含 1,000 个章节的变化，紧凑区域只能显示部分条目
- **THEN** 摘要计数覆盖完整候选
- **AND** 界面明确表示当前只显示部分条目
- **AND** 所有差异都能通过滚动或完整差异入口访问
- **AND** 完整差异入口展示同一个候选且不重新计算表达式

#### Scenario: Clear a preview
- **WHEN** 用户取消候选、应用成功或文档会话结束
- **THEN** 对应预览结果和确认操作清除
- **AND** 结果区域不保留空白占位或过期值

### Requirement: Main-window preview values use localized business formats
主窗口预览 MUST 从 typed snapshot 展示本地化业务值。它 MUST 按章节组织修改前、修改后和变化量。时间变化、帧信息更新和属性变化 MUST 明确区分。普通结果 MUST NOT 直接显示 ticks、内部字段标识、原始枚举或对象转储。

#### Scenario: Review a one-second change
- **WHEN** 候选将章节时间从 00:00:00 改为 00:00:01
- **THEN** 对比显示章节身份、可读的原时间、目标时间和正一秒变化量
- **AND** 同一章节的帧信息变化在该章节的明细中显示
- **AND** 原始 `StartTicks` 数字不是默认时间表示

#### Scenario: Distinguish frame-only and property-only changes
- **WHEN** 候选只更新帧信息或持久化属性而没有时间变化
- **THEN** 摘要明确说明没有章节时间变化
- **AND** 相关更新以本地化字段和原值、目标值显示
- **AND** 同一章节的多种变化只计为一个受影响章节

#### Scenario: Preserve precise and missing values
- **WHEN** 时间差小于一毫秒，或原帧信息缺失而目标为零帧
- **THEN** 时间精度能区分实际变化且变化量不显示为零
- **AND** 缺失帧值与零帧使用不同的业务表示
- **AND** 帧率依据不同的两侧各自显示依据且不生成误导的帧差

#### Scenario: Localize normal review content
- **WHEN** 用户使用中文或英文检查预览
- **THEN** 字段标签、单位、空值、状态和错误摘要使用当前语言
- **AND** accuracy 等枚举含义以业务说明展示
- **AND** 长文本可换行或展开且含义不只依赖颜色

### Requirement: Preview UX preserves candidate transactions
预览展示和布局调整 MUST 保留现有候选事务语义。准备 MUST 保持已提交章节和历史不变。Apply MUST 只提交展示的有效且当前的候选。输入、目标或历史变化 MUST 使旧候选不可应用。取消、失败和过期状态 MUST 不提交部分结果。

#### Scenario: Apply and undo the reviewed result
- **WHEN** 用户确认一个有效且当前的候选
- **THEN** 提交值与展示值完全相同且只创建一个事务
- **AND** 应用路径不重新计算表达式
- **AND** 一次撤销恢复该事务的全部变化

#### Scenario: Cancel or reject an invalid result
- **WHEN** 用户取消预览，或候选无效、无变化、等待计算或正在计算
- **THEN** 不创建内容事务
- **AND** 不符合提交条件时 Apply 禁用
- **AND** 错误和等待状态位于结果区域且不覆盖输入

#### Scenario: Invalidate a ready result
- **WHEN** 用户修改输入、切换目标或通过历史弹框改变基础内容
- **THEN** 旧候选不能提交
- **AND** 界面显示等待新预览或过期状态
- **AND** 拒绝过期提交时保留草稿并提供更新预览入口

### Requirement: UX regressions require rendered workflow evidence
实现 MUST 通过真实渲染和交互验证以上要求。测试 MUST 使用实际生产资源和确定性会话数据。截图 MUST 补充行为断言，不能替代断言。验收 MUST 包含中文、英文、默认、宽和最小窗口，以及布局阈值两侧。

#### Scenario: Verify layout and interactions
- **WHEN** 对本 change 执行验收
- **THEN** Headless 覆盖 760×600、1280×800、760×520、860 和 861 宽度及宽→窄→宽切换
- **AND** 断言章节表格覆盖中央分配区，选项和结果边界不相交
- **AND** 验证历史开关及分支、末行编辑、完整差异访问、应用、取消和一步撤销
- **AND** 代表性状态在放大字体和浅色、深色主题下保持可用
- **AND** 默认、宽、窄截图保存于 `artifacts/main-window-editing-ux/` 并复核

### Requirement: Candidate comparisons appear in original chapter cells
候选预览 MUST 显示在原章节表的对应单元格内。原值和预览值 MUST 明确区分。时间变化 MUST 显示带符号的变化量。宽窗口 MUST 使用可读的同格横向比较；窄窗口 MUST 使用紧凑两行比较或表格内滚动。单元格 MUST NOT 显示“原/预览”或 `Original/Preview` 文本标签。图标、语义颜色、无障碍名称及工具提示 MUST 区分已提交值和预览值。普通逐章比较 MUST NOT 要求打开独立审阅弹框。

#### Scenario: Review a time shift in place
- **WHEN** 用户准备 `t + 1` 候选
- **THEN** 对应章节的时间单元格显示原时间、预览时间和正一秒变化量
- **AND** 章节名称和其他未变化字段继续显示已提交值
- **AND** 短表头保持时间、名称、帧和编号
- **AND** 图标和语义颜色明确表示尚未应用的预览，辅助功能名称或工具提示解释值的角色
- **AND** 用户沿原章节位置即可比对结果

#### Scenario: Compare other fields
- **WHEN** 候选改变名称、编号或帧信息
- **THEN** 对应原单元格显示实际原值和目标值
- **AND** 未变化单元格不出现虚假的变化标记
- **AND** 显示的原帧值与进入预览前同一行的可见帧值一致
- **AND** 只有进入预览前没有可见帧计算值时才显示本地化缺失值
- **AND** 原帧保留进入预览前可见行的准确、近似或中性颜色
- **AND** 候选帧按候选准确度显示对应语义颜色，不使用统一的预览强调色
- **AND** 文档或轨道属性差异可从相关现有控件的本地化工具提示检查
- **AND** 缺失值与零值使用不同业务表示

### Requirement: Inline review preserves row identity and committed values
比较 MUST 通过稳定轨道及章节身份映射。原行顺序、未变化章节、选择、滚动和虚拟化 MUST 保持。准备和展示 MUST NOT 修改已提交字段或产生内容事务。

#### Scenario: Compare repeated names and shifted numbering
- **WHEN** 多个章节有相同名称且候选改变编号
- **THEN** 对比通过稳定身份显示在正确原行
- **AND** 不按名称、显示编号或行索引推断对应关系

#### Scenario: Review a large candidate
- **WHEN** 候选涉及 1,000 个章节并且用户滚动到末行
- **THEN** 所有原章节仍然可达且比较值正确
- **AND** 表格不默认只显示变化行或插入重复章节行
- **AND** 可视区域分配高度不因候选大小变化

#### Scenario: Save while preview is visible
- **WHEN** 用户尚未确认候选而保存已提交内容
- **THEN** 保存结果不包含未应用的预览值
- **AND** 展示投影不改写正常编辑字段或导出源

### Requirement: Grid preview is read-only while candidate input remains editable
有效候选展示期间章节表及直接内容变更入口 MUST 只读。滚动和选择 MUST 继续可用。表达式输入 MUST 保持可编辑。输入或基础内容变化 MUST 立即使旧候选不可应用。

#### Scenario: Edit an expression during review
- **WHEN** 用户在预览状态修改表达式
- **THEN** 旧候选立即失效且应用禁用
- **AND** 旧目标值清除或明确标为过期，不能冒充当前结果
- **AND** 自动准备完成后仅展示最新草稿对应候选
- **AND** 输入编辑的撤销不改变文档历史

#### Scenario: Attempt a chapter edit during preview
- **WHEN** 用户尝试编辑单元格、插入或删除章节
- **THEN** 直接内容编辑保持禁用，并明确提供放弃预览的动作
- **AND** 放弃后普通表格编辑恢复
- **AND** 预览准备前的单元格草稿按既有规则结束

### Requirement: Inline preview actions share the existing options row
主窗口 MUST 在原操作槽显示应用及放弃操作，并保留需要的更新和错误反馈。它 MUST NOT 新增确认行或结果面板。章节摘要和重复逐章详情入口 MUST NOT 显示。无法放入章节单元格的属性差异 MUST 通过相关现有操作的本地化工具提示检查。错误 MUST 使用现有错误反馈显示。

#### Scenario: Confirm an inline result
- **WHEN** 有效当前候选显示在原章节表中
- **THEN** 原操作槽提供应用和放弃按钮
- **AND** 原操作槽不显示章节摘要、完整范围摘要工具提示或重复逐章详情按钮
- **AND** 无需先打开模态审阅才能应用
- **AND** 入口标明实际操作而不把所有候选都误标为表达式

#### Scenario: Review property-only effects
- **WHEN** 候选只有文档或轨道属性变化
- **THEN** 相关现有操作的本地化工具提示显示属性差异
- **AND** 用户可查看原值、目标值和所属范围
- **AND** 即使章节时间不变，完整候选有变化时仍可应用
- **AND** 检查工具提示不增加底部布局行或修改候选

#### Scenario: Invalid, unchanged, or stale preview
- **WHEN** 候选无效、无变化、计算中或过期
- **THEN** 应用禁用且原操作槽显示对应状态
- **AND** 错误通过现有错误反馈查看
- **AND** 更新过期结果需要显式动作，不能替换后自动提交

### Requirement: Apply and discard restore the normal table
应用 MUST 提交原表格实际展示的有效当前候选，不得重新计算表达式。放弃 MUST 移除比较并保持原文档。成功或放弃后 MUST 恢复普通编辑且尽可能保留选择和滚动。

#### Scenario: Apply and undo
- **WHEN** 用户点击应用
- **THEN** 目标值成为普通表格的已提交值且比较标记消失
- **AND** 只创建一次原子事务，一次撤销恢复全部变化
- **AND** 重复点击不重复提交

#### Scenario: Discard without erasing input
- **WHEN** 用户点击放弃
- **THEN** 原值恢复为普通表格，文档和历史保持不变
- **AND** 表达式草稿保留
- **AND** 同一草稿版本不立即自动重建被放弃候选
- **AND** 下一次修改或显式准备可以创建新候选

### Requirement: Bottom option inputs share responsive label alignment
底部选项中的文本输入 MUST 使用响应式共享标签列，且不能按某个固定语言文本设置像素宽度。表达式编辑器 MUST 与同列输入左边界对齐，并在中、英、日文、字号变化及宽窄布局中保持一致。

#### Scenario: Align bottom text inputs
- **WHEN** 主窗口以支持语言和字体大小渲染
- **THEN** 保存格式、XML 语言及表达式输入的同列控件左边界对齐
- **AND** 断言实际 TextBox 或 ExpressionEditor 的边界，而不是只检查容器

### Requirement: Inline review requires rendered workflow evidence
验收 MUST 覆盖中文及英文、760×600、1280×800、760×520 和布局阈值两侧。测试 MUST 检查原值保持、身份映射、单元格可读性及表格可视区域。截图 MUST 补充行为断言。

#### Scenario: Validate the inline design
- **WHEN** 对本 change 执行验收
- **THEN** 验证预览前后可视区域边界相同，允许窄布局两行单元格增加行高
- **AND** 验证显示帧率 24000/1001 下原 0 和 8357 等可见帧值在预览中保持，且候选目标帧正确
- **AND** 验证无候选时没有可见原/预标签、章节摘要或重复详情按钮
- **AND** 验证箭头图标资源、颜色、accessible name 及工具提示
- **AND** 验证稳定身份、未变化行、末行、名称及帧变化、属性独立变化和亚毫秒时间差
- **AND** 验证应用、放弃、候选失效和一次撤销
- **AND** 代表性字体放大及深浅主题保持可用
- **AND** 截图保存在 `artifacts/compact-content-preview-review/`

