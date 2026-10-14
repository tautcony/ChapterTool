## ADDED Requirements

### Requirement: Browser settings use the current storage key only
The browser app SHALL load persisted settings from `chaptertool.wasm.settings` only. It SHALL NOT read or delete predecessor storage keys.

#### Scenario: Current settings are stored
- **WHEN** the current storage key contains valid settings
- **THEN** the app SHALL load those settings
- **AND** it SHALL leave unrelated local storage entries unchanged

#### Scenario: Only predecessor settings are stored
- **WHEN** an obsolete storage key contains settings and the current key is absent
- **THEN** the app SHALL use current defaults
- **AND** it SHALL NOT copy or delete the predecessor entry
