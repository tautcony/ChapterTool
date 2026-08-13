# Native libbluray Parity Method

Use this method to compare Homebrew libbluray with the managed BDMV implementation.

## Installed Baseline

The verified environment contains Homebrew `libbluray 1.5.0` at `/opt/homebrew/opt/libbluray`.
The repository checkout is also `libbluray 1.5.0` at commit `d75c88e5`.

Homebrew installs these relevant files:

- `bd_info`
- `bd_list_titles`
- `bd_splice`
- `libbluray.dylib`
- public headers, including `bluray.h` and `clpi_data.h`

Homebrew does not install `mpls_dump` or `clpi_dump`.
Build these tools from the matching repository source and link them to the Homebrew library.

## Version Check

Run these commands before each native comparison:

```sh
brew list --versions libbluray
git -C libbluray describe --tags --always --dirty
```

Both commands must report `1.5.0`.
Do not combine private structure headers from one version with a different library version.

## Level 1: Disc Title Comparison

Use `bd_list_titles` for the first comparison.
This tool exercises libbluray disc discovery, playlist filtering, CLPI lookup, and BACKUP fallback.

```sh
libbluray_prefix=$(brew --prefix libbluray)
disc_root="tests/ChapterTool.Core.Tests/Fixtures/Importing/Disc/Bdmv/Detective Conan The Bride of Halloween/DISC2"
"$libbluray_prefix/bin/bd_list_titles" "$disc_root"
```

Use the default relevant-title mode for parity with `BdmvImporter`.
Use `-a` only as a policy diagnostic. It includes titles that Core intentionally
filters, such as no-chapter playlists and repeated-segment candidates.

Compare these fields with `BdmvImporter` output:

- playlist ID and order
- main title
- duration
- chapter count
- angle count
- clip count
- video, audio, subtitle, and Dolby Vision stream counts

Use `-l` to include language codes.
Use `-c` to include optional chapter names.

## Level 2: Build Parser Dump Tools

The following commands do not require Meson, Ninja, or `pkg-config`.
They compile the matching libbluray devtool sources with the system C compiler.

```sh
libbluray_prefix=$(brew --prefix libbluray)
parity_tools=$(mktemp -d /tmp/chaptertool-libbluray-parity.XXXXXX)

cc -std=c11 \
  -I"$libbluray_prefix/include/libbluray" \
  -Ilibbluray/src \
  -Ilibbluray/src/libbluray \
  -Ilibbluray/src/devtools \
  libbluray/src/devtools/mpls_dump.c \
  libbluray/src/devtools/util.c \
  -L"$libbluray_prefix/lib" \
  -Wl,-rpath,"$libbluray_prefix/lib" \
  -lbluray \
  -o "$parity_tools/mpls_dump"

cc -std=c11 \
  -I"$libbluray_prefix/include/libbluray" \
  -Ilibbluray/src \
  -Ilibbluray/src/libbluray \
  -Ilibbluray/src/devtools \
  libbluray/src/devtools/clpi_dump.c \
  libbluray/src/devtools/util.c \
  -L"$libbluray_prefix/lib" \
  -Wl,-rpath,"$libbluray_prefix/lib" \
  -lbluray \
  -o "$parity_tools/clpi_dump"
```

These commands were verified with the repository checkout and Homebrew `libbluray 1.5.0`.

## Level 3: MPLS Field Comparison

Run the MPLS dump with all parser-relevant sections:

```sh
mpls_file="tests/ChapterTool.Core.Tests/Fixtures/Importing/Disc/Mpls/00020_Terminator2.mpls"
"$parity_tools/mpls_dump" -v -l -i -c -p -P -S "$mpls_file"
```

Compare these values with `MplsPlaylistFile`:

- app information and playback type
- play items, clips, angles, STC IDs, IN times, and OUT times
- stream entries and attributes
- subpaths and sub-play-items
- playlist marks
- PiP metadata
- extension subpaths
- static metadata

`mpls_dump` reports raw MPLS timestamps in 45 kHz units.
Core MPLS records also use 45 kHz units.

## Level 4: CLPI Field Comparison

Run the CLPI dump with every available section:

```sh
clpi_file="<path-to-clpi-file>"
"$parity_tools/clpi_dump" -v -c -s -p -i -e "$clpi_file"
```

Compare these values with `ClpiFile`:

