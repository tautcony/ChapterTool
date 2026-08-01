## Context

`ChapterTool.Avalonia.UI` is intended to serve more than one Avalonia host. The current desktop path creates a `MainWindowViewModel`, a `MainView`, and an `AvaloniaWindowService` from `AppCompositionRoot`. The window service then resolves a static tool registry and creates tool ViewModels from an optional-heavy `ToolWindowCreateContext`.

The shared layer also contains host capability models and an embedded-tool branch. The branch is reached through a runtime cast from `MainView` to `IInViewSecondarySurface`. Native-window and embedded-tool behavior therefore share some contracts, but they do not share one explicit composition boundary.

The change must preserve the existing `ChapterWorkspace`, async revision rules, projection behavior, localization, tool ViewModels, and process isolation between Avalonia unit and Headless tests. It must improve host substitution without making the shared layer depend on desktop window types.

## Goals / Non-Goals

**Goals:**

- Define one explicit host composition boundary for the shared Avalonia shell.
- Allow a host to select Native Window, Embedded, or another tool-presentation implementation without changes to the main workflow ViewModel.
- Keep source, output, clipboard, settings, theme, font, and process services host-owned.
- Replace the static tool registry with an injected catalog that has one source of truth.
- Reuse an open tool ViewModel and preserve its state.
- Keep tool ViewModels dependent on narrow workspace ports.
- Make host and tool contracts testable without requiring a particular desktop host.

**Non-Goals:**

- Do not make the Blazor WebAssembly application consume Avalonia UI in this change.
- Do not move `ChapterWorkspace` into another project.
- Do not change chapter import, edit, projection, expression, or export semantics.
- Do not introduce a dependency injection container.
- Do not redesign the visible main-window layout.
- Do not merge Avalonia unit and Headless test projects.

## Decisions

### 1. Use a required host composition record with capability groups

Introduce a shared composition boundary that receives required services in typed groups. The groups cover workspace operations, host effects, settings and appearance, and auxiliary tool hosting. The boundary must not use nullable service fields to represent normal host differences.

A host that cannot provide a capability supplies an explicit unavailable implementation or capability value. The shared shell can then bind visibility and command availability to `IRuntimeCapabilities` without checking for null services.

This is preferred over passing the current 16 constructor arguments to every ViewModel. It makes host requirements visible at one boundary and lets the host build one consistent object graph. It is also preferred over a service locator because construction remains explicit and compile-time visible.

The boundary record and its group types live in the shared UI assembly and contain no Avalonia window types. Window-scoped factory services (file picker, clipboard) are never fields of the boundary record; the auxiliary-tool host injects them into the typed shell context when a host window exists.

### 2. Keep host-neutral ports in the shared UI assembly

The shared layer owns interfaces and data contracts for source loading, output, clipboard, settings, localization, runtime capabilities, and auxiliary tool hosting. Concrete desktop adapters remain in `ChapterTool.Avalonia`. A future Avalonia host can provide different implementations without changing `MainWindowViewModel` or tool ViewModels.

Browser-specific adapters that do not participate in Avalonia composition remain outside the Avalonia shell boundary. Their contracts may be shared, but their implementation must belong to the host that owns the browser API.

### 3. Replace the static registry with an injected tool catalog

Define a stable tool identifier and a `ToolDescriptor` containing the identifier, title resource key, size constraints, and a typed content factory. The host composition creates an `IToolCatalog` and passes it to the auxiliary-tool host.

The catalog is the only lookup source. It must not fall back to a static default registry. Shared code may provide a factory for the standard tool descriptors, but the resulting catalog is an instance owned by the host.

The tool creation context contains only required typed objects: the host window when one exists, the workspace tool session, and host service groups. It must not contain a list of unrelated optional dependencies.

Window-scoped factories for file picking and clipboard are host-injected into this context at open time, alongside the host window. They never appear in the shared boundary record.

### 4. Separate auxiliary-tool hosting from embedded presentation

`IAuxiliaryToolHost` is the command-facing port. It opens and closes a stable `ToolId` and receives a typed shell context, not `object?`. Native-window and Embedded implementations both implement this port.

Embedded content presentation is a separate explicit `IEmbeddedToolPresenter` supplied to `MainView`. `MainView` must not cast `WindowService` to discover whether the host supports embedded content. A native-window host supplies a no-content presenter. An Embedded host supplies a presenter that raises content-change notifications.

This keeps the main view reusable while making the visual presentation choice a host composition decision.

