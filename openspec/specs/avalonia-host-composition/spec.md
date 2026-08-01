# avalonia-host-composition Specification

## Purpose
Define the typed composition boundary that connects Avalonia hosts to the shared ChapterTool shell.

## Requirements

### Requirement: Avalonia shell has an explicit host composition boundary
The shared Avalonia shell SHALL be constructed from an explicit typed host composition boundary that supplies workspace services, host effects, settings and appearance services, localization, runtime capabilities, and an auxiliary-tool host.

#### Scenario: Desktop host composes the shell
- **WHEN** the desktop application creates the main window
- **THEN** `AppCompositionRoot` SHALL provide all required shell services through the host composition boundary
- **AND** the shared shell SHALL not construct desktop adapters by probing global state or optional service fields

#### Scenario: Host without a capability composes the shell
- **WHEN** an Avalonia host cannot provide a capability such as local paths, external processes, or clipboard access
- **THEN** the host SHALL provide an explicit unavailable adapter or capability value
- **AND** the shell SHALL expose the documented disabled or hidden behavior without a null-reference path

#### Scenario: Window-scoped factories stay host-owned
- **WHEN** a host composes the shell boundary
- **THEN** the shared boundary record SHALL contain no Avalonia window types
- **AND** window-scoped file picker and clipboard factories SHALL be injected at tool-open time by the auxiliary-tool host

### Requirement: Host differences use shared auxiliary-tool host contracts
The shared Avalonia shell SHALL expose auxiliary tools through one typed auxiliary-tool host contract that can be implemented by Native Window, Embedded, or another Avalonia host presentation.

#### Scenario: Native-window host opens a tool
- **WHEN** the main shell requests a tool from a Native Window host
- **THEN** the host SHALL create or activate the registered auxiliary tool presentation
- **AND** the main shell SHALL not depend on Avalonia desktop window construction

#### Scenario: Embedded host opens a tool
- **WHEN** the main shell requests a tool from an Embedded host
- **THEN** the host SHALL present the same registered tool content through its embedded-tool presenter
- **AND** the main shell SHALL use the same tool identifier and workspace ports as the Native Window host

#### Scenario: Close confirmation is host-owned
- **WHEN** the user closes a tool that declares close confirmation with unsaved changes
- **THEN** the host SHALL run Save, Discard, or Cancel confirmation through its own presentation surface
- **AND** Native Window and Embedded hosts SHALL apply the same confirmation rules

### Requirement: Host adapters own host-specific implementations
Host-specific file, output, clipboard, settings, process, and surface implementations SHALL be owned by the host composition that provides the corresponding platform API.

#### Scenario: Shared shell is referenced by a second Avalonia host
- **WHEN** a second Avalonia host references `ChapterTool.Avalonia.UI`
- **THEN** it SHALL be able to replace desktop file, output, clipboard, and auxiliary-tool host adapters through composition
- **AND** it SHALL not need to modify shared tool ViewModels or workspace workflow rules

#### Scenario: Browser-only implementation is not required by the desktop shell
- **WHEN** a browser-specific adapter is not part of an Avalonia host composition
- **THEN** the desktop shell SHALL not load or instantiate that adapter
- **AND** the browser adapter SHALL remain owned by the browser host or a host-neutral contract assembly
