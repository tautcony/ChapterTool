## ADDED Requirements

### Requirement: Preserve format catalog behavior
The system MUST return the same codes and descriptions for every supported import and export format, and MUST retain the existing fallback behavior for unknown enum values.

#### Scenario: Supported format lookup
- **WHEN** a supported import or export enum is provided
- **THEN** the stable code and display metadata match the current catalog

### Requirement: Preserve workflow outcomes
CLI import/convert/inspect and Avalonia editing, keyboard, completion, media-opening, and logging workflows MUST produce the same observable success, error, cancellation, and diagnostic outcomes for existing inputs.

#### Scenario: Existing workflow input
- **WHEN** an existing valid CLI or Avalonia workflow is executed
- **THEN** its success, error, cancellation, and diagnostic outcome is unchanged

### Requirement: Preserve parser and platform boundaries
BDMV/HDMV resolution and Windows registry probing MUST preserve ordering, filtering, and unsupported-platform behavior.

#### Scenario: Unsupported platform
- **WHEN** registry probing runs on a non-Windows platform
- **THEN** it returns no values without throwing

### Requirement: Make decisions independently testable
Extracted decision logic MUST be callable through focused internal/private helpers and covered by behavior tests for normal, boundary, and unknown inputs.

#### Scenario: Boundary decision
- **WHEN** a helper receives a boundary or unknown value
- **THEN** it returns the documented fallback and can be verified without UI or process state

### Requirement: Reduce method complexity
Each listed method MUST be reduced to a small orchestration body by moving independent branches into named helpers or data tables, without broad unrelated refactoring.

#### Scenario: Refactored entry point
- **WHEN** a listed method is invoked
- **THEN** it delegates independent decisions to named helpers or tables and preserves observable behavior
