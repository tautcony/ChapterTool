# Blu-ray Parser Review Summary

## Scope

This review compares the `libbluray` MPLS, CLPI, and INDEX parsers with the ChapterTool Core and Infrastructure parsers. It also checks parser fixtures, build targets, and test entry points.

## Findings

## Executed Native Comparison

The Homebrew `libbluray 1.5.0` library was compared with the Core parsers and
the `BdmvImporter`.

- 160/160 BDMV MPLS files matched on all compared fields.
- 244/244 BDMV CLPI files matched on all common fields.
- 18/18 valid independent MPLS samples matched. One malformed sample was
  rejected by Core and accepted as an empty playlist by libbluray.
- Six discs matched in relevant-title mode for playlist set, duration, and
  chapter count.
- The real PiP fixture matched, so the previously reported PiP address-base
  defect is not confirmed.

The remaining items below are static-review hypotheses or untested behavioral
areas. They must not be treated as confirmed parser mismatches until a fixture
or runtime comparison reproduces them.

### P1-P1-1: CLPI timeline hypothesis (not reproduced)

- Location: `src/ChapterTool.Core/Importing/Disc/MplsChapterImporter.cs`, `PlaylistChapters` and `ComputePlayItemStartPts`.
- Trigger: A playlist has a readable CLPI file with a non-zero `PresentationStartTime`.
- Evidence: Core adds `stcStart` to each chapter timestamp and to each cumulative play-item start. `libbluray` uses the STC start packet to find an entry point, but calculates title time as `clip.title_time + mark.time - play_item.in_time`.
- Comparison status: No title-level duration or chapter mismatch was found on
  six discs. Keep this as a targeted regression test rather than a confirmed
  defect.

### P1-P2-1: `0240` version support (not covered by current fixtures)

- Location: `MplsPlaylistFile.cs:28`, `ClpiFile.cs:25`, and `IndexFile.cs:18`.
- Trigger: A valid MPLS, CLPI, or INDEX header uses version `0240`.
- Evidence: `libbluray/src/libbluray/bdnav/bdmv_parse.h:30` defines `BDMV_VERSION_0240`, and `bdmv_parse.c:60-63` accepts it. Core accepts only `0100`, `0200`, and `0300`.
- Comparison status: All tested files use version `0200`. No `0240` fixture was
  available, so this remains an unverified compatibility gap.

### P1-P2-2: INDEX AppInfo flag layout (not covered by current comparison)

- Location: `src/ChapterTool.Core/Importing/Disc/Index/IndexAppInfoBDMV.cs:17-20`.
- Trigger: `index.bdmv` contains non-zero AppInfo flags.
- Evidence: `libbluray` skips one reserved bit, then reads output mode at bit 6, content at bit 5, one reserved bit, and dynamic range in bits 3..0. Core reads output from bit 7, content from bit 6, and dynamic range from bits 5..2.
- Comparison status: The native comparison did not exercise non-zero INDEX
  AppInfo flags. Keep the bit-layout test as a separate fixture task.

### P1-P2-3: BDMV BACKUP fallback (not exercised)

- Location: `BdmvPathHelper.cs:44-47` and `BdmvImporter.cs:39,67`.
- Trigger: The primary `BDMV/PLAYLIST`, `BDMV/CLIPINF`, or `BDMV/index.bdmv` path is absent or unusable while the corresponding `BDMV/BACKUP` path is usable.
- Evidence: `libbluray` retries `BDMV/BACKUP/PLAYLIST` and `BDMV/BACKUP/index.bdmv` after the primary path. Core constructs only primary paths and scans only the primary playlist directory.
- Comparison status: The six complete discs did not require fallback. A damaged
  primary and backup-only fixture is still required before implementation.

### P1-P2-4: MPLS extension entries (partly covered)

- Location: `MplsExtensionData.cs:15-48`.
- Trigger: An MPLS contains extension data.
- Evidence: `libbluray` validates each entry range and dispatches known extension types for PiP metadata, extension SubPath records, and static metadata. Core stores one aggregate data block and does not validate or parse each `MplsExtDataEntry`. Its data-block seek is relative to the stream position after the length field, while libbluray resolves entry addresses from the extension section start.
- Comparison status: All 160 BDMV MPLS files had zero extension entries. The
  real Terminator2 PiP sample matched exactly, including nested records. Keep
  malformed-range and additional extension fixtures as future coverage, but do
  not report the earlier PiP address-base claim as a confirmed bug.

### P1-INFO-1: Non-chapter CLPI metadata (partly covered)

- Location: `ClpiClipInfo.cs`, `ClpiStreamCodingInfo.cs`, and `ClpiFile.cs`.
- Observation: `libbluray` parses ATC delta records, subtitle font records, video coding type `0x20`, ISRC values, and CLPI extension data. Core skips or does not expose these fields.
- Comparison status: The tested BDMV set contained no native extent points,
  ProgramInfo SS, or CPI SS records. The typed metadata scope remains a product
  decision, not a demonstrated chapter-import difference.

## Test Review

`libbluray` exposes `bd_read_mpls` and `bd_read_clpi` as testing/debugging APIs. Its Meson files build `mpls_dump` and `clpi_dump` only when `enable_devtools=true`. The checkout contains no automated parser tests, no `test()` declarations, and no parser fixture suite. Default Meson configuration therefore provides compilation, not parser behavior verification.

ChapterTool has a useful Core fixture suite. It passed 227 focused importing tests, but the tests miss the non-zero STC timestamp result, valid `0240` headers, correct INDEX flag bit positions, backup-path fallback, and real extension payloads.

## Residual Risk

Homebrew `libbluray 1.5.0` is installed and matches the repository checkout. The
repository `mpls_dump` and `clpi_dump` sources compile directly against the
Homebrew library without Meson or Ninja. The executed parser and title
comparisons are recorded in [`native-libbluray-parity.md`](./native-libbluray-parity.md).
Remaining risk is limited to malformed-input policy, BACKUP fallback, navigation
execution, INDEX access flags, BDJO behavior, and extension fixtures that are
absent from the current corpus.
