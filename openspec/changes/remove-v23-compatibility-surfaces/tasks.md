## 1. UI and selector cleanup

- [x] 1.1 Replace the expression completion converter alias with the concrete presentation type and remove the alias; verify the UI project builds.
- [x] 1.2 Remove the no-op expression toggle properties and unused settings port method, then update dependent tests and fakes; verify the Avalonia and WASM projects build.
- [x] 1.3 Rename `LegacyMplsCode` to `MplsFrameRateCode` across Core, desktop, WASM, Node DTOs, docs, and tests; verify no old identifier remains in shipped source.

## 2. Core and Node API migration

- [x] 2.1 Remove Core numeric compatibility constructors, projections, deconstructors, and frame-rate overloads; migrate all production consumers to canonical rates and ticks; verify Core and host projects build.
- [x] 2.2 Remove transform-bearing export options and compatibility export entry points; migrate CLI, WASM, and Node flows to explicit operations followed by serialization; verify affected projects build.
- [x] 2.3 Make the Node package expose only exact content contracts, remove numeric DTOs/wrappers, update declarations and package documentation; verify the package build completes.

## 3. Browser storage and release documentation

- [x] 3.1 Remove predecessor localStorage key migration and cleanup; verify the implementation references only the current settings key.
- [x] 3.2 Update API docs, code maps, release notes, and compatibility specs for the removals and migration paths; verify searches show no stale compatibility-window claims.
- [x] 3.3 Validate the change and inspect the complete diff; record browser E2E and packed NuGet checks not run in this follow-up.
