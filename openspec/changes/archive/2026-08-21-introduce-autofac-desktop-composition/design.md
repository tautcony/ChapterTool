## Context

The desktop Avalonia application currently builds its object graph in `AppCompositionRoot`. The root owns many service instances and exposes factory methods for load, save, import, export, localization, settings, tool hosting, and platform effects. This preserves shared identities, but each new dependency requires another manual construction path.

The previous shell change introduced typed host contracts and explicit tool descriptors. Those contracts are suitable Autofac registration boundaries. The desktop host now needs a container that resolves constructor dependencies, groups registrations by ownership, supports test overrides, and validates the graph before the first workflow runs.

The shared Avalonia UI assembly must remain host-neutral. It must not depend on Autofac or on desktop-only adapters. Browser and command-line hosts must not load the desktop container.

## Goals / Non-Goals

**Goals:**

- Add Autofac to the desktop composition project.
- Register the production graph through explicit Autofac modules.
- Resolve the main window and its ViewModel through constructor injection.
- Preserve shared service identity for localization, settings, expression, export, formatter, tool catalog, and external-tool services.
- Make platform services replaceable through test modules or registration overrides.
- Keep window-scoped picker and clipboard creation host-owned.
- Validate required root registrations and constructor resolution during startup and tests.
- Dispose the Autofac lifetime scope and owned disposable services at application exit.

**Non-Goals:**

- Do not add property injection or field injection to ViewModels or application services.
- Do not move Autofac references into Core, Contracts, browser, or shared Avalonia UI projects.
- Do not replace direct unit-test constructors for classes that benefit from small fake graphs.
- Do not use assembly-wide scanning for every type. Registration modules must retain ownership boundaries.
- Do not change Core workflow behavior, settings data, import formats, or user-facing tool behavior.
- Do not make `Window`, `Control`, `TopLevel`, or other live Avalonia visual objects global singletons.

## Decisions

### Use Autofac as the desktop container

The desktop project SHALL reference Autofac and own the container. Autofac provides module composition, constructor selection, keyed registrations, lifetime scopes, registration overrides, and container diagnostics that match the current composition problem. The built-in Microsoft container remains suitable for simple hosts, but it would require more custom registration structure for the module and keyed tool-host boundaries in this application.

### Use constructor injection as the default

Autofac SHALL resolve application services, workflows, ViewModels, tool descriptors, and host services through public constructors. A constructor SHALL list every required dependency. Property or field injection is excluded from the normal production path because it hides required dependencies and weakens compile-time and test-time guarantees.

### Split registrations into ownership modules

The desktop composition SHALL define modules for logging, Core workflow services, Infrastructure services, Avalonia platform services, auxiliary-tool descriptors and hosts, and application-shell objects. Each module SHALL register interfaces at the boundary where the implementation is owned. Modules SHALL use explicit registrations for host-sensitive services. Scanning MAY be used only for stable descriptor conventions and SHALL be constrained to the intended assembly and service type.

### Keep shared instances in the application lifetime

Formatter, expression engine and authoring service, export service, settings store, localization manager, font catalog and application service, theme service, external-tool locator, process runner, tool catalog, and the Native Window auxiliary-tool host SHALL use one application lifetime instance unless a service has an explicit per-window or per-tool state contract. Load and save services MAY be transient when their dependencies are shared and stateless.

`MainWindowViewModel` and `MainWindow` SHALL be resolved as application-window instances. Tool ViewModels SHALL be created by the auxiliary-tool host for each tool content instance and SHALL be disposed with that content.

### Keep live window dependencies outside the root graph

`AvaloniaFilePickerService`, `AvaloniaSettingsPickerService`, and `AvaloniaClipboardService` require a concrete `Window`. Autofac SHALL inject factories or host-owned delegates for these services. The root container SHALL not register a fabricated or global `Window` instance.

### Use host modules and test overrides

The production desktop host SHALL register Native Window adapters and desktop capabilities. A test composition SHALL be able to override load, save, importer, shell, clipboard, settings, catalog, auxiliary-tool host, and runtime capability registrations before the container is built. The test graph SHALL not mutate production singletons after resolution.

### Validate before showing the main window

The composition root SHALL build the container, validate required registrations, resolve the main window and its primary dependencies, and only then assign the window to the Avalonia desktop lifetime. Validation failures SHALL identify the missing or invalid registration before user interaction.

### Keep `AppCompositionRoot` as a container owner

`AppCompositionRoot` SHALL resolve options, create and configure the Autofac container, expose narrowly scoped test hooks, and dispose the container. It SHALL not manually construct the full application graph through a chain of `new` expressions. Shared factory methods needed by CLI consumers MAY remain in a separate shared factory module, but they must use the same registration rules where the contracts overlap.

## Risks / Trade-offs

- [Risk] Container resolution can hide the concrete dependency graph at the call site. -> Keep constructors explicit, use module-level ownership comments where needed, and fail validation for missing registrations.
- [Risk] Lifetime mistakes can retain tool ViewModels or windows. -> Use application lifetime for shared services, create tool content through host-controlled factories, and test disposal exactly once.
- [Risk] Autofac can become a dependency of shared assemblies through accidental references. -> Reference Autofac only from `ChapterTool.Avalonia` and desktop composition tests, and check project references in build validation.
- [Risk] Broad assembly scanning can register unintended types. -> Prefer explicit registrations and constrain any scanning by assembly, service interface, and naming convention.
- [Risk] Test overrides can diverge from production registration. -> Build tests from production modules plus a final override module and run container validation on both graphs.
- [Risk] Window factories can be resolved from the wrong scope. -> Keep window creation delegates in the desktop host and pass the active window at tool-open time.
- [Risk] Startup validation can create services with external side effects. -> Separate graph validation from settings loading and process execution, and use lazy or factory registrations for side-effecting actions.

## Migration Plan

1. Add the pinned Autofac package to the desktop project and the required composition test project.
2. Introduce module types and register existing shared service instances with the intended application lifetime.
3. Register the tool catalog, Native Window host, host-owned window factories, main ViewModel, main view, and main window.
4. Add a production composition validation path and test-module override path.
5. Migrate desktop startup to resolve `MainWindow` from the container and dispose the lifetime scope on exit.
6. Remove manual graph construction from `AppCompositionRoot` while keeping narrowly scoped compatibility methods required by CLI and tests.
7. Migrate composition identity and Headless tests to assert container resolution, shared lifetimes, tool disposal, and overrides.
8. Update code maps and run focused tests, the Avalonia build, the full solution test, and strict OpenSpec validation.

Rollback consists of restoring the previous explicit desktop composition while retaining the typed host contracts. No persisted data or user settings require migration.

## Open Questions

- The exact Autofac package version must match the repository's .NET 10 package policy at implementation time.
- The implementation must decide whether the main window is resolved directly by Autofac or through a small `MainWindowFactory` when the title and window-owned delegates require runtime values.
