## 1. Add Autofac Boundaries

- [x] 1.1 Add a pinned Autofac package reference to `src/ChapterTool.Avalonia` and the required desktop composition test project.
- [x] 1.2 Define the desktop container owner and registration options for settings directory, startup path, capabilities, and test overrides.
- [x] 1.3 Keep Autofac references out of Core, Contracts, `ChapterTool.Avalonia.UI`, `ChapterTool.Wasm`, and command-line projects.

## 2. Create Registration Modules

- [x] 2.1 Add an Autofac logging module that registers the logger factory, Serilog provider, application log service, and logger ownership.
- [x] 2.2 Add a workspace module for formatter, frame-rate service, expression engine, expression authoring, export, editing, segment, load, save, and importer services.
- [x] 2.3 Add an infrastructure module for settings store, external-tool locator, process runner, native dependency services, and shared runtime factories.
- [x] 2.4 Add an Avalonia platform module for localization, theme, font, shell, desktop capabilities, unavailable adapters, and host-owned picker factories.
- [x] 2.5 Add an auxiliary-tool module for the standard catalog, descriptor registrations, Native Window host, close-confirmation port, and embedded presenter boundary.
- [x] 2.6 Add an application-shell module for `MainWindow`, `MainView`, `MainWindowViewModel`, and title/file-picker/presenter factories.
- [x] 2.7 Use explicit registrations and constrained descriptor scanning only where the registration convention is stable and testable.

## 3. Migrate Production Resolution

- [x] 3.1 Refactor `AppCompositionRoot` to build an Autofac container and expose the application lifetime scope.
- [x] 3.2 Resolve `MainWindow` from Autofac before assigning it to the Avalonia desktop lifetime.
- [x] 3.3 Replace manual `CreateHostDependencies()` and equivalent service chains with module registrations.
- [x] 3.4 Preserve shared service identity for formatter, expression, export, settings, localization, external-tool, tool catalog, and host services.
- [x] 3.5 Keep window-scoped file picker, settings picker, clipboard, and close-confirmation creation bound to the active window.
- [x] 3.6 Keep CLI shared factory behavior aligned without making CLI load the desktop Autofac container.

## 4. Apply Constructor Injection

- [x] 4.1 Migrate production application services and workflows to constructor-resolvable dependencies.
- [x] 4.2 Migrate `MainWindowViewModel`, session adapters, and shell collaborators without adding property or field injection.
- [x] 4.3 Migrate tool descriptor and auxiliary-host construction to the registered typed boundaries.
- [x] 4.4 Remove production-only service locator and manual fallback paths introduced by the old composition root.
- [x] 4.5 Keep direct constructors needed by focused unit tests and document any deliberate factory boundary.

## 5. Add Validation And Test Overrides

- [x] 5.1 Add a production composition validation method that resolves `MainWindow`, `MainWindowViewModel`, `IToolCatalog`, `IAuxiliaryToolHost`, and the importer registry without showing a window or starting a process.
- [x] 5.2 Add a test module or registration callback that replaces load, save, importer, shell, settings, capabilities, catalog, and auxiliary-tool services.
- [x] 5.3 Add tests for missing-registration failure before startup, shared singleton identity, test override precedence, and repeated disposal.
- [x] 5.4 Add Headless behavior coverage for main-window resolution, Native Window tool lifecycle, Embedded host behavior, and active-window factory use.
- [x] 5.5 Keep Avalonia unit and Headless tests in separate projects and run them in separate processes.

## 6. Documentation And Cleanup

- [x] 6.1 Remove obsolete manual composition methods after Autofac resolution passes focused tests.
- [x] 6.2 Update `docs/code-map/avalonia.md` with module ownership, container entry points, lifetimes, and test lookup paths.
- [x] 6.3 Update `docs/code-map/testing.md` with Autofac composition validation and override tests.
- [x] 6.4 Document the constructor-injection rule and the prohibition on production property or field injection.
- [x] 6.5 Review changed documentation for short controlled English and consistent Autofac terms.

## 7. Verification Gates

- [x] 7.1 Run focused Avalonia unit tests with `--no-restore` after package and module changes.
- [x] 7.2 Run focused Avalonia Headless tests in a separate process.
- [x] 7.3 Build `src/ChapterTool.Avalonia/ChapterTool.Avalonia.csproj --no-restore`.
- [x] 7.4 Run `dotnet test ChapterTool.slnx --no-restore` after focused tests pass.
- [x] 7.5 Run `openspec validate "introduce-autofac-desktop-composition" --strict`.
