# Avalonia Browser Shared UI Clean-Cut Plan

## Status

This document defines the target architecture for the desktop and browser applications.

The implementation must replace the Blazor browser interface in one clean cut. The repository must not merge a dual-interface state.

No active OpenSpec change existed when this plan was written. Implementation must start with one OpenSpec change for the complete cutover.

## Thesis

`ChapterTool.Avalonia.UI` must become the only owner of the graphical user interface and its presentation workflows.

`ChapterTool.Avalonia` and `ChapterTool.Wasm` must become thin runtime hosts. Each host must supply its own platform adapters.

## Confidence

- **Confidence level**: High
- **Evidence**: Avalonia 12.1 supplies a `net10.0-browser` host and `ISingleViewApplicationLifetime`.
- **Evidence**: `ChapterTool.Core` already supplies byte-based import APIs for browser WebAssembly.
- **Evidence**: The current browser application already proves the portable browser feature set.
- **Remaining risk**: Each shared Avalonia control package must pass a browser publish and runtime smoke test.

## First Publish Evidence

- The Release browser project published successfully to `artifacts/avalonia-browser-proof-untrimmed`.
- The publish included `ChapterTool.Avalonia.UI`, Avalonia Browser, DataGrid, AvaloniaEdit, SVG, icon, theme, font, image, and localization resources.
- The default trimmed publish failed because `Avalonia.Controls.DataGrid` emitted `IL2104` and `NETSDK1144`.
- The browser project disables trimming until the DataGrid package provides a clean trim analysis result.
- Runtime smoke verification remains pending until the browser adapters and bootstrap are complete.

## The Trap

- **Inherited constraint**: The desktop project currently owns the views, ViewModels, composition root, and desktop adapters.
- **Inherited constraint**: The browser project currently duplicates the interface and workspace with Blazor.
- **Is the constraint real?**: No.
- **Reason**: These boundaries are internal implementation choices.
- **Real contracts**: The browser URL, persisted browser settings, user workflows, and desktop behavior must remain stable.

## Decision

The implementation must use the clean target.

| Option | Result | Cost | Decision |
| --- | --- | --- | --- |
| Conservative path | Keep Blazor and share selected services | Two interface models and two presentation states remain | Reject |
| Clean target | Use one Avalonia UI library and two thin hosts | One large coordinated change | Select |
| Staged clean path | Add Avalonia Browser before deleting Blazor | A temporary dual path can escape into the main branch | Reject |

Work can use incomplete local commits on one branch. The branch must satisfy all cutover gates before merge.

## Non-Negotiable Rules

1. `ChapterTool.Avalonia.UI` must contain the only main interface implementation.
2. The shared UI must not reference `ChapterTool.Avalonia`, `ChapterTool.Wasm`, `ChapterTool.Infrastructure`, or `ChapterTool.CommandLine`.
3. The shared UI must not test the runtime name to select an implementation.
4. Each host composition root must select its platform adapters.
5. `IRuntimeCapabilities` must control visible state and command state.
6. A capability flag and its adapter behavior must agree.
7. The browser host must not reference `ChapterTool.Infrastructure` or `ChapterTool.CommandLine`.
8. The final tree must not contain Razor pages, `WasmWorkspace`, or Blazor packages.
9. The browser settings migration must preserve the `chaptertool.wasm.settings` key and schema version `1`.
10. The final change must preserve behavior tests before it deletes their old implementation targets.

## Scope

The clean cut includes these changes:

- Add the `ChapterTool.Avalonia.UI` class library.
- Change the main interface root from `Window` to `UserControl`.
- Keep a desktop `Window` wrapper in `ChapterTool.Avalonia`.
- Replace the Blazor project content with an Avalonia Browser host.
- Replace path-only presentation contracts with source-document contracts.
- Add browser source, download, clipboard, settings, and secondary-surface adapters.
- Add runtime capability state for all platform differences.
- Move shared tests to the shared UI behavior path.
- Update build, publish, deployment, documentation, and code maps.
- Delete all superseded Blazor code in the same change.

## Non-Goals

