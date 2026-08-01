## Why

The Avalonia UI must remain usable by desktop, embedded, and future host applications. The current implementation mixes that goal with host-specific construction details. `AvaloniaWindowService`, `ToolWindowCreateContext`, the static tool registry, and the main-window adapters express the same host boundary in several different ways.

This change defines one explicit cross-host shell contract and one composition model. It keeps host-neutral workspace and tool behavior in `ChapterTool.Avalonia.UI`. It moves native-window, embedded-content, file, output, and clipboard choices to host composition. It also makes auxiliary-tool reuse preserve tool state.

## What Changes

- Introduce an explicit Avalonia host composition contract for source input, output, auxiliary tools, clipboard, settings, localization, and runtime capabilities.
- Replace the static `ToolWindowRegistry` and optional-heavy `ToolWindowCreateContext` with an injected tool catalog and typed tool creation boundary.
- Make auxiliary-tool requests use stable tool identifiers and a typed shell context instead of `object?` parameters.
- Keep native windows, embedded content, and future host presentations behind the same auxiliary-tool host contract.
- Keep narrow workspace ports for secondary tool ViewModels, but make their ownership independent from the concrete `MainWindowViewModel` type.
- Remove the cast from `MainView` to a special window-service implementation. Supply embedded tool content through an explicit presenter.
- Reuse an existing tool window and its ViewModel when the user opens the same tool again.
- Move settings close confirmation behind a host-owned close-confirmation port shared by Native and Embedded hosts.
- Preserve the existing Core `ChapterWorkspace`, revision rules, projection rules, localization behavior, and tool workflows.
- Add composition, lifecycle, host-mode, tool-port, and cross-host contract tests.
- Update `docs/code-map/avalonia.md` when ownership and entry points change.

## Capabilities

### New Capabilities

- `avalonia-host-composition`: Defines the host-neutral Avalonia shell contract and the host-owned composition model for native, embedded, and future Avalonia hosts.

### Modified Capabilities

- `avalonia-ui-shell`: Changes auxiliary-tool commands, tool ownership, main-view host integration, and tool ViewModel dependencies.
- `supporting-ui-platform-services`: Changes tool registration, tool lifecycle, composition ownership, and host adapter construction.
- `tests-build-distribution-assets`: Adds behavior coverage for host composition, tool presentation modes, tool reuse, and contract isolation.

## Impact

- Shared shell: `src/ChapterTool.Avalonia.UI/PlatformPorts/`, `ViewModels/`, `Views/MainView.axaml(.cs)`, and tool ViewModels.
- Desktop host: `src/ChapterTool.Avalonia/Composition/AppCompositionRoot.cs`, `Services/AvaloniaWindowService.cs`, `Services/ToolWindowRegistry.cs`, and desktop adapters.
- Tests: Avalonia unit tests, Avalonia Headless tests, composition tests, and host-boundary contract tests.
- Documentation: `docs/code-map/avalonia.md` and `docs/code-map/testing.md` if primary ownership or test lookup paths change.
- Public or internal API changes: `IAuxiliaryToolHost`, `IEmbeddedToolPresenter`, the close-confirmation port, tool descriptor types, `MainView` construction, and host composition factories.
- No chapter format, importer, exporter, Lua, or Core workspace behavior change is intended.