- ClipInfo fields
- ATC and STC sequences
- ProgramInfo streams and coding attributes
- CPI coarse and fine entries
- extent start points
- ProgramInfo SS and CPI SS when present

## Time Normalization

Use the correct time base for each API:

- Raw MPLS fields and `mpls_dump` use 45 kHz units.
- `BLURAY_TITLE_INFO` from `bd_get_title_info()` and `bd_get_playlist_info()` uses 90 kHz units.
- Core chapter `TimeSpan` values use managed ticks after conversion from 45 kHz MPLS values.

For title-level comparison, convert libbluray 90 kHz values to Core time:

```text
seconds = libbluray_value / 90000
```

For raw MPLS comparison, compare the 45 kHz integer values directly.

## Required Fixture Matrix

Run all comparison levels for these cases:

- one normal single-play-item playlist
- one multi-play-item playlist
- one multi-angle playlist
- one playlist with PiP or static metadata extensions
- one version `0240` playlist and CLPI file
- one disc with primary files only
- one disc with BACKUP files only
- one disc with a damaged primary file and a valid BACKUP file
- one INDEX with interleaved HDMV and BD-J titles
- one HDMV program that uses conditional branches and title calls

Store captured output under `artifacts/libbluray-parity/` when the comparison is part of a change verification.

## Acceptance Rule

A comparison passes only when all chapter-relevant values match after time-base normalization.
Document intentional metadata differences separately.
Do not treat successful parsing alone as parity evidence.

## Executed Comparison Results

The following comparisons used Homebrew `libbluray 1.5.0` and the matching
repository checkout (`d75c88e5`). The temporary harness and captured JSONL/text
outputs are under `artifacts/libbluray-parity/`.

### Parser records

- Core and libbluray parsed all 160 BDMV MPLS files.
- All 160 BDMV MPLS records matched on version, play items, timing, angles,
  stream counts, subpaths, marks, and extension counts.
- Core and libbluray parsed all 244 BDMV CLPI files.
- All 244 BDMV CLPI records matched on common fields, including ClipInfo,
  ATC/STC sequences, ProgramInfo, and CPI entries.
- No tested BDMV CLPI file contained native extent points, ProgramInfo SS, or
  CPI SS records. No tested BDMV MPLS file contained extension records.

The independent MPLS sample set produced one intentional strictness difference:
Core rejected `00001_Invalid.mpls`, while libbluray accepted it as an empty
playlist after reporting an unexpected end of AppInfo. The other 18 files
matched exactly. The real PiP fixture `00020_Terminator2.mpls` matched,
including 29 PiP records and 98 PiP data records. The earlier suspected PiP
`data_address` mismatch is therefore not reproduced.

### Disc titles

The default relevant-title output matched Core for all six BDMV discs:

| Disc | libbluray titles | Core titles | Duration and chapters |
|---|---:|---:|---|
| Detective Conan Zero the Enforcer | 9 | 9 | All common durations within 1 second; chapter counts match |
| KIMETSU NO YAIBA MUGENJO HEN P1 DISC1 | 3 | 3 | Same |
| Detective Conan The Bride of Halloween DISC1 | 4 | 4 | Same |
| Detective Conan The Bride of Halloween DISC2 | 17 | 17 | Same |
| MAYONAKA PUNCH DISC1 | 27 | 27 | Same |
| MAYONAKA PUNCH DISC2 | 17 | 17 | Same |

`bd_list_titles -a` exposes an intentional title-policy difference. It includes
`00020.mpls` in the Kimetsu disc and `01001.mpls` in the Mayonaka discs, while
Core excludes those candidates through relevant-title filtering. This is not a
parser mismatch.

## Confirmed Differences

- Core is stricter than libbluray for the malformed `00001_Invalid.mpls` sample.
- All-title mode and Core relevant-title mode have different, intentional
  filtering policies.

## Not Reproduced By This Comparison

The current evidence does not confirm an MPLS PiP address-base bug, generic
MPLS/CLPI field mismatch, or title duration/chapter mismatch. The following
scenarios still need dedicated fixtures or behavioral tests:

- primary-file damage and per-file `BDMV/BACKUP` fallback;
- HDMV navigation execution and conditional branches;
- BDJO dynamic navigation;
- INDEX access-control and hidden-title behavior;
- non-empty MPLS/CLPI extension fixtures outside the tested BDMV set.
