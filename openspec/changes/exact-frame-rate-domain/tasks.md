## 1. Value and public contract baseline

- [x] 1.1 Inventory established external FPS signatures, result fields, model usage, extension contracts, and Node DTOs. Separate them from internal-only public types and record the retained major-version-23 boundary surface.
- [x] 1.2 Add immutable `FrameRateValue` with Rational, Approximate, and default Unknown. Validate reduced positive Int64 rationals and reject default invalid `ChapterFrameRate`.
- [x] 1.3 Define canonical equality/hash and invariant formatting. Test equivalent ratios, decimal scale normalization, different provenance at equal numeric rates, and invalid states.
- [x] 1.4 Add diagnostic-returning numeric boundary normalization. Test invariant round-trip doubles, positive decimals, metadata zero versus operation zero, non-finite values, decimal range, and underflow.
- [x] 1.5 Replace internal C# signatures directly. Add only necessary external adapters/projections; verify retained API named arguments, construction/deconstruction/with usage, and proven extension contracts.

## 2. Import and document ownership

- [x] 2.1 Migrate `ChapterSet`, import projections, editable document/segment current rates, and source rates to canonical storage. Remove legacy double-to-rational synthesis.
- [x] 2.2 Implement uniform-rate resolution across every contributing segment/track. Test known plus Unknown, different values, exact plus Approximate, empty/no-segment tracks, and selected-track isolation.
- [x] 2.3 Preserve MPLS per-item rates and boundaries through split import and BDMV aggregate adaptation. Cover mixed/missing primary-video rates and prevent first-item scalar authority.
- [x] 2.4 Supply authoritative IFO PAL/NTSC rates independently of IFO/NAV timing. Preserve existing source timestamps and fallback behavior.
- [x] 2.5 Remove XPL double rate arithmetic and distinguish clock bases from video FPS. Keep no-rate text, CUE, and media imports Unknown. Add representative importer tests.
- [x] 2.6 Migrate merge/split/append and compatibility flattening to preserve rates or return explicit conflicts. Verify source Unknown survives repeated conversions and undo/redo.

## 3. Core arithmetic and state

- [x] 3.1 Migrate frame display, frame editing, signed shifts, zones, detection, and frame converters to typed inputs. Preserve display-only detection separately.
- [x] 3.2 Use integer coefficient/scale arithmetic for Approximate values without upgrading provenance. Verify long timelines and all four source/target rate-kind combinations.
- [x] 3.3 Migrate FPS conversion for chapters, explicit ends, duration, and absolute segment boundaries. Verify same-rate quantization, unchanged-result no-op, metadata-only assignments, and exact Undo of off-grid conversions.
- [x] 3.4 Return diagnostics for invalid/default rate, unknown/mixed scope, overflow, end-before-start, and collapsed segments. Verify whole-candidate rejection without content/history changes.
- [x] 3.5 Migrate document equality, candidate property diffs, history deltas/branch restoration, and export fingerprints to kind plus canonical payload. Exclude display preferences and suggestions.
- [x] 3.6 Capture computation rates in candidates and output snapshots. Verify operation-rate draft invalidation and display-preference refresh without recomputation or stale commit.

## 4. Expressions and serialization

- [x] 4.1 Migrate canonical expression contexts and internal engine contracts directly; keep adapters only for established external extension contracts. Allow Unknown-rate time-only scripts and reject unresolved FPS-dependent presets.
- [x] 4.2 Preserve known numeric Lua fps globals. Add kind, ratio text, and safely representable numeric ratio globals. Update analysis, symbols, completion, presets, and help.
- [x] 4.3 Preserve original ticks when Lua returns unchanged projected time. Recompute frame metadata from validated result ticks and canonical rate. Test nil FPS and unsafe-integer ratio components.
- [x] 4.4 Migrate QPFile, celltimes, zones, and Chapter-to-QPFile to ticks-based frame calculations. Cover midpoint-away-from-zero output and valid v2 timecode maps without constant FPS.
- [x] 4.5 Add compatible typed serialization-rate overrides. Verify time-only export on Unknown, explicit rate export on mixed content, preview/save equivalence, and no document mutation or expression replay.

## 5. Desktop and Web workflows

