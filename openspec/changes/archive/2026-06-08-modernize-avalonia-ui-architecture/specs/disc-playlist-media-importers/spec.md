## ADDED Requirements

### Requirement: Runtime importer registry
Runtime chapter loading SHALL route supported sources through injected importer registrations or factories.

#### Scenario: Supported source dispatches through registry
- **WHEN** the load service receives `.mpls`, `.ifo`, `.xpl`, BDMV directory, `.mp4`, `.m4a`, `.m4v`, or other supported chapter sources
- **THEN** it SHALL select the matching importer through an injected registry or factory model and return the importer result

#### Scenario: Importer infrastructure is injected
- **WHEN** dependency-backed importers require external tool location, process execution, native dependency resolution, filesystem access, or parser adapters
- **THEN** those dependencies SHALL come from registered services rather than being constructed inside each load operation

#### Scenario: Importer dispatch is test-substitutable
- **WHEN** tests replace importer registrations, external tool locators, process runners, or native dependency services
- **THEN** runtime loading SHALL use the replacements without requiring changes to UI code or concrete runtime service constructors

#### Scenario: Dependencies are not recreated per load by default
- **WHEN** multiple load operations run in one application session
- **THEN** singleton or scoped infrastructure services SHALL follow their registered lifetimes instead of being recreated manually inside `LoadAsync`
