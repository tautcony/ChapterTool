## ADDED Requirements

### Requirement: Desktop application uses the SourceGit user interface foundation
The Avalonia application SHALL load the complete reusable SourceGit icon, theme, and control-style resources.

#### Scenario: Application starts with imported resources
- **WHEN** the Avalonia application starts
- **THEN** every imported SourceGit icon and theme token SHALL resolve
- **AND** every reusable imported control style SHALL compile under Avalonia 12.1

#### Scenario: Product control uses the global style layer
- **WHEN** a ChapterTool window displays a standard control
- **THEN** the control SHALL receive the ported SourceGit base style
- **AND** a later ChapterTool-specific style MAY refine product constraints

#### Scenario: Migration excludes Git-domain user interfaces
- **WHEN** the imported resource layer is compared with its SourceGit source
- **THEN** the migration SHALL exclude only selectors that require SourceGit Git-domain types
- **AND** the migration notice SHALL identify each exclusion

### Requirement: Imported SourceGit work retains attribution
ChapterTool SHALL keep an attribution record for the imported SourceGit resources.

#### Scenario: Reviewer inspects third-party evidence
- **WHEN** a reviewer opens the SourceGit resource directory
- **THEN** the directory SHALL identify the upstream repository and source revision
- **AND** it SHALL include the SourceGit MIT license text

### Requirement: Global styles preserve workflow usability
The ported style layer SHALL keep ChapterTool workflows usable at supported window sizes.

#### Scenario: Main window uses the global styles
- **WHEN** the main window opens
- **THEN** the load and save area, chapter grid, options area, and status strip SHALL remain available
- **AND** controls SHALL not overlap

#### Scenario: Tool window uses a narrow supported size
- **WHEN** a tool window is resized to its minimum width
- **THEN** primary actions SHALL remain visible
- **AND** text SHALL remain inside its control bounds

### Requirement: Settings window uses compact SourceGit form composition
The settings window SHALL use the ported SourceGit input and action styles.

#### Scenario: Settings form displays path input
- **WHEN** a settings tab displays a path input
- **THEN** the input SHALL use a consistent 32-pixel height and SourceGit border states
- **AND** browse and clear actions SHALL appear inside the input right-content area

#### Scenario: Settings footer displays actions
- **WHEN** the settings window is open
- **THEN** folder access and status SHALL remain on the left side of the footer
- **AND** reset and save actions SHALL form a right-aligned group with equal height

#### Scenario: Settings window uses its minimum width
- **WHEN** the settings window is resized to its supported minimum width
- **THEN** footer actions SHALL remain inside the window
- **AND** form inputs and embedded actions SHALL not overlap

#### Scenario: Settings tabs use a consistent form ratio
- **WHEN** the user changes between settings form tabs
- **THEN** each form SHALL use the same responsive label and editor column ratio
- **AND** primary editors SHALL share the same left and right boundaries
