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

### Requirement: Keyboard and menu routing
The Avalonia shell SHALL preserve documented shortcuts and context menu actions.

#### Scenario: Global shortcuts route to commands
- **WHEN** the main window has focus
- **THEN** `Ctrl+O`, `Ctrl+S`, `Alt+S`, `Ctrl+R`, `F5`, `Ctrl+L`, and `F11` SHALL invoke the corresponding ViewModel commands

#### Scenario: Clip shortcuts preserve legacy mapping
- **WHEN** `Ctrl+1` through `Ctrl+9` or `Ctrl+0` are pressed
- **THEN** they SHALL select clips 1 through 9 and clip 10 respectively when in range

#### Scenario: Context menus use capability flags
- **WHEN** load, clip, or chapter-row context menus open
- **THEN** entries such as append MPLS, merge chapters, related media, zones, forward translation, and insert SHALL be enabled only when the ViewModel capability flags allow them

### Requirement: Chapter grid interaction
The chapter table SHALL use observable row models and commands instead of UI row tags.

#### Scenario: Edit chapter cell
- **WHEN** a time, name, or frame cell is edited
- **THEN** the ViewModel SHALL delegate validation and conversion to Core services and refresh row display from returned model state

#### Scenario: Localized grid headers still route edits
- **WHEN** the chapter grid uses Chinese headers for time, name, and frame columns
- **THEN** committed edits SHALL still route to the correct time, name, and frame commands

#### Scenario: Delete selected rows
- **WHEN** selected rows are deleted
- **THEN** the ViewModel SHALL update the underlying chapter list through Core operations and refresh numbering, time, and frame values

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
The Avalonia main window SHALL bind visible state to ViewModel properties instead of synchronizing normal UI state through imperative control assignments.

#### Scenario: Bound controls update from ViewModel
- **WHEN** ViewModel state changes after loading, editing, clip switching, option changes, or save operations
- **THEN** bound text, progress, item sources, selections, checkboxes, visibility, enabled state, and option fields SHALL update through XAML bindings

#### Scenario: Code-behind remains view-specific
- **WHEN** `MainWindow.axaml.cs` is inspected
- **THEN** it SHALL only contain view-specific event adaptation, platform UI interactions, and constructor wiring, and SHALL NOT be responsible for routine status, progress, grid, clip, option, or command state synchronization

### Requirement: Typed compiled bindings
The Avalonia shell SHALL use typed data contexts and compiled bindings for the main window and migrated typed views.

#### Scenario: Main window bindings are checked at build time
- **WHEN** the Avalonia app project is built
- **THEN** main-window bindings to ViewModel properties and commands SHALL be validated by typed or compiled Avalonia binding support

#### Scenario: Binding regressions fail tests or build
- **WHEN** a bound ViewModel property or command is renamed or removed
- **THEN** the build or focused UI/static tests SHALL fail before runtime manual testing is required

### Requirement: Hidden command shims are removed
The UI shell SHALL NOT use hidden buttons or invisible controls solely as command hosts for main-window actions.

#### Scenario: Main actions are reachable through real command surfaces
- **WHEN** save, append MPLS, combine, open media, color, expression, template, zones, forward shift, and similar actions are available
- **THEN** they SHALL be exposed through visible buttons, menu items, context menu items, key bindings, or directly testable ViewModel commands

#### Scenario: Hidden shim controls are absent
- **WHEN** the main-window XAML is inspected
- **THEN** controls whose only purpose is to hide a command binding from the visible UI SHALL NOT be present

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
- **THEN** it SHALL expose UI language, default save directory, and main window location reset controls

#### Scenario: External tool settings are editable
- **WHEN** the settings panel is opened
- **THEN** it SHALL expose MKVToolNix/mkvextract path, eac3to path, ffprobe path, and ffmpeg directory fallback controls with browse, clear, and validation status behavior

#### Scenario: Output defaults are editable
- **WHEN** the settings panel is opened
- **THEN** it SHALL expose default save format and default XML chapter language rather than the current working values being edited on the main screen

#### Scenario: Appearance settings are editable
- **WHEN** the settings panel is opened
- **THEN** it SHALL expose the six theme color slots in their legacy order

#### Scenario: High-frequency main workflow controls stay on the main screen
- **WHEN** the settings panel is opened
- **THEN** high-frequency current working controls such as naming mode, template use, order shift, expression, frame-rate choice, and round-frames SHALL remain on the main workflow surface instead of being duplicated as settings

#### Scenario: Platform integration is gated
- **WHEN** file association or another platform-specific integration is shown in settings
- **THEN** it SHALL be hidden, disabled, or clearly marked unsupported when the current platform cannot perform it

### Requirement: Settings changes apply predictably
The settings panel SHALL save, apply, reset, and validate changes in a way that keeps the main ViewModel and persisted settings consistent.

#### Scenario: Save applies runtime-safe settings
- **WHEN** the user saves settings that can be applied at runtime
- **THEN** the running shell SHALL update language, save defaults, and appearance state without requiring a restart

#### Scenario: Reset restores defaults
- **WHEN** the user resets a settings group to defaults
- **THEN** the panel SHALL restore the same defaults used by a fresh application start

#### Scenario: Invalid settings are surfaced
- **WHEN** a setting value is invalid or an external tool path cannot be resolved
- **THEN** the settings panel SHALL show a localized validation message and SHALL NOT silently discard the user's input

### Requirement: Restored conversion tools are reachable
The Avalonia shell SHALL expose restored legacy conversion tools through compact command surfaces without coupling conversion logic to the view.

#### Scenario: Celltimes conversion is discoverable
- **WHEN** a chapter set is loaded and a valid frame rate is selected
- **THEN** the UI SHALL provide a compact command or tool entry for exporting or generating celltimes output

#### Scenario: Chapter2Qpfile conversion is discoverable
- **WHEN** the user opens auxiliary chapter conversion tools
- **THEN** Chapter2Qpfile conversion SHALL be available without requiring manual command-line use

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
