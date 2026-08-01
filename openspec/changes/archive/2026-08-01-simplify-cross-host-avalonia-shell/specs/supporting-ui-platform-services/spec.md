## ADDED Requirements

### Requirement: Tool descriptors come from an injected catalog
Secondary tool windows SHALL be resolved from one injected tool catalog owned by host composition. The window service SHALL not combine a static default registry with an injected registration list.

#### Scenario: Desktop composition creates the standard catalog
- **WHEN** the desktop composition root creates the auxiliary-tool host
- **THEN** it SHALL pass one catalog containing the standard tool descriptors
- **AND** title, size, and content factory lookup SHALL use that same catalog

#### Scenario: Test host supplies a custom catalog
- **WHEN** a test supplies a catalog containing a custom descriptor for an existing tool identifier
- **THEN** the surface host SHALL use the supplied descriptor
- **AND** it SHALL not silently resolve the static standard descriptor instead

### Requirement: Tool creation context has typed required dependencies
Tool creation SHALL use a typed context or factory with required dependency groups. Normal host differences SHALL not be represented by unrelated nullable properties in one shared context.

#### Scenario: Settings tool is created
- **WHEN** the settings descriptor creates its ViewModel
- **THEN** it SHALL receive the settings, appearance, picker, shell, and localization dependencies from the host composition group
- **AND** missing required production dependencies SHALL fail during composition validation rather than during user interaction

#### Scenario: Tool dependencies stay scoped
- **WHEN** the log descriptor creates the log tool
- **THEN** it SHALL receive only the log, localization, and clipboard dependencies it needs
- **AND** it SHALL not receive unrelated settings or external-tool dependencies

### Requirement: Existing secondary tools are reusable
The auxiliary-tool host SHALL reuse an existing tool content instance for repeated open requests unless the descriptor declares that its request data requires refresh.

#### Scenario: Reopening settings preserves state
- **WHEN** the user opens Settings, changes an unsaved value, and opens Settings again
- **THEN** the existing Settings ViewModel SHALL remain active
- **AND** its unsaved value SHALL remain visible

#### Scenario: Refreshable tool reflects the current selection
- **WHEN** the user changes the selected rows and opens Zones or Forward Shift again
- **THEN** the host SHALL refresh the tool content from the current selection because the descriptor declares it refreshable
- **AND** reusable tools SHALL keep their existing ViewModel and state

#### Scenario: Closing a tool disposes it once
- **WHEN** a tool window closes
- **THEN** its disposable DataContext SHALL be disposed once
- **AND** the tool entry SHALL be removed from the host registry

### Requirement: Native and Embedded hosts share lifecycle rules
Native-window and Embedded auxiliary-tool host implementations SHALL apply the same open, activate, close, disposal, localization, and unknown-tool rules.

#### Scenario: Closing unsaved settings confirms once
- **WHEN** the user closes a Settings tool with unsaved changes
- **THEN** the host SHALL run Save, Discard, or Cancel confirmation through its close-confirmation port
- **AND** the tool SHALL be disposed exactly once after the confirmed close

#### Scenario: Host disposal clears embedded content
- **WHEN** an Embedded auxiliary-tool host is disposed
- **THEN** the presenter content SHALL be cleared
- **AND** each disposable tool DataContext SHALL be disposed exactly once

#### Scenario: Culture changes while a tool is open
- **WHEN** the active culture changes while a tool is open
- **THEN** the host SHALL update the existing tool title and localization subscriptions
- **AND** it SHALL not recreate the tool solely to refresh culture
