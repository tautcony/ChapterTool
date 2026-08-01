# BDMV Navigation Parsing Specification

## Purpose
The system parses bounded HDMV MovieObject commands and BD-J playlist declarations.

## Requirements

### Requirement: MovieObject binary parsing

Core SHALL parse `MovieObject.bdmv` into typed objects and 12-byte navigation commands. The parser SHALL validate section addresses, lengths, counts, reserved bounds, and truncation.

#### Scenario: Valid MovieObject command is decoded

- **WHEN** a command contains instruction fields and two operands
- **THEN** the parser SHALL preserve the group, subgroup, operand count, immediate flags, operation option, destination operand, and source operand

#### Scenario: MovieObject input exceeds a limit

- **WHEN** the file, section, object count, command count, or total command count exceeds its configured limit
- **THEN** parsing SHALL fail with a structured limit diagnostic

### Requirement: Bounded HDMV navigation resolution

Core SHALL resolve playlist playback events from HDMV navigation commands. It SHALL implement the branch, compare, set, register, and playback behavior that affects playlist selection.

#### Scenario: Playlist identifier is stored in a GPR

- **WHEN** Set and Compare instructions prepare a GPR before a `PlayPL` instruction uses that register
- **THEN** the resolver SHALL emit the resolved playlist identifier

#### Scenario: Compare condition is false

- **WHEN** a Compare instruction evaluates to false
- **THEN** the resolver SHALL skip the next instruction according to HDMV behavior

#### Scenario: Navigation enters a cycle

- **WHEN** a program revisits states or exceeds an execution limit
- **THEN** the resolver SHALL stop that path
- **AND** it SHALL return a structured limit diagnostic
- **AND** it SHALL NOT hang

#### Scenario: Player settings affect a branch

- **WHEN** a navigation program reads a relevant PSR
- **THEN** the resolver SHALL use a documented deterministic default profile
- **AND** it MAY evaluate a bounded set of relevant profile variants
- **AND** diagnostics SHALL identify every evaluated profile

### Requirement: BDJO accessible-playlist parsing

Core SHALL parse the BDJO accessible-playlist declaration. It SHALL preserve the playlist count, access-to-all flag, autostart-first flag, and five-character playlist names.

#### Scenario: BDJO declares explicit playlists

- **WHEN** a BDJO file contains an explicit playlist list
- **THEN** the parser SHALL return each playlist in declaration order
- **AND** it SHALL mark the first playlist as autostart evidence when the flag is set

#### Scenario: BDJO grants access to all playlists

- **WHEN** `access_to_all_flag` is set
- **THEN** the discovery policy SHALL permit bounded playlist-scan candidates for that title

#### Scenario: BDJO relies on dynamic application code

- **WHEN** the declaration does not identify the playlist selected by the BD-J application
- **THEN** the resolver SHALL report unsupported dynamic BD-J navigation
- **AND** it SHALL NOT load or execute a JAR file
