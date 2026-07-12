## ADDED Requirements

### Requirement: Main-window orchestration uses workspace-consuming coordinators
The Avalonia main shell SHALL implement load/append/save, clip editing, projection/row refresh, and status/diagnostic presentation as dedicated orchestration components that consume `ChapterWorkspace` (or equivalent session owner) rather than permanently accumulating all orchestration as one undifferentiated multi-thousand-line ViewModel. The main ViewModel SHALL remain the bindable command and property façade for XAML.

#### Scenario: Load and save orchestration is not only partial methods on the ViewModel type
- **WHEN** a maintainer inspects the production load, append, and save flow after this change
- **THEN** the async load/append commit and save coordination logic SHALL live in a named workflow/coordinator type that uses workspace revision commit APIs
- **AND** the ViewModel SHALL invoke that workflow rather than owning the full implementation only as private partial methods with no extracted owner

#### Scenario: Projection and row refresh share a projection façade
- **WHEN** expression, naming mode, order shift, or frame display options change and rows refresh
- **THEN** projection application and row materialization SHALL route through a dedicated projection façade/coordinator shared with preview/save option building
- **AND** the façade SHALL read projection/export state from the workspace rather than a second parallel field set

#### Scenario: User-visible load/edit/save behavior is preserved
- **WHEN** the user loads a source, edits a cell, toggles combine, applies expression projection, and saves
- **THEN** those workflows SHALL remain available with the same observable outcomes as before the coordinator extraction for successful paths covered by existing unit and Headless tests

### Requirement: Settings modules are real ownership or removed
Settings durable preference groups that claim modular ownership (output defaults, external tools, about/runtime info, appearance) SHALL either own their state and related actions used by the settings tool, or those unused module types SHALL be removed. The repository SHALL NOT keep permanently unreferenced “ownership module” types as documentation substitutes.

#### Scenario: No dead settings module types remain
- **WHEN** the settings tool implementation is complete for this change
- **THEN** every `Settings*Module` type shipped under the Avalonia settings ViewModels folder SHALL be referenced by the settings tool composition or production code path
- **OR** unused module types SHALL be deleted from the tree

#### Scenario: Appearance ownership stays dedicated
- **WHEN** theme or font settings change in Settings
- **THEN** appearance selection and font catalogs SHALL continue to be owned by a dedicated appearance ViewModel/module used by the settings tool

#### Scenario: External tools actions remain testable without about-panel logic
- **WHEN** external tool browse, clear, validate, or discover actions run
- **THEN** those actions SHALL remain implementable without requiring about-panel-only state to execute

### Requirement: Expression editor uses composition-owned authoring services
Production construction of the Lua expression editor SHALL obtain `IExpressionAuthoringService` (or an equivalent authoring analysis service) from application composition or an injected dependency, rather than permanently constructing a private default authoring service that can diverge from the composition-root expression engine policy.

#### Scenario: Production editor shares engine policy with the shell
- **WHEN** the main window or expression tool hosts an expression editor in the running application
- **THEN** authoring analysis SHALL use the composition-owned expression engine/authoring service path
- **AND** the control SHALL NOT be the only place that constructs a separate default Lua engine for production analysis

#### Scenario: Both XAML editor hosts receive the composed authoring service
- **WHEN** the main-window or expression-tool XAML creates an `ExpressionEditor`
- **THEN** its authoring-service property SHALL be bound to a service supplied by the composition root
- **AND** the expression-tool path SHALL carry that service through its tool-window creation context

#### Scenario: Editor presentation remains separable from analysis
- **WHEN** token coloring, completion chrome, or diagnostic underlines render
- **THEN** analysis results SHALL still come from the Core authoring service contract
- **AND** control-specific rendering helpers SHALL remain separable from analysis

### Requirement: Single command surface for main workflow actions
The Avalonia main window SHALL expose one primary command surface for workflow actions through the main ViewModel. Window-level command wrappers MAY exist only when they add view-only parameters such as picker results or selected row indexes, and SHALL NOT duplicate independent can-execute business rules or re-implement load/save/combine/delete business semantics.

#### Scenario: Business can-execute lives on the ViewModel
- **WHEN** save, combine, append MPLS, delete, insert, or related-media availability changes
- **THEN** the ViewModel commands SHALL raise the authoritative can-execute changes
- **AND** any window wrappers SHALL derive enabled state from those commands or equivalent ViewModel capability flags

#### Scenario: Keyboard routing does not fork business semantics
- **WHEN** a documented shortcut is pressed
- **THEN** the gesture SHALL route to the same ViewModel command path used by the corresponding visible control or menu action
- **AND** load/save shortcuts SHALL NOT maintain a separate incompatible command implementation

#### Scenario: Window wrappers stay view adapters only
- **WHEN** `MainWindow.axaml.cs` defines a command for browse-load, save-to, delete selected, or insert selected
- **THEN** that command SHALL only gather view parameters (picker paths, selected indexes) and forward to the ViewModel command or method surface
- **AND** it SHALL NOT maintain a second independent business status/progress pipeline for those actions
