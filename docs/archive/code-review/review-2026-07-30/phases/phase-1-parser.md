# Phase 1 Review: Core Blu-ray Parsers

> Date: 2026-07-30
> Files: Core MPLS, CLPI, INDEX, BDMV path, and BDMV importer files.
> Findings: P1(1) / P2(4) / INFO(1)
> Navigation: [Back to review index](../index.md) | [Fix checklist](../fix-checklist.md)

## Reviewed Files

- `src/ChapterTool.Core/Importing/Disc/MplsChapterImporter.cs`
- `src/ChapterTool.Core/Importing/Disc/MplsPlaylistFile.cs`
- `src/ChapterTool.Core/Importing/Disc/MplsExtensionData.cs`
- `src/ChapterTool.Core/Importing/Disc/Clpi/ClpiFile.cs`
- `src/ChapterTool.Core/Importing/Disc/Clpi/ClpiClipInfo.cs`
- `src/ChapterTool.Core/Importing/Disc/Clpi/ClpiStreamCodingInfo.cs`
- `src/ChapterTool.Core/Importing/Disc/Index/IndexAppInfoBDMV.cs`
- `src/ChapterTool.Core/Importing/Disc/Index/IndexFile.cs`
- `src/ChapterTool.Core/Importing/Disc/BdmvPathHelper.cs`
- `src/ChapterTool.Infrastructure/Importing/Bdmv/BdmvImporter.cs`

## Findings

### P1-P1-1: CLPI presentation time changes the user timeline (not reproduced)

See [`summary.md`](../summary.md#p1-p1-1-clpi-timeline-hypothesis-not-reproduced). The static review raised this concern, but the six-disc native comparison found no duration or chapter mismatch. Keep exact non-zero-STC assertions as regression coverage.

### P1-P2-1: `0240` support is not covered by current fixtures

See [`summary.md`](../summary.md#p1-p2-1-0240-version-support-not-covered-by-current-fixtures). All compared files use version `0200`; add a `0240` fixture before treating this as a confirmed gap.

### P1-P2-2: INDEX AppInfo flags are not covered by native comparison

See [`summary.md`](../summary.md#p1-p2-2-index-appinfo-flag-layout-not-covered-by-current-comparison). The native comparison did not exercise non-zero flags.

### P1-P2-3: BACKUP lookup is not exercised

See [`summary.md`](../summary.md#p1-p2-3-bdmv-backup-fallback-not-exercised). The complete discs did not require fallback.

### P1-P2-4: MPLS extension payloads are partly covered

See [`summary.md`](../summary.md#p1-p2-4-mpls-extension-entries-partly-covered). The tested BDMV set has no extension entries, and the real PiP sample matches.

### P1-INFO-1: CLPI metadata is intentionally incomplete or undocumented

See [`summary.md`](../summary.md#p1-info-1-core-omits-non-chapter-clpi-metadata). The omission does not currently change chapter extraction.

## Missed-Risk Review

- Unknown values and reserved bits: checked. Core generally skips unknown payloads within bounded containers.
- Length and count boundaries: checked. Core has stronger explicit limits than libbluray in several paths.
- Timestamp base and STC context: no mismatch reproduced in native comparison.
- Backup paths and extension data: gaps found.
- Async or resource paths: no new parser-specific issue found. Core import streams use disposal on the main import path.

## Uncovered Areas

- No broad CLPI SS or additional MPLS extension fixture was available.
- BACKUP, HDMV, BDJO, and INDEX access-control behavior still needs runtime comparison.
