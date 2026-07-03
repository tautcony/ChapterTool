## MODIFIED Requirements

### Requirement: Platform service abstractions
The application SHALL access dialogs, clipboard, shell, process execution, settings, localization, windows, privileges, file association, and native dependencies through injectable services.

#### Scenario: Dialog and clipboard are testable
- **WHEN** a ViewModel needs confirmation, input, error display, or clipboard access
- **THEN** it SHALL call service interfaces rather than WinForms APIs

#### Scenario: Process runner captures failures
- **WHEN** an external process is executed
- **THEN** the process result SHALL include stdout, stderr, exit code, timeout, cancellation, command, and working directory

#### Scenario: Process runner decodes redirected output explicitly
- **WHEN** an external process writes redirected stdout or stderr containing non-ASCII text
- **THEN** the process runner SHALL decode output with an explicit platform-appropriate encoding instead of relying on an interactive terminal code page

### Requirement: External dependency location
The system SHALL centralize discovery and configuration for mkvextract and eac3to while keeping default MP4 chapter reading independent from external tool configuration.

#### Scenario: Configured path wins
- **WHEN** a dependency path exists in migrated settings
- **THEN** tool locator SHALL use it before registry or installation discovery

#### Scenario: Environment path wins before platform install discovery
- **WHEN** no configured dependency path exists and the requested executable exists in the environment/PATH search directories
- **THEN** tool locator SHALL use the environment/PATH executable before probing platform installation locations

#### Scenario: Windows MKVToolNix registry discovery
- **WHEN** `mkvextract` is requested on Windows and no configured or PATH executable is found
- **THEN** tool locator SHALL probe MKVToolNix uninstall registry entries and resolve `mkvextract.exe` from the discovered install directory when present

#### Scenario: macOS MKVToolNix app bundle discovery
- **WHEN** `mkvextract` is requested on macOS and no configured or PATH executable is found
- **THEN** tool locator SHALL probe MKVToolNix app bundles such as `/Applications/MKVToolNix-96.0.app/Contents/MacOS/mkvextract`

#### Scenario: Missing tool is structured
- **WHEN** a tool cannot be found
- **THEN** callers SHALL receive a missing-dependency result rather than a UI prompt from Core

#### Scenario: Unix/Linux discovery relies on PATH search
- **WHEN** `mkvextract` is requested on Linux or another Unix-like platform and no configured path exists
- **THEN** tool locator SHALL search only configured path and environment/PATH directories without probing registry or app-bundle installation locations

#### Scenario: Configured directory path resolves executable
- **WHEN** a configured MKVToolNix path points to a directory rather than an executable file
- **THEN** tool locator SHALL resolve the platform-appropriate executable name (`mkvextract.exe` on Windows, `mkvextract` elsewhere) within that directory

#### Scenario: MP4 dependency does not require tool or native library lookup
- **WHEN** MP4 chapter import is performed by the managed ATL.NET adapter
- **THEN** dependency discovery SHALL NOT require external tool location or `libmp4v2` native library resolution
