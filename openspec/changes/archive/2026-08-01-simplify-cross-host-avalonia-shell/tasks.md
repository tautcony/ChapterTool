## 1. Define Host Contracts

- [x] 1.1 Inventory the current `IWindowService`, `IAuxiliaryToolHost`, `IEmbeddedToolPresenter`, `IRuntimeCapabilities`, file, output, clipboard, settings, and tool-port usages.
- [x] 1.2 Add a stable typed tool identifier (constants-backed readonly record struct with ordinal case-insensitive equality) and the shared auxiliary-tool request/result contracts.
- [x] 1.3 Add required host service groups for workspace operations, host effects, settings and appearance, localization, runtime capabilities, and auxiliary-tool hosting.
- [x] 1.4 Add explicit unavailable and native no-content adapters for capabilities and embedded presentation.
- [x] 1.5 Add the injected tool catalog and typed tool descriptor contracts.
- [x] 1.6 Add contract-level validation that rejects missing required host services and duplicate tool identifiers during composition.
- [x] 1.7 Add a host-owned close-confirmation port for descriptors that declare close confirmation.

## 2. Build the Shared Tool Catalog

- [x] 2.1 Move standard tool descriptor construction behind a catalog factory that accepts host-owned factories for file pickers, clipboard, settings, and auxiliary-tool hosting.
- [x] 2.2 Ensure each descriptor declares its title resource key, size constraints, required tool ports, and content refresh policy. Declare Zones and Forward Shift refreshable and Settings, Log, Language, Expression, Template Names, and Preview reusable.
- [x] 2.3 Remove static lookup precedence from `ToolWindowRegistry` and provide a compatibility adapter only until all consumers use the injected catalog.
- [x] 2.4 Add runtime tests that a custom descriptor replaces the standard descriptor for the same tool identifier.

## 3. Migrate Desktop Composition

- [x] 3.1 Create the desktop host service groups in `AppCompositionRoot` and pass them through one explicit shared-shell composition boundary.
- [x] 3.2 Construct the desktop Native Window auxiliary-tool host with the injected catalog and desktop close-confirmation, picker, clipboard, theme, font, and shell adapters.
- [x] 3.3 Keep composition-owned formatter, expression authoring, export, localizer, settings, logger, and importer instances shared across the main shell and tools.
- [x] 3.4 Remove production-only nullable fallbacks from desktop composition after compatibility adapters are active.
- [x] 3.5 Add a composition smoke test that resolves the main shell, tool catalog, auxiliary-tool host, and standard tool descriptors.

## 4. Migrate Main Shell Integration

- [x] 4.1 Change main-window auxiliary-tool commands to use stable typed tool identifiers and the shared auxiliary-tool host contract.
- [x] 4.2 Remove the `object?` main ViewModel parameter from the production auxiliary-tool request path and update fakes and callers.
- [x] 4.3 Change `MainView` construction to receive an explicit embedded-content presenter and keep the file picker factory as an explicit host-supplied port.
- [x] 4.4 Remove the runtime cast from `MainView` to `IEmbeddedToolPresenter` and remove the hidden fallback wiring from the window service.
- [x] 4.5 Verify that Native Window hosts keep the embedded content region hidden and Embedded hosts update it through presenter notifications.

## 5. Decouple Tool ViewModels

- [x] 5.1 Create a workspace tool-session facade beside `MainWindowViewModel` that exposes the existing narrow expression, preference, export, naming, and chapter-edit ports.
- [x] 5.2 Move port adapter ownership from the public `MainWindowViewModel.PortAdapters` surface to the tool-session facade.
- [x] 5.3 Migrate preview, expression, settings, language, template-names, and forward-shift tool creation to receive only the port each tool uses.
- [x] 5.4 Keep the narrow port interfaces separate and remove concrete `MainWindowViewModel` dependencies from tool ViewModels and tool factories.
- [x] 5.5 Add unit tests that construct each tool ViewModel with a minimal fake port and no main-window object.
- [x] 5.6 Define the session-facade notification port that refreshes main-shell expression fields, row grid, and status, and add cross-object notification tests.

## 6. Fix Secondary-Surface Lifecycle

- [x] 6.1 Make repeated open requests activate existing tool content instead of disposing and recreating its DataContext.
- [x] 6.2 Implement descriptor-level refresh behavior for tools that explicitly need new request data.
- [x] 6.3 Ensure close and host disposal detach content and dispose each disposable DataContext exactly once.
- [x] 6.4 Refresh titles and existing tool localization state on culture changes without recreating tool content.
- [x] 6.5 Add Headless behavior tests for settings state preservation, disposal, culture refresh, refreshable tools, close confirmation, Embedded host disposal, unknown tools, and custom descriptors.
- [x] 6.6 Move Settings close confirmation behind the host-owned close-confirmation port and verify Save, Discard, and Cancel behavior.

## 7. Validate Cross-Host Behavior

- [x] 7.1 Add a Native Window auxiliary-tool host contract test suite using the desktop test adapter.
- [x] 7.2 Add an Embedded auxiliary-tool host contract test adapter and verify the same tool identifiers, port behavior, and lifecycle rules.
- [x] 7.3 Add unavailable-capability tests for local paths, clipboard, and external processes.
- [x] 7.4 Preserve and run existing workspace revision, projection, localization, and tool-port tests without changing Core workflow expectations.
- [x] 7.5 Keep Avalonia unit and Headless test projects separate and keep all Headless classes in `AvaloniaHeadlessTestCollection`.
- [x] 7.6 Add contract tests that tool identifiers resolve case-insensitively and that unknown identifiers return the safe no-op result.

## 8. Remove Obsolete Paths and Update Documentation

- [x] 8.1 Remove the static registry, object parameter path, runtime service cast, obsolete optional context properties, `IWindowService`, `IInViewSecondarySurface`, the unused `ISecondarySurfaceService`, and the test-only `RecordingWindowService` after all consumers migrate.
- [x] 8.2 Move browser-only adapter implementations to their owning host or document why a shared contract remains in the shared assembly.
- [x] 8.3 Update `docs/code-map/avalonia.md` with the host composition root, tool catalog, auxiliary-tool host implementations, and primary tests.
- [x] 8.4 Update `docs/code-map/testing.md` with cross-host contract and lifecycle test lookup paths.
- [x] 8.5 Review all modified documentation for short, controlled English and consistent terms.

## 9. Verification Gates

- [x] 9.1 Run `dotnet test tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj --no-restore`.
- [x] 9.2 Run `dotnet test tests/ChapterTool.Avalonia.Headless.Tests/ChapterTool.Avalonia.Headless.Tests.csproj --no-restore` in a separate process.
- [x] 9.3 Run `dotnet build src/ChapterTool.Avalonia/ChapterTool.Avalonia.csproj --no-restore`.
- [x] 9.4 Run `dotnet test ChapterTool.slnx --no-restore` after the focused runs pass.
- [x] 9.5 Run `openspec validate "simplify-cross-host-avalonia-shell" --strict` and record any intentional compatibility differences before implementation is considered complete.
