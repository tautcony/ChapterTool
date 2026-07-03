## MODIFIED Requirements

### Requirement: External dependency location
The system SHALL centralize discovery and configuration for mkvextract and eac3to while keeping default MP4 chapter reading independent from external tool configuration.

#### Scenario: Configured path wins
- **WHEN** a dependency path exists in migrated settings
- **THEN** tool locator SHALL use it before registry or installation discovery

#### Scenario: Missing tool is structured
- **WHEN** a tool cannot be found
- **THEN** callers SHALL receive a missing-dependency result rather than a UI prompt from Core

#### Scenario: MP4 dependency does not require tool or native library lookup
- **WHEN** MP4 chapter import is performed by the managed ATL.NET adapter
- **THEN** dependency discovery SHALL NOT require external tool location or `libmp4v2` native library resolution
