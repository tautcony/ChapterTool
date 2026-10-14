## MODIFIED Requirements

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
- **THEN** the menu item SHALL display the active shortcut from the shared shortcut catalog as an input gesture, or the control tooltip SHALL include that shortcut text

#### Scenario: Platform-specific integration is gated
- **WHEN** a workflow needs file picking, directory picking, clipboard, shell-open, settings, or file association
- **THEN** the UI SHALL use platform service abstractions and SHALL NOT require direct Windows registry access for normal cross-platform operation

#### Scenario: Registry-dependent actions are not primary controls
- **WHEN** the normal cross-platform main window is rendered
- **THEN** registry-dependent integrations such as `.mpls` file association SHALL NOT be exposed as always-visible primary controls

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
