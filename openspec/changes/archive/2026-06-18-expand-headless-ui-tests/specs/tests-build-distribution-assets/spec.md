## MODIFIED Requirements

### Requirement: Avalonia Headless UI test coverage
The Avalonia test project SHALL provide comprehensive headless Avalonia runtime coverage for rendered UI behavior that runs in CI without launching the desktop application.

#### Scenario: Headless runtime initializes without desktop lifetime
- **WHEN** Avalonia UI tests start
- **THEN** they SHALL initialize an Avalonia Headless AppBuilder compatible with the app's Avalonia major version
- **AND** they SHALL NOT call `StartWithClassicDesktopLifetime` or require a platform desktop session

#### Scenario: Main window renders under headless backend
- **WHEN** a headless test constructs the main window with test services
- **THEN** the window SHALL load compiled XAML, apply localization resources, bind to the supplied ViewModel, and complete layout without throwing

#### Scenario: Main window initial state is rendered from bindings
- **WHEN** a headless main-window test renders the application before any source is loaded
- **THEN** the visible load/save/frame controls, empty chapter grid state, hidden clip selector, collapsed advanced panel, disabled save action, default TXT save type, and default progress/status state SHALL match the ViewModel state through bindings

#### Scenario: Main window load action updates rendered state
- **WHEN** a headless main-window test loads a deterministic chapter source through the visible load command surface
- **THEN** the rendered display path, status/progress strip, chapter grid rows, save command availability, frame-rate selector, and option controls SHALL update from the load result without imperative test-only control assignment

#### Scenario: XML edition switching displays chapter names
- **WHEN** a headless main-window test loads an XML source with multiple edition options and selects a non-default edition through the visible clip/edition selector
- **THEN** the rendered chapter grid SHALL display the selected edition's chapter names in the name column
- **AND** the names SHALL NOT be blank, stale from the previous edition, or hidden by layout/binding failure

#### Scenario: IFO edition switching displays chapter names
- **WHEN** a headless main-window test loads an IFO source with multiple program-chain or title options and selects a non-default option through the visible clip/edition selector
- **THEN** the rendered chapter grid SHALL display the selected option's chapter names in the name column
- **AND** the names SHALL NOT be blank, stale from the previous option, or hidden by layout/binding failure

#### Scenario: MPLS clip switching displays chapter names
- **WHEN** a headless main-window test loads an MPLS source with multiple playlist or clip options and selects another option through the visible clip selector
- **THEN** the rendered chapter grid SHALL display the selected option's chapter names in the name column
- **AND** the names SHALL NOT be blank, stale from the previous option, or hidden by layout/binding failure

#### Scenario: Save and output options are routed through the UI
- **WHEN** a headless main-window test changes save format, XML language, naming, template, order shift, expression, frame-rate, and save-directory options through rendered controls before invoking save
- **THEN** the fake save service SHALL receive options and chapter rows that reflect the rendered UI state
- **AND** XML-language controls SHALL enable only for XML export through bindings

#### Scenario: Chapter grid command surfaces are rendered and bound
- **WHEN** a headless main-window test renders chapter grid rows and opens the grid command surface
- **THEN** insert, delete, combine, related media, zones, forward translation, and preview entries SHALL be exposed through real visible or context-menu UI elements
- **AND** detailed time, name, and frame edit behavior SHALL remain covered by ViewModel-level tests rather than duplicated through headless UI tests

#### Scenario: Chapter grid selection updates command surfaces
- **WHEN** a headless main-window test selects chapter rows and opens visible command surfaces or context menus
- **THEN** the selected rows and command enabled states SHALL match the ViewModel capability state without duplicating row editing business assertions

#### Scenario: Keyboard shortcuts route through the rendered window
- **WHEN** a headless main-window test focuses the rendered window and sends documented shortcuts
- **THEN** `Ctrl+O`, `Ctrl+S`, `Alt+S`, `Ctrl+R`, `F5`, `Ctrl+L`, `F11`, and in-range `Ctrl+0` through `Ctrl+9` SHALL invoke the same commands as the visible controls

#### Scenario: Context menus respect capability flags
- **WHEN** a headless main-window test opens load, clip, or chapter-row context menus under different ViewModel capability states
- **THEN** append MPLS, merge chapters, related media, zones, forward translation, insert, and similar entries SHALL be visible or enabled only when the corresponding capability flag allows them

#### Scenario: Auxiliary tool commands open the expected views
- **WHEN** a headless main-window test invokes preview, log, settings, color, language, expression, template names, zones, forward shift, and related auxiliary commands
- **THEN** the window service or rendered tool view SHALL receive the expected window identifier and parameter
- **AND** no command SHALL depend on hidden shim controls as its only reachable UI surface

#### Scenario: Secondary tool views render representative states
- **WHEN** headless tests render settings, color settings, language, expression, template names, text/log/preview, and forward-shift tool views with deterministic ViewModels
- **THEN** each view SHALL load compiled XAML, complete layout, render localized title/labels/action captions, expose expected editable controls, and bind changes back to the ViewModel

#### Scenario: Runtime language switching refreshes rendered UI
- **WHEN** a headless UI test renders the main window or a secondary tool and switches the active language to Simplified Chinese, English, or Japanese
- **THEN** representative visible labels, button captions, menu text, grid headers, status text, and tool-window text SHALL refresh to the active resource set
- **AND** unsupported or blank language settings SHALL fall back predictably without rendering localization keys or mojibake strings

#### Scenario: Responsive layouts remain usable at required sizes
- **WHEN** headless UI tests render the main window and secondary tools at default, wide, and narrow supported sizes
- **THEN** workflow zones SHALL remain visible and usable, numeric controls SHALL keep values readable, DataGrid headers/content SHALL avoid overlap through sensible minimum widths, button content SHALL remain centered, and bottom options SHALL resize without clipped controls
- **AND** screenshot artifacts for these size checks SHALL be written under `artifacts/`

#### Scenario: Headless UI tests stay deterministic
- **WHEN** headless UI tests exercise main-window workflows, secondary tools, localization, or layout
- **THEN** they SHALL use in-process deterministic fixtures or fake services
- **AND** they SHALL NOT require external media tools, installed desktop applications, registry state, native file dialogs, real shell launches, wall-clock timing, network access, or machine-specific file paths

#### Scenario: UI tests avoid static source assertions
- **WHEN** headless UI coverage verifies labels, layout, command surfaces, bindings, localization, or context menus
- **THEN** it SHALL use compiled Avalonia views, rendered controls, public ViewModel/service effects, screenshots, or structured runtime APIs
- **AND** it SHALL NOT test `.cs`, `.axaml`, `.csproj`, scripts, CI YAML, README, or documentation files by reading them as plain text and asserting strings
