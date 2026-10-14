## Context

See proposal.md. The canonical Core and Node exact contracts already carry rate kind, rational components, and timestamp ticks. The current compatibility paths duplicate those values through numeric fields and option projections.

## Goals / Non-Goals

**Goals:** Remove the selected compatibility contracts while preserving current editing, conversion, export, selector, and settings behavior through canonical APIs.

**Non-Goals:** Change chapter file formats, CLI option syntax, current selector ordering, current browser settings key, or the 23.x package version line.

## Decisions

- Keep `ChapterSet` as the current importer/service model but remove its numeric FPS and exact-rate alias projections. Keep its canonical `FrameRate` property.
- Route public Node workflows through the exact tick-string/rate-kind API. Remove the old numeric Node wrapper and its DTOs.
- Replace transform-bearing export options with explicit candidate operations and pure serialization. Keep CLI conversion behavior by applying its selected operation before serialization.
- Rename `LegacyMplsCode` to `MplsFrameRateCode`; preserve integer values, catalog order, custom-rate sentinel, and reserved-rate handling.
- Remove old expression UI no-ops and bind the XAML converter to its concrete implementation.
- Remove predecessor localStorage key reads and cleanup. Do not migrate old browser preferences implicitly.
- Keep the current 23.x version line as directed; update all compatibility-window documentation to describe the removal in this release.

## Risks / Trade-offs

- [External Core and Node consumers can fail to compile or lose old settings] → Update migration docs and release notes to name exact replacements.
- [CLI or UI may accidentally replay a transform or change selector behavior] → Route every operation through the existing candidate model and preserve current option identities.
- [Older browser settings are ignored] → Keep the current key path unchanged and cover its load behavior in existing workflow checks.

## Migration Plan

Remove old signatures and DTOs after migrating in-repository callers. Update TypeScript declarations and package examples from the generated source. Keep `CHANGELOG.md` and `CHANGELOG.zh-CN.md` entries under Unreleased until release.
