## MODIFIED Requirements

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