- The change will not add a mobile host.
- The change will not add browser access to local external processes.
- The change will not add browser access to arbitrary local paths.
- The change will not add browser imports that require `ffprobe`, `mkvtoolnix`, `eac3to`, or `ffmpeg`.
- The change will not add browser popup windows.
- The change will not change the Node.js WebAssembly host.
- The change will not delete the desktop CLI launch path.
- The change will not preserve Blazor extension points or Blazor component APIs.

## Target Projects

### `ChapterTool.Avalonia.UI`

This project must target `net10.0`. It must be a class library.

The project must reference `ChapterTool.Core`. It must use browser-compatible Avalonia packages.

The project must own these items:

- `MainView` and all shared tool views
- Shared ViewModels and commands
- Main workflow coordinators
- Localization resources and localization state
- Shared styles, themes, fonts, icons, and images
- Shared runtime capability contracts
- Shared source, output, clipboard, settings, and secondary-surface ports
- Shared UI composition helpers
- Responsive layout behavior
- Accessibility names and keyboard routing

The project must not own these items:

- An application executable entry point
- A desktop application lifetime
- A browser application lifetime
- Sentry startup
- File-system settings storage
- Browser JavaScript interop
- External process execution
- Desktop shell execution
- Native application icons or browser site files

### `ChapterTool.Avalonia`

This project remains the desktop executable. It must reference `ChapterTool.Avalonia.UI`.

The desktop host must own these items:

- `Program.cs`
- The desktop `App` and `IClassicDesktopStyleApplicationLifetime`
- The `MainWindow` wrapper
- Desktop startup path and CLI launch selection
- Sentry and Serilog startup
- Desktop composition root
- Path-based imports and external-tool imports
- Desktop directory output
- Desktop file and folder pickers
- Native auxiliary windows
- Desktop clipboard and shell actions
- File-system settings storage
- Desktop font discovery
- Native application assets and packaging

The `MainWindow` wrapper must set the title, icon, minimum size, and initial size. Its content must be one shared `MainView`.

### `ChapterTool.Wasm`

This project keeps its current project name and public deployment role. Its implementation must change to Avalonia Browser.

The project must use these project properties:

- SDK: `Microsoft.NET.Sdk.WebAssembly`
- Target framework: `net10.0-browser`
- Output type: `Exe`
- Package: `Avalonia.Browser` version `12.1.0`
- Project reference: `ChapterTool.Avalonia.UI`

The browser host must own these items:

- Browser `Program.cs`
- The browser `App` and `ISingleViewApplicationLifetime`
- Browser composition root
- Byte-based source selection and drag/drop conversion
- Browser download output
- Browser clipboard access
- Browser `localStorage` settings
- In-view secondary surfaces
- Browser capability probing
- `wwwroot/index.html`
- Avalonia browser bootstrap JavaScript
- Small JavaScript bridges that Avalonia does not supply
- GitHub Pages deployment behavior

The browser host must not contain presentation state that duplicates a shared ViewModel.

## Dependency Direction

```text
ChapterTool.Core
    ^
    |
ChapterTool.Avalonia.UI
    ^                    ^
    |                    |
ChapterTool.Avalonia     ChapterTool.Wasm
    |
    +--> ChapterTool.Infrastructure
    +--> ChapterTool.CommandLine
```

The browser dependency path must end at `ChapterTool.Avalonia.UI` and `ChapterTool.Core`.

The desktop host can adapt Infrastructure services to shared UI ports. The shared UI must not consume Infrastructure types directly.

## Application Lifetimes

Each host must define its own `App`. Both applications must load the resource dictionaries from `ChapterTool.Avalonia.UI`.

The desktop application must use this lifetime path:

```text
Program
  -> Desktop App
  -> DesktopCompositionRoot
  -> MainWindow
  -> MainView
  -> MainViewModel
```

The browser application must use this lifetime path:

```text
Program.StartBrowserAppAsync
  -> Browser App
  -> BrowserCompositionRoot
  -> ISingleViewApplicationLifetime.MainView
  -> MainView
  -> MainViewModel
```

