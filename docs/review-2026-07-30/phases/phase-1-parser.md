# Phase 1 Review: Core Blu-ray Parsers

> Date: 2026-07-30
> Files: Core MPLS, CLPI, INDEX, BDMV path, and native BDMV importer files.
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
- `src/ChapterTool.Infrastructure/Importing/Bdmv/NativeBdmvImporter.cs`

## Findings

### P1-P1-1: CLPI presentation time changes the user timeline

See [`summary.md`](../summary.md#p1-p1-1-core-shifts-the-playlist-timeline-when-clpi-is-present). Core adds `PresentationStartTime` to chapter and play-item title time. libbluray does not add it to title time.

### P1-P2-1: `0240` is missing from Core version checks

See [`summary.md`](../summary.md#p1-p2-1-core-rejects-the-valid-0240-bdmv-version). libbluray accepts `0100`, `0200`, `0240`, and `0300`.

### P1-P2-2: INDEX AppInfo flags use shifted bit positions

See [`summary.md`](../summary.md#p1-p2-2-core-reads-index-appinfo-flags-at-the-wrong-bit-positions). The current test builder repeats the same shifted layout.

### P1-P2-3: BACKUP lookup is missing

See [`summary.md`](../summary.md#p1-p2-3-core-does-not-use-the-bdmv-backup-parser-paths). libbluray retries backup playlist and index paths.

### P1-P2-4: MPLS extension payloads are not equivalent

See [`summary.md`](../summary.md#p1-p2-4-core-does-not-parse-mpls-extension-entries-like-libbluray). Core stores raw data and does not validate per-entry ranges.

### P1-INFO-1: CLPI metadata is intentionally incomplete or undocumented

See [`summary.md`](../summary.md#p1-info-1-core-omits-non-chapter-clpi-metadata). The omission does not currently change chapter extraction.

## Missed-Risk Review

- Unknown values and reserved bits: checked. Core generally skips unknown payloads within bounded containers.
- Length and count boundaries: checked. Core has stronger explicit limits than libbluray in several paths.
- Timestamp base and STC context: one confirmed error found.
- Backup paths and extension data: gaps found.
- Async or resource paths: no new parser-specific issue found. Core import streams use disposal on the main import path.

## Uncovered Areas

- No real extension-data fixture was available in the Core test set.
- Native libbluray execution was not available in this environment.
