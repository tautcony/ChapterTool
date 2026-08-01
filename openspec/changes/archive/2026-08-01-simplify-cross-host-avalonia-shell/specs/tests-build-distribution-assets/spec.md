## ADDED Requirements

### Requirement: Cross-host composition contracts have behavior coverage
Tests SHALL verify the shared Avalonia host contracts with replaceable Native Window, Embedded, and unavailable-capability adapters.

#### Scenario: Host modes use the same tool command contract
- **WHEN** contract tests execute a tool command against Native Window and Embedded test hosts
- **THEN** both hosts SHALL receive the same stable tool identifier
- **AND** both SHALL expose the same workspace tool-port behavior

#### Scenario: Unavailable capability is reflected in the shell
- **WHEN** a test host disables local paths or external processes
- **THEN** the shell SHALL disable or hide the affected commands and settings
- **AND** it SHALL not throw because an optional service is null

#### Scenario: Tool identifiers resolve case-insensitively
- **WHEN** contract tests open a tool with a differently-cased identifier
- **THEN** both hosts SHALL resolve the same descriptor as the standard identifier

### Requirement: Tool lifecycle behavior has Headless coverage
Avalonia Headless tests SHALL verify tool reuse, state preservation, content disposal, localization refresh, and Embedded content presentation as user-facing behavior.

#### Scenario: Repeated open preserves tool state
- **WHEN** a Headless test opens a stateful tool, changes a value, and invokes the same open command again
- **THEN** the test SHALL verify that the same tool ViewModel remains active and the value remains unchanged

#### Scenario: Closing removes and disposes tool content
- **WHEN** a Headless test closes a tool
- **THEN** the test SHALL verify that content is detached and disposable subscriptions no longer receive localization updates

#### Scenario: Refreshable tool receives new selection
- **WHEN** a Headless test changes the row selection and reopens Zones
- **THEN** the test SHALL verify that the visible content reflects the current selection

### Requirement: Host boundary tests do not inspect source text
Host composition and tool lifecycle tests SHALL verify compiled behavior through public or internal contracts, runtime composition, rendered UI behavior, or structured results. They SHALL not read C# or XAML files as text to prove implementation details.

#### Scenario: Custom catalog is selected at runtime
- **WHEN** a test supplies a custom descriptor through composition
- **THEN** it SHALL verify the created control or ViewModel behavior at runtime
- **AND** it SHALL not inspect registry source text