Close confirmation for unsaved tool state is a host-owned presentation concern. A descriptor may declare that its tool requires close confirmation; the host then runs Save, Discard, or Cancel through its own surface. Native-window and Embedded hosts apply the same confirmation rules, and tools must not assume a native window `Closing` event.

### 5. Reuse existing tool content

The auxiliary-tool host keeps an open-tool entry keyed by `ToolId`. Opening an existing tool activates it and updates only the typed request data that the descriptor explicitly declares as refreshable. It does not dispose and recreate the DataContext by default.

Standard descriptors declare Zones and Forward Shift as refreshable because their content derives from the current row selection. Settings, Log, Language, Expression, Template Names, and Preview are reusable.

Closing a tool disposes its disposable DataContext exactly once and removes the entry. Culture changes update the existing title and localized ViewModel state through existing localization subscriptions.

### 6. Decouple tool ports from `MainWindowViewModel`

Create the narrow tool ports from a workspace-session facade that is composed beside the main ViewModel. The facade may delegate to the existing workspace and workflow collaborators, but tool ViewModels must not require the concrete main ViewModel type.

The existing narrow interfaces remain separate by capability. They must not be replaced by one unrestricted owner interface. A grouped session object may expose the narrow interfaces to composition, while each tool receives only the interface it uses.

The facade also owns an explicit notification port that refreshes main-shell bindings — expression fields, row grid, and status — when a tool applies changes. Tools must not reach the concrete `MainWindowViewModel` for notification.

### 7. Migrate with compatibility adapters

Introduce the new catalog and host contracts first. Adapt the current desktop window service and the current port adapters behind those contracts. Then migrate `MainView` and the tool factories. Remove the static registry, object parameter, runtime cast, nullable production-only fallback paths, the superseded `IWindowService` and `IInViewSecondarySurface` contracts, the unused `ISecondarySurfaceService`, and the test-only `RecordingWindowService` after the new path has focused unit and Headless coverage.

The migration keeps public behavior stable. It does not require a data migration or a settings migration.

## Risks / Trade-offs

- [Risk] A composition record can become another service bag. → Keep groups small, require all fields, expose no general-purpose lookup, and review every new field as a host capability decision.
- [Risk] Tool reuse can preserve stale request-specific content. → Give each descriptor an explicit refresh policy (Zones and Forward Shift refreshable) and test both reusable and refreshable tools.
- [Risk] Removing the runtime cast can break Embedded layout updates. → Add a Headless Embedded presenter test that drives content changes and verifies visibility and content replacement.
- [Risk] Moving adapters between shared and host projects can break the future browser plan. → Move only implementations that are not used by Avalonia hosts; retain host-neutral contracts and document the ownership in the code map.
- [Risk] Constructor and port changes can create broad test churn. → Add test composition factories and migrate unit tests before changing production fallback behavior.
- [Risk] Native and Embedded hosts can diverge in close and disposal behavior. → Run the same auxiliary-tool contract tests against both implementations.

## Migration Plan

1. Add typed tool identifiers, host service groups, the tool catalog, and auxiliary-tool host contracts.
2. Add an adapter that exposes the current desktop registry and window service through the new catalog without changing visible behavior.
3. Move standard tool descriptor construction into host composition and remove static lookup precedence.
4. Change `MainView` to receive an explicit embedded-content presenter. Add the native no-content presenter.
5. Change auxiliary-tool commands and test doubles to use typed tool identifiers and shell context.
6. Change tool creation to reuse existing content. Add lifecycle and state-preservation tests.
7. Move port adapter ownership behind the workspace tool-session facade and remove concrete main ViewModel dependencies from tools.
8. Remove obsolete static registry, object parameter, runtime cast, nullable production fallback paths, the superseded `IWindowService` and `IInViewSecondarySurface` contracts, and the test-only `RecordingWindowService`.
9. Update code maps and run focused unit tests, Headless tests, the Avalonia build, and the full solution test sequence.

Rollback consists of restoring the previous desktop composition adapter before removing compatibility types. No persisted data or user settings require rollback handling.

## Open Questions

- Future Avalonia hosts are in scope. The Blazor WebAssembly application remains a separate host until a later change explicitly chooses to share the Avalonia shell with it.
- `ToolId` is a constants-backed readonly record struct with ordinal case-insensitive equality, preserving the current matching behavior and stable IDs in tests and localization lookup. Contract tests cover case-insensitive resolution and unknown identifiers.