- [x] 5.1 Migrate both host workspaces and row/preview projections to canonical rates. Preserve custom approximate and non-catalog rational selector entries.
- [x] 5.2 Implement the confirmed Auto policy in both hosts. Keep detection suggestions visible, require explicit rates for missing/mixed FPS operations, and keep time-only operations available.
- [x] 5.3 Verify load, selection/reselection, frame edit, reviewed conversion, apply, undo/redo, export, and track switching with exact, Approximate, Unknown, and mixed fixtures in both hosts.
- [x] 5.4 Verify unchanged rounded frame cells retain ticks/history and active candidates survive display changes. Verify rate-draft changes and stale candidates cannot commit.
- [x] 5.5 Verify Chinese, English, and Japanese labels/rows, accessibility, and normal/editor/preview frame width. Cover empty, valid, invalid, pending, and stale states.
- [x] 5.6 Add real-window Headless and browser workflow assertions. Capture default/wide/narrow screenshots under `artifacts/exact-frame-rate-domain/`; use real browser key events for changed editing shortcuts.

## 6. Node exact facade and compatibility

- [x] 6.1 Add discriminated rate and Int64 tick-string DTOs with strict validation and canonical formatting. Register them in `NodeJsonContext`; test values above JavaScript safe-integer range.
- [x] 6.2 Add `tool.exact` content APIs and typed JSExport bindings for import/edit/shift/zones, combine/append, detection/frame projection, conversion, expressions, serialization, one-shot export, and conversion tools. Adapt to typed snapshots; preserve null versus known-zero duration and reuse canonical Core services.
- [x] 6.3 Preserve existing facade signatures and numeric DTO shapes. Verify callers can still mutate FPS/seconds fields; document the precision/provenance downgrade when using legacy numeric round-trips.
- [x] 6.4 Update handwritten TypeScript types/wrappers and generate declarations through the package build. Add type and runtime coverage for exact inputs, outputs, nullable ends, Unknown, and malformed payloads.
- [x] 6.5 Run packaged Node round-trips for exact/approximate import, edits, conversion, and output. Verify ticks, rate kind, reduction, invalid/default rates, scalar mixed-rate conflicts, and diagnostic behavior.

## 7. CLI boundary

- [x] 7.1 Add mutually exclusive `--frame-rate-ratio N/D` through DotMake.CommandLine and migrate the internal request model directly. Validate syntax and positive Int64 components before import.
- [x] 7.2 Normalize old numeric options to Approximate and use imported canonical rate when neither override is supplied. Apply explicit overrides once before FPS-dependent expression/export.
- [x] 7.3 Keep terminal inspection identifiers and numeric FPS projection; add canonical kind/value labels. Preserve stdout content-only behavior and validation exit code 1.
- [x] 7.4 Test real CLI binding for numeric, ratio, reduced ratio, conflict, missing FPS, non-finite/range failures, expression conversion, and imported exact-rate export.

## 8. Verification and documentation

- [x] 8.1 Add cross-layer regressions for MPLS/IFO exact import -> edit -> candidate -> history -> conversion -> export. Include fractional frames, large ticks, both exact/approximate conversion directions, and 500 reversible arithmetic/conversion cycles with loss below one frame.
- [x] 8.2 Add external compiled Core consumers and packed Node consumers for legacy and typed contracts. Verify the NuGet consumer checks in `scripts/ci/verify-nuget.py` cover the retained surface.
- [x] 8.3 Update Core, CLI, and Node API documentation with compatibility lifetime, exact migration examples, wire shapes, Auto policy, mixed-rate limits, Lua limits, and rounding boundaries.
- [x] 8.4 Update relevant code-map ownership, entry points, and primary test references. If locales change, generate Wasm JSON with `uv run --project scripts scripts/axaml-to-json.py` and verify with `--check`.
- [x] 8.5 Run `dotnet test ChapterTool.slnx --no-restore` with test projects in separate sequential processes. Run affected browser, Node package/type/declaration, and packed NuGet checks through the maintained CI entry points.
- [x] 8.6 Run `openspec validate exact-frame-rate-domain --strict` and validate all specs. Before any requested push, run `python scripts/check-ci.py` and verify required remote jobs for the pushed SHA.
