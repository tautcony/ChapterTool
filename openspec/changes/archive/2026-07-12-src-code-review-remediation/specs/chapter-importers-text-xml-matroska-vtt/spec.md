## ADDED Requirements

### Requirement: Matroska XML import loads without external entity resolution
Matroska chapter XML import SHALL parse documents using XML reader settings that prohibit DTD processing and do not resolve external XML entities or remote resources. Import of untrusted XML MUST NOT perform network fetches or local file inclusion via DTD/entity mechanisms.

#### Scenario: Document type declaration does not enable external resolution
- **WHEN** an XML chapter document contains a DOCTYPE or external entity declaration intended to resolve remote or local resources
- **THEN** the importer SHALL fail closed with a structured diagnostic or refuse entity expansion
- **AND** the import path SHALL NOT successfully load content that depends on resolving those external entities

#### Scenario: Valid Matroska chapter XML without DTD still imports
- **WHEN** a normal Matroska chapters XML document without external entity dependencies is imported
- **THEN** editions and chapter atoms SHALL continue to import according to existing XML chapter import requirements

#### Scenario: Secure load applies to stream, path, and text entry points
- **WHEN** Matroska XML is imported from a file path, from `ChapterImportRequest.Content`, or from text content APIs on the importer
- **THEN** each entry point SHALL use the same secure loading policy