One host must not start or inspect the other host lifetime.

## Main View Conversion

The existing `MainWindow.axaml` content must move to `MainView.axaml`.

The conversion must make these changes:

1. Change the root from `Window` to `UserControl`.
2. Change `RootWindow` references to `RootView` or ViewModel commands.
3. Move window title and version logic to the desktop wrapper.
4. Move initial window size and minimum size to the desktop wrapper.
5. Replace the `Opened` initialization hook with one ViewModel initialization command.
6. Replace window-height mutation with responsive rows, scrolling, or a splitter.
7. Keep drag/drop, keyboard routing, and DataGrid selection in shared UI behavior.
8. Move file-picker and output commands from the window code-behind to shared commands and platform ports.
9. Keep code-behind only for control events that Avalonia cannot bind cleanly.
10. Keep the four main workflow zones at all supported sizes.

The desktop wrapper must not duplicate any main workflow control.

## Platform Contracts

### Runtime capabilities

`ChapterTool.Avalonia.UI` must define `IRuntimeCapabilities`.

The contract must expose semantic modes. It must not expose only an `IsBrowser` value.

```csharp
public interface IRuntimeCapabilities
{
    RuntimeSourceMode SourceMode { get; }

    RuntimeOutputMode OutputMode { get; }

    RuntimeSecondarySurfaceMode SecondarySurfaceMode { get; }

    bool CanReadClipboard { get; }

    bool CanWriteClipboard { get; }

    bool CanConfigureExternalTools { get; }

    bool CanRunExternalProcesses { get; }

    bool CanOpenLocalPaths { get; }
}
```

The shared ViewModel must project these values into command and visibility properties. XAML must bind to those properties.

Host composition must select the adapters. Capability checks must not select adapters inside the shared UI.

### Source documents

The shared load workflow must stop accepting a raw path string.

It must accept a `ChapterSourceDocument` union with these variants:

- `LocalPathChapterSource` for desktop paths
- `BufferedChapterSource` for browser file names and bytes

The source contract must include a stable display name. The buffered source must keep the bytes for reload.

The browser adapter must apply `PortableInputPolicy.MaxBytes` before it allocates the final buffer. Empty input must fail with a typed diagnostic.

The desktop adapter must preserve the local path. Runtime importers can use that path for related files and external tools.

The shared workspace must store a source identity instead of assuming that every source has a local path.

### Source selection

The UI must use one `IChapterSourcePicker` port for button selection and drop conversion.

The desktop implementation must return a local-path source. The browser implementation must return a buffered source.

The browser file filter must list only portable import formats. The desktop file filter can list runtime importer formats.

### Chapter loading

The shared UI must keep one load and append workflow.

The desktop load adapter must use `RuntimeChapterImporterRegistry`. The browser load adapter must use `ChapterContentService`.

Both adapters must return `ChapterImportResult`. The shared workflow must keep the existing revision and session checks.

Reload must reuse the last successful `ChapterSourceDocument`. Append must use the same source contract.

### Output documents

The shared export workflow must build one `ChapterOutputDocument`.

The document must contain these values:

- File name
- Media type
- Encoded bytes
- Export diagnostics

The shared code must apply the selected text encoding and byte-order-mark option before it calls the output sink.

`IChapterOutputSink` must deliver the document.

- The desktop sink must write the document to the selected or configured directory.
- The browser sink must start a browser download.

The shared `Save` command must use the active output mode. `Save To` must only exist when directory output is available.

### Clipboard

The shared UI must use one `IClipboardService` port.

The desktop implementation must use the Avalonia desktop `TopLevel` clipboard. The browser implementation must use the browser clipboard API.

The browser capability probe must disable clipboard commands when the browser blocks clipboard access.

### Secondary surfaces

The shared UI must own every tool view and tool ViewModel.

`ISecondarySurfaceService` must present the tool content in one of these modes:

- Desktop: A native Avalonia `Window`
- Browser: An overlay region in `MainView`

The browser host must not emulate a native window. The shared tool lifecycle must preserve apply, cancel, close, and unsaved-change behavior.

