## Why

The desktop Avalonia composition root creates a large service graph by hand. Each new service or shared instance requires changes in several factory methods and increases the risk of inconsistent lifetimes or duplicate instances.

The desktop host needs a maintained dependency-injection boundary with automatic constructor resolution, modular registration, and container validation.

## What Changes

- Add Autofac to the desktop Avalonia composition project.
- Register desktop services through Autofac modules grouped by ownership and lifetime.
- Resolve `MainWindow`, `MainWindowViewModel`, the auxiliary-tool host, and shared services from the Autofac container.
- Use constructor injection as the default injection method for application services, workflows, ViewModels, and tool factories.
- Keep window-scoped file picker and clipboard factories outside the global container when they require a concrete Avalonia `Window`.
- Add explicit host and test modules that can replace platform services and workflow services.
- Validate the container during startup and in composition tests.
- **BREAKING** Remove the manual service-construction path from `AppCompositionRoot` after the Autofac path is verified.
- Keep Autofac references in the desktop composition project and test composition only.
- Do not add Autofac references to Core, Contracts, browser, or shared Avalonia UI assemblies.

## Capabilities

### New Capabilities

- `autofac-desktop-composition`: Autofac registration modules, constructor injection, service lifetimes, host resolution, and dependency-graph validation for the desktop Avalonia application.

### Modified Capabilities

- `supporting-ui-platform-services`: The application composition root resolves the desktop shell from Autofac and exposes replaceable registrations for tests while preserving shared service identity and host-owned platform adapters.

## Impact

- `src/ChapterTool.Avalonia/Composition/AppCompositionRoot.cs` becomes a thin Autofac container owner.
- New Autofac modules will own registrations for logging, Core workflows, Infrastructure services, Avalonia platform adapters, and auxiliary tools.
- `App` and desktop startup code will resolve the main window from the container and dispose the container at application exit.
- Desktop composition tests will verify required registrations, shared singleton identity, scoped tool behavior, and test overrides.
- The desktop project will add the `Autofac` package.
- Existing direct constructors remain available for focused unit tests where a test needs a minimal fake graph.
