# avalonia-ui-shell Delta

## ADDED Requirements

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
- **THEN** the menu item SHALL display the shortcut as an input gesture, or the control tooltip SHALL include the shortcut text

#### Scenario: Platform-specific integration is gated
- **WHEN** a workflow needs file picking, directory picking, clipboard, shell-open, settings, or file association
- **THEN** the UI SHALL use platform service abstractions and SHALL NOT require direct Windows registry access for normal cross-platform operation

#### Scenario: Registry-dependent actions are not primary controls
- **WHEN** the normal cross-platform main window is rendered
- **THEN** registry-dependent integrations such as `.mpls` file association SHALL NOT be exposed as always-visible primary controls

### Requirement: Main window load progress
The main window SHALL present bounded progress during source loading when the load pipeline reports intermediate progress, and SHALL NOT present an empty progress indicator at idle.

#### Scenario: Importer reports intermediate progress
- **WHEN** a load operation reports progress before returning its import result
- **THEN** the main-window view model SHALL update the progress value to a bounded intermediate value
- **AND** completion or failure handling SHALL remain responsible for the final progress state

#### Scenario: Idle state hides the progress indicator
- **WHEN** no load or save operation is running
- **THEN** the status-strip progress indicator SHALL NOT be visible

### Requirement: Hidden command shims are removed
The UI shell SHALL NOT use hidden buttons or invisible controls as command hosts or state hosts for main-window actions.

#### Scenario: Main actions are reachable through real command surfaces
- **WHEN** save, append MPLS, combine, open media, color, expression, template, zones, forward shift, and similar actions are available
- **THEN** they SHALL be exposed through visible buttons, menu items, context menu items, key bindings, or directly testable ViewModel commands

#### Scenario: Hidden shim controls are absent
- **WHEN** the main-window XAML is inspected
- **THEN** controls whose only purpose is to hide a command binding or a state binding from the visible UI SHALL NOT be present
- **AND** tests SHALL drive source-path state through the ViewModel instead of a hidden text box