The shared UI must distinguish `CanShowSecondarySurfaces` from `CanOpenNativeWindows`.

### External actions

The shared UI must use explicit ports for related-media actions and external-tool settings.

The desktop host must supply these ports. The browser host must not register process or local-shell adapters.

The browser capability state must hide external-tool configuration. It must disable local-path actions with a localized reason.

### Settings

The shared UI must consume an `IUiSettingsStore` port and a browser-safe settings snapshot.

The desktop adapter must map the snapshot to the existing `ChapterToolSettingsStore`. The browser adapter must store the snapshot in `localStorage`.

The browser adapter must preserve these contracts:

- Storage key: `chaptertool.wasm.settings`
- Schema version: `1`
- JSON property naming: camel case
- Application, theme, and font sections
- Existing language, export, encoding, byte-order-mark, tolerance, theme, and font values

The browser adapter must ignore desktop-only path values. It must not show them as available browser features.

### Localization and appearance

`ChapterTool.Avalonia.UI` must own one set of AXAML localization resources for `en-US`, `zh-CN`, and `ja-JP`.

The change must delete the browser JSON localization files. Both hosts must use the same localization manager and resource keys.

The shared theme service must apply Avalonia resources in both hosts. Browser CSS must not duplicate the Avalonia color palette.

The browser host can keep CSS for the HTML host surface and splash screen only.

## Capability Matrix

| Capability | Desktop host | Browser host |
| --- | --- | --- |
| Source representation | Local path | Buffered bytes and file name |
| Source picker | Avalonia desktop storage provider | Avalonia browser storage provider |
| Drag/drop | Local path source | Buffered source |
| Portable importers | Enabled | Enabled |
| External-tool importers | Enabled when tools exist | Unavailable |
| Reload | Reopen the path | Reuse the retained buffer |
| Append MPLS | Path load | Byte load |
| Default output | Directory write | Browser download |
| Save To directory | Enabled | Hidden |
| Clipboard | Avalonia desktop clipboard | Browser clipboard when allowed |
| Secondary tools | Native windows | In-view overlays |
| Related local media | Shell action | Informational data only |
| External processes | Enabled | Unavailable |
| Settings storage | Versioned file | Versioned `localStorage` value |
| System font catalog | Enabled | Fixed browser-safe choices |
| Sentry and file logs | Desktop policy | Unavailable |

## File Ownership Move

The implementation must move these groups to `src/ChapterTool.Avalonia.UI`:

- `Localization/`
- `ViewModels/`
- `Workflows/`
- `Session/Ports/`, after the ports no longer use Infrastructure types
- `Views/Controls/`
- `Views/Tools/`
- `Views/Styles/`
- Shared SourceGit resources
- Shared theme and font application logic
- Shared images
- `UiOperationBoundary.cs`

The implementation must split the existing main window:

- Move workflow layout and interaction behavior to `ChapterTool.Avalonia.UI/Views/MainView.*`.
- Keep `ChapterTool.Avalonia/Views/MainWindow.*` as a thin desktop wrapper.

The implementation must keep these groups in `src/ChapterTool.Avalonia`:

- `Program.cs`
- Desktop `App.*`
- `Diagnostics/SentryStartupConfiguration.cs`
- Native app icons and macOS assets
- Desktop composition root
- Desktop picker, output, clipboard, window, shell, font, and settings adapters
- Runtime load and save adapters that use Infrastructure

The implementation must replace the contents of `src/ChapterTool.Wasm` with browser host files and browser adapters.

## Mandatory Deletion List

The final change must delete these Blazor implementation items:

- `App.razor`
- `_Imports.razor`
- `Layout/`
- `Pages/`
- `Services/WasmWorkspace.cs`
- `Services/WasmChapterService.cs`
- `Services/WasmLocalizer.cs`
- `Services/WasmModels.cs`
- Blazor JSON localization resources
- Blazor page CSS
- Blazor-specific JavaScript functions
- `Microsoft.AspNetCore.Components.WebAssembly`
- `Microsoft.AspNetCore.Components.WebAssembly.DevServer`
- `Microsoft.NET.Sdk.BlazorWebAssembly`

