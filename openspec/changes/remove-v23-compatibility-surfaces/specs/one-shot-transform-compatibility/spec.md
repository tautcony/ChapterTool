## MODIFIED Requirements

### Requirement: Core and Node exports serialize prepared content
Core and Node export APIs SHALL serialize the supplied chapter snapshot and SHALL NOT accept transform-bearing compatibility export options. Callers that need transformed content SHALL apply an explicit content operation, then call serialization.

#### Scenario: Caller exports prepared content
- **WHEN** a caller serializes a chapter snapshot
- **THEN** serialization SHALL preserve the supplied chapter values
- **AND** it SHALL NOT apply expression, naming, numbering, or other content transforms

#### Scenario: Caller transforms content before export
- **WHEN** a caller needs an expression, naming, numbering, or frame-rate operation
- **THEN** the caller SHALL apply the explicit operation to content first
- **AND** it SHALL serialize the resulting snapshot without replaying that operation

#### Scenario: CLI converts with an expression
- **WHEN** `convert` is called with an expression or preset
- **THEN** the CLI SHALL apply the selected operation once before serialization
- **AND** CLI expression options SHALL remain structured DotMake command options

## REMOVED Requirements

### Requirement: Compatibility lifetime and deprecation are explicit
**Reason**: The user selected removal of the transform-bearing Core and Node export compatibility surfaces in the current 23.x release line.
**Migration**: Replace Core `ChapterExportOptions`/`ExportCompatibility` calls with explicit content operations and `ChapterSerializationOptions`. Replace Node `export` with exact content operations followed by `serialize`.
