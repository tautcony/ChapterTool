# Blu-ray Parser Review Summary

## Scope

This review compares the `libbluray` MPLS, CLPI, and INDEX parsers with the ChapterTool Core and Infrastructure parsers. It also checks parser fixtures, build targets, and test entry points.

## Findings

### P1-P1-1: Core shifts the playlist timeline when CLPI is present

- Location: `src/ChapterTool.Core/Importing/Disc/MplsChapterImporter.cs`, `PlaylistChapters` and `ComputePlayItemStartPts`.
- Trigger: A playlist has a readable CLPI file with a non-zero `PresentationStartTime`.
- Evidence: Core adds `stcStart` to each chapter timestamp and to each cumulative play-item start. `libbluray` uses the STC start packet to find an entry point, but calculates title time as `clip.title_time + mark.time - play_item.in_time`.
- Impact: The first chapter can start after a false offset. Later chapters and the total playlist timeline can also be shifted or extended. The existing `StcAwarePtsTests` only checks that chapters exist, so it does not detect the wrong timestamp.
- Direction: Keep CLPI STC information for packet lookup only. Calculate playlist chapter time from MPLS `INTime`, `OUTTime`, and mark timestamps. Add exact assertions for non-zero STC offsets.

### P1-P2-1: Core rejects the valid `0240` BDMV version

- Location: `MplsPlaylistFile.cs:28`, `ClpiFile.cs:25`, and `IndexFile.cs:18`.
- Trigger: A valid MPLS, CLPI, or INDEX header uses version `0240`.
- Evidence: `libbluray/src/libbluray/bdnav/bdmv_parse.h:30` defines `BDMV_VERSION_0240`, and `bdmv_parse.c:60-63` accepts it. Core accepts only `0100`, `0200`, and `0300`.
- Impact: 3D profile discs can fail import or lose native BDMV discovery.
- Direction: Accept `0240` in all three shared header parsers. Add one fixture or synthetic test for each affected file type.

### P1-P2-2: Core reads INDEX AppInfo flags at the wrong bit positions

- Location: `src/ChapterTool.Core/Importing/Disc/Index/IndexAppInfoBDMV.cs:17-20`.
- Trigger: `index.bdmv` contains non-zero AppInfo flags.
- Evidence: `libbluray` skips one reserved bit, then reads output mode at bit 6, content at bit 5, one reserved bit, and dynamic range in bits 3..0. Core reads output from bit 7, content from bit 6, and dynamic range from bits 5..2.
- Impact: Diagnostic metadata is wrong. Existing tests construct `0xC0` and expect the Core interpretation, so the test protects the wrong wire layout.
- Direction: Read the fields from the same bit positions as `index_parse.c`. Update the builder and add tests for independent output, content, and dynamic-range values.

### P1-P2-3: Core does not use the BDMV BACKUP parser paths

- Location: `BdmvPathHelper.cs:44-47` and `NativeBdmvImporter.cs:39,67`.
- Trigger: The primary `BDMV/PLAYLIST`, `BDMV/CLIPINF`, or `BDMV/index.bdmv` path is absent or unusable while the corresponding `BDMV/BACKUP` path is usable.
- Evidence: `libbluray` retries `BDMV/BACKUP/PLAYLIST` and `BDMV/BACKUP/index.bdmv` after the primary path. Core constructs only primary paths and scans only the primary playlist directory.
- Impact: Core can report no titles or miss CLPI timing data for a disc that libbluray can read.
- Direction: Apply one explicit primary-then-backup resolution policy to INDEX, MPLS, and CLPI files. Add a fixture with only the backup copy.

### P1-P2-4: Core does not parse MPLS extension entries like libbluray

- Location: `MplsExtensionData.cs:15-48`.
- Trigger: An MPLS contains extension data.
- Evidence: `libbluray` validates each entry range and dispatches known extension types for PiP metadata, extension SubPath records, and static metadata. Core stores one aggregate data block and does not validate or parse each `MplsExtDataEntry`. Its data-block seek is relative to the stream position after the length field, while libbluray resolves entry addresses from the extension section start.
- Impact: Extension payloads can be read from the wrong base and their contents are unavailable to callers. Chapter extraction does not currently use these fields, but the parser result is not equivalent.
- Direction: Define the address base explicitly. Validate every entry start and length against the bounded extension section. Parse the required known extension types or expose an intentional raw-extension contract with documented limits.

### P1-INFO-1: Core omits non-chapter CLPI metadata

- Location: `ClpiClipInfo.cs`, `ClpiStreamCodingInfo.cs`, and `ClpiFile.cs`.
- Observation: `libbluray` parses ATC delta records, subtitle font records, video coding type `0x20`, ISRC values, and CLPI extension data. Core skips or does not expose these fields.
- Impact: No current chapter timestamp path depends on these fields. Future media or diagnostic features cannot use the parsed information.
- Direction: Treat this as an intentional scope decision or add typed records and tests before using CLPI as a general parser.

## Test Review

`libbluray` exposes `bd_read_mpls` and `bd_read_clpi` as testing/debugging APIs. Its Meson files build `mpls_dump` and `clpi_dump` only when `enable_devtools=true`. The checkout contains no automated parser tests, no `test()` declarations, and no parser fixture suite. Default Meson configuration therefore provides compilation, not parser behavior verification.

ChapterTool has a useful Core fixture suite. It passed 227 focused importing tests, but the tests miss the non-zero STC timestamp result, valid `0240` headers, correct INDEX flag bit positions, backup-path fallback, and real extension payloads.

## Residual Risk

The review did not run a native libbluray binary comparison because Meson and Ninja are unavailable. A follow-up should build the devtools and compare `mpls_dump -c` and `clpi_dump` output against Core records on the existing fixtures.