The final change must also delete these superseded desktop patterns:

- A main workflow XAML root that derives from `Window`
- Window-owned file commands
- Path-only load command parameters
- Window-height mutation for expression editor expansion
- Shared ViewModel references to Infrastructure services
- Host selection through runtime-name checks

## One-Cut Implementation Sequence

The sequence describes work inside one change. It does not permit partial merge.

### 1. Prove the browser toolchain

1. Add the final `ChapterTool.Avalonia.UI` and Avalonia Browser project shapes.
2. Reference the final shared Avalonia package set.
3. Publish the browser host with the shared DataGrid, editor, icon, SVG, theme, and font resources.
4. Start the published site.
5. Verify that the shared controls render in a browser.
6. Replace any browser-incompatible package before other migration work continues.

This proof must use the final projects. Do not keep a temporary browser host.

### 2. Establish shared contracts

1. Add `IRuntimeCapabilities` and its semantic modes.
2. Add the source-document union.
3. Add the source picker and load ports.
4. Add the output document and output sink ports.
5. Add clipboard, settings, secondary-surface, and external-action ports.
6. Refactor `ChapterWorkspace` source identity where required.
7. Add contract and negative tests.

### 3. Move the shared presentation

1. Create `MainView` from the current main window content.
2. Move shared views, ViewModels, workflows, resources, and localization.
3. Move window-owned commands into the shared command path.
4. Bind command state to runtime capabilities.
5. Add the browser overlay host for secondary surfaces.
6. Remove direct Infrastructure types from the shared UI.
7. Update Headless tests to construct `MainView` through a test composition root.

### 4. Rebuild the desktop host

1. Create the thin desktop `MainWindow` wrapper.
2. Split `AppCompositionRoot` into shared creation and desktop adapter composition.
3. Connect path imports and directory output.
4. Connect desktop settings, clipboard, windows, shell actions, fonts, logging, and external tools.
5. Preserve CLI launch selection and startup path behavior.
6. Run desktop unit and Headless behavior tests.

### 5. Replace the browser host

1. Change `ChapterTool.Wasm.csproj` to Avalonia Browser.
2. Add the browser application lifetime.
3. Add buffered source selection and drag/drop.
4. Add portable byte-based loading and append.
5. Add encoded download output.
6. Add browser clipboard probing.
7. Add browser settings migration and persistence.
8. Connect browser overlay surfaces.
9. Delete all Blazor code and packages.
10. Port browser behavior tests to shared UI and browser adapter tests.

### 6. Complete the repository cutover

1. Add the new project to `ChapterTool.slnx`.
2. Update test project references.
3. Update `.github/workflows/dotnet-ci.yml`.
4. Update `.github/workflows/github-pages.yml` for Avalonia Browser assets.
5. Update `README.md` and `src/ChapterTool.Wasm/README.md`.
6. Update all applicable files under `docs/code-map/`.
7. Update this plan with final evidence.
8. Sync the OpenSpec delta specs into the main specs.
9. Validate and archive the OpenSpec change only after all gates pass.

## Test Ownership

### Shared unit tests

The non-Headless Avalonia test project must test shared ViewModels, workflows, commands, capabilities, and settings projection.

These tests must use fake source documents and fake platform ports. They must not start Avalonia Headless.

Add a compiled assembly-reference test. The test must verify that `ChapterTool.Avalonia.UI` does not reference desktop, browser, Infrastructure, or CommandLine assemblies.

### Headless UI tests

The Headless project must test `MainView` behavior in its separate process.

The tests must verify these workflows:

- Load through a selected source
- Reload the active source
- Append MPLS from a second source
- Edit and select chapter rows
- Preview and save command routing
- Capability-based Save To state
- Browser-style secondary overlay behavior
- Desktop-style secondary window routing through a fake port
- Localization refresh
- Settings apply, cancel, and close behavior
- Narrow, default, and wide responsive layout behavior

The tests must drive behavior. They must not only assert that controls exist.

### Desktop adapter tests

