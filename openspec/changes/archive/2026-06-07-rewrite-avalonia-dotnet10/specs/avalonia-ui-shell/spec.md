## ADDED Requirements

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
