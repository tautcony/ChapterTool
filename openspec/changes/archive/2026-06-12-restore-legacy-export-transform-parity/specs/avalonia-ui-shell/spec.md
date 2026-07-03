## ADDED Requirements

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