Desktop tests must cover these adapters:

- Local-path source selection
- Runtime importer selection
- Directory output
- Settings mapping
- Clipboard routing
- Native secondary windows
- External-tool capability state
- Startup source initialization

### Browser adapter tests

`ChapterTool.Wasm.Tests` must replace `WasmWorkspaceTests` with browser adapter and composition tests.

The tests must cover these contracts:

- The 64 MiB portable input limit
- Empty and blocked file input
- Buffered reload
- Buffered MPLS append
- Download file name and encoded bytes
- Clipboard denial
- `localStorage` schema migration
- Browser capability values
- Absence of external process and local-path actions

### Browser runtime checks

The implementation must publish and serve the browser application. A browser smoke test must verify these workflows:

1. Load a portable chapter file.
2. Edit a chapter row.
3. Open Preview in an overlay.
4. Start a download.
5. Change the interface language.
6. Reload the page and verify persisted settings.
7. Resize to narrow and wide viewports.
8. Verify that the application canvas is not blank.

Store default, narrow, and wide screenshots under `artifacts/` for manual layout evidence.

## Verification Commands

Run the project tests in sequence.

```bash
dotnet restore ChapterTool.slnx
dotnet build src/ChapterTool.Avalonia.UI/ChapterTool.Avalonia.UI.csproj --no-restore
dotnet build src/ChapterTool.Avalonia/ChapterTool.Avalonia.csproj --no-restore
dotnet build src/ChapterTool.Wasm/ChapterTool.Wasm.csproj --no-restore
dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj --no-restore
dotnet test tests/ChapterTool.Avalonia.Headless.Tests/ChapterTool.Avalonia.Headless.Tests.csproj --no-restore
dotnet test tests/ChapterTool.Wasm.Tests/ChapterTool.Wasm.Tests.csproj --no-restore
dotnet publish src/ChapterTool.Wasm/ChapterTool.Wasm.csproj -c Release --no-restore
dotnet test ChapterTool.slnx --no-restore
openspec validate --all
git diff --check
```

Do not run the .NET test commands in parallel.

## Cutover Gates

The change can merge only when all gates pass.

- `ChapterTool.Avalonia.UI` is the only main UI owner.
- Both executable hosts reference `ChapterTool.Avalonia.UI`.
- The shared UI has no forbidden assembly references.
- The desktop host preserves its current workflows.
- The browser host preserves all current portable workflows.
- The browser host uses Avalonia Browser and no Blazor package.
- The browser settings key and schema remain readable.
- Headless behavior tests pass in their separate process.
- Browser adapter tests pass.
- The browser Release publish passes.
- The published site starts from the GitHub Pages base path.
- Browser smoke workflows pass at narrow, default, and wide sizes.
- The full solution tests pass.
- The code maps describe the final ownership and entry points.
- The OpenSpec change is synchronized and strictly valid.
- No temporary host, compatibility wrapper, or dead Blazor file remains.

## First Proof Point

The first proof point is a browser Release publish that references the final `ChapterTool.Avalonia.UI` project.

The published application must render `MainView` with the actual DataGrid, editor, styles, icons, localization resources, and one tool overlay.

This proof tests the highest-risk package and resource boundary before the full move.

## Falsifiers

The target remains valid if one shared control package fails in the browser. Replace that package or control implementation.

The target is false only if a required user workflow cannot run through Avalonia Browser after package replacement and bounded adapter work.

A browser performance result also falsifies the target if normal chapter files cannot meet an agreed interaction budget on supported browsers.

The implementation must record the failing evidence before it changes the target architecture.

## Final Architecture Test

The final system must answer each question with one owner:

- **Who owns the interface?** `ChapterTool.Avalonia.UI`.
- **Who owns desktop platform effects?** `ChapterTool.Avalonia`.
- **Who owns browser platform effects?** `ChapterTool.Wasm`.
- **Who owns chapter rules and portable import/export?** `ChapterTool.Core`.
- **Who owns desktop runtime importers and external tools?** `ChapterTool.Infrastructure`.

Any second answer indicates that the clean cut is incomplete.
