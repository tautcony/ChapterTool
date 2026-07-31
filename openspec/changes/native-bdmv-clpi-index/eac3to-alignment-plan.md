# Native BDMV and eac3to Alignment Plan

## Decision

ChapterTool must parse native Blu-ray navigation data.

The native importer must use `index.bdmv`, `MovieObject.bdmv`, BD-J Object files (`*.bdjo`), and MPLS files to discover playlist candidates. It must not interpret a MovieObject identifier as an MPLS identifier.

eac3to is the reference oracle for discovery order and chapter output. It is not the primary runtime implementation. Standard automated tests must use committed reference manifests and must not require eac3to.

ChapterTool will not execute BD-J JAR files or Xlets. The native importer will parse the accessible-playlist declaration in each BDJO file. It will use a bounded MPLS scan when a BD-J application selects playlists dynamically.

## User-Visible Contract

The following inputs must resolve to the same BDMV layout:

1. A disc root that contains `BDMV`.
2. A `BDMV` directory.
3. The primary `BDMV/index.bdmv` file.

Each returned entry must represent one complete chapter-bearing MPLS playlist. The importer must not create one entry for each PlayItem.

Each entry must contain:

- the MPLS file name;
- the complete playlist duration;
- chapter marks on the cumulative playlist timeline;
- the first primary-video frame rate;
- the referenced clip files from all PlayItems;
- absolute `BDMV/STREAM/<clip>.m2ts` paths;
- the disc title when metadata provides it;
- diagnostics that identify the discovery evidence.

The clip collection must preserve first-use order. It must remove duplicate clip paths after it preserves the first occurrence. It must include declared angle clips.

A playlist without chapter marks must remain available to parity diagnostics. It must not create a `ChapterImportEntry`.

Standalone `.mpls` import is outside this behavior change. It can continue to expose one selectable entry for each PlayItem.

## Current Defects

`NativeBdmvImporter` reads an HDMV object identifier from `index.bdmv` and formats it as `<identifier>.mpls`. The Blu-ray format does not define this mapping. The identifier selects an object in `MovieObject.bdmv`.

The current native importer delegates to standalone MPLS import. This operation splits a playlist into PlayItem entries. BDMV import requires one aggregate entry for the complete playlist.

Direct `index.bdmv` input fails because the current path logic treats the file path as a disc root and appends `BDMV/PLAYLIST` to it.

Real fixture MovieObjects use register operands for `PlayPL`, `PlayPLPI`, and `PlayPLPM`. A command scan that reads only immediate playlist operands is insufficient. The resolver must execute relevant `Set`, `Compare`, and branch instructions.

## Discovery Model

Native discovery must merge two independent evidence sources.

```text
index.bdmv
  |-- HDMV title --> MovieObject ID --> bounded HDMV interpreter --> PlayPL events
  `-- BD-J title --> BDJO name ------> accessible playlist table

BDMV/PLAYLIST/*.mpls --> bounded structural scan and duplicate filtering

navigation evidence + scan evidence --> parity discovery policy
                                     --> aggregate MPLS projection
                                     --> chapter-bearing entries
```

MovieObject navigation and playlist scanning are not the same operation. libbluray `bd_get_titles()` calls `nav_get_title_list()`, which scans the playlist directory. It does not execute MovieObject navigation. ChapterTool must keep navigation evidence and scan evidence separate before it merges them.

Use this evidence priority:

1. A playlist emitted by an HDMV `PlayPL`, `PlayPLPI`, or `PlayPLPM` command.
2. A BDJO autostart playlist or an explicitly accessible BDJO playlist.
3. A structurally valid playlist from the bounded MPLS scan.

The merge must remove duplicate playlist identities. It must retain all evidence for diagnostics.

Do not guess the final title comparator. Derive and lock the comparator with the committed eac3to manifests. The implementation must document the resulting deterministic comparison keys in code and tests.

## MovieObject Parser

Add a pure managed parser under `src/ChapterTool.Core/Importing/Disc/MovieObject/`.

The parser must:

- validate the `MOBJ` type indicator and version;
- read the extension-data start address from the common BDMV header;
- seek to byte 40 for the MovieObject section;
- read the section length, reserved field, and object count;
- read the resume-intention, menu-call-mask, and title-search-mask flags;
- read the command count for each object;
- read each command as exactly 12 bytes;
- preserve the raw instruction fields and the two 32-bit operands;
- reject invalid addresses, lengths, counts, and truncated data;
- apply explicit limits for file size, object count, commands per object, and total commands;
- try `BDMV/BACKUP/MovieObject.bdmv` only when the primary file is absent or unusable under the defined fallback policy.

The 12-byte command decoder must read these fields in order:

| Field | Bits |
| --- | ---: |
| operand count | 3 |
| instruction group | 2 |
| instruction subgroup | 3 |
| operand 1 immediate flag | 1 |
| operand 2 immediate flag | 1 |
| reserved | 2 |
| branch option | 4 |
| reserved | 4 |
| compare option | 4 |
| reserved | 3 |
| set option | 5 |
| destination operand | 32 |
| source operand | 32 |

The parser must represent an INDEX HDMV reference as a typed MovieObject identifier. It must not keep the current string-only representation that permits accidental conversion to an MPLS name.

## Bounded HDMV Resolver

Add a pure managed navigation resolver under `src/ChapterTool.Core/Importing/Disc/MovieObject/`.

The first implementation must implement the instruction behavior needed to resolve playlist events:

- Branch group: `Nop`, `Goto`, `Break`, `JumpObject`, `JumpTitle`, `CallObject`, `CallTitle`, and `Resume`.
- Play subgroup: `PlayPL`, `PlayPLPI`, `PlayPLPM`, `TerminatePL`, `LinkPI`, and `LinkMK`.
- Compare group: `BC`, `EQ`, `NE`, `GE`, `GT`, `LE`, and `LT`.
- Set group: `Move`, `Swap`, `Add`, `Sub`, `Mul`, `Div`, `Mod`, `Rnd`, `And`, `Or`, `Xor`, `BitSet`, `BitClear`, `ShiftLeft`, and `ShiftRight`.
- Set-system operations that can affect later navigation operands or branch conditions.

The resolver must implement immediate and register operands. A register operand with bit 31 set selects a Player Status Register (PSR). Other register operands select a General Purpose Register (GPR). Normal Set operations must not write to a PSR.

The resolver must use the libbluray arithmetic behavior as the compatibility reference. This includes saturated addition and multiplication, clamped subtraction, and the defined divide-by-zero and modulo-by-zero result. Random behavior must be deterministic in tests. A bounded outcome fork is also acceptable.

The resolver must emit structured playlist events. Each event must contain the playlist identifier, optional PlayItem or mark identifier, source title, source object, program counter, player profile, and instruction type.

The resolver must never execute without limits. Define limits for:

- executed instructions;
- object transitions;
- call depth;
- emitted events;
- player-profile variants;
- visited states.

A visited-state key must include the object identifier, program counter, call state, relevant GPR values, and relevant PSR values. A limit failure must return a diagnostic. It must not hang or silently produce partial success.

Use a deterministic default player profile that matches documented libbluray PSR defaults. Record every default in one testable type. At minimum, define title, chapter, playlist, PlayItem, playback time, audio, subtitle, angle, region, language, output mode, and player-profile values.

Some discs branch on region, language, output, or player-profile values. The resolver may create bounded profile variants only for PSRs that the current navigation program reads. It must merge emitted playlists in stable order. It must state which variants it evaluated in diagnostics.

## BDJO Parser and BD-J Policy

Add a pure managed BDJO parser under `src/ChapterTool.Core/Importing/Disc/Bdjo/`.

The parser must resolve a typed INDEX BD-J reference to `BDMV/BDJO/<name>.bdjo`. It must use `BDMV/BACKUP/BDJO/<name>.bdjo` under the defined backup policy.

Parse the accessible-playlist section:

- the 11-bit playlist count;
- `access_to_all_flag`;
- `autostart_first_playlist_flag`;
- each five-character playlist name;
- the reserved byte after each name.

An explicit list supplies navigation evidence. The first entry has stronger evidence when the autostart flag is set. `access_to_all_flag` permits the bounded playlist scan to supply all valid playlists.

ChapterTool will not load or execute BD-J JAR files. A BD-J title can select a playlist dynamically in application code. When the BDJO declaration cannot identify the final playlist, the importer must emit an `UnsupportedDynamicBdJNavigation` diagnostic and use the playlist scan as fallback evidence. The diagnostic must make the limitation visible.

## Playlist Scan and Aggregate Projection

Add a bounded playlist scanner in Infrastructure. Use libbluray `navigation.c` as the behavioral reference.

The scanner must:

- enumerate primary or backup `PLAYLIST/*.mpls` files in stable name order;
- reject malformed playlists with a structured diagnostic;
- detect structurally duplicate playlists by PlayItems, marks, and stream declarations;
- detect excessive repeated identical segments;
- apply an explicit minimum-duration policy;
- retain the original MPLS name for every candidate;
- retain no-chapter candidates for parity diagnostics.

Do not assume that a navigation-reachable playlist set equals the eac3to title list. The parity discovery policy must merge both sources.

Add a Core aggregate MPLS projection. It must parse one MPLS once and return the complete duration, cumulative chapter marks, video frame rate, ordered clip references, angle clips, and source-display fallback.

`NativeBdmvImporter` must use this aggregate projection. `MplsChapterImporter.ImportAsync` must retain its standalone behavior.

## Input Layout and Runtime Routing

Add an Infrastructure-owned `BdmvSourceLayout` resolver.

The resolver must return:

- the original input path;
- the disc root;
- the `BDMV` directory;
- primary and backup INDEX paths;
- primary and backup MovieObject paths;
- primary and backup BDJO directories;
- primary and backup playlist directories;
- the CLIPINF directory;
- the STREAM directory;
- the metadata directory.

The resolver must accept only `index.bdmv` as a direct `.bdmv` entry point. It must reject `MovieObject.bdmv` and arbitrary `.bdmv` files as top-level user input.

Route the three accepted input forms to `NativeBdmvImporter`. Keep the eac3to importer available as an explicit verifier and as a diagnosed fallback for unsupported dynamic BD-J navigation. Do not silently use eac3to for malformed native input.

## eac3to Reference Evidence

Use this repository fixture root for standard parity manifests and parser tests:

```text
tests/ChapterTool.Core.Tests/Fixtures/Importing/Disc/Bdmv
```

Use this local compatibility-reference source tree:

```text
libbluray
```

Use this executable for opt-in parity capture:

```text
C:\Tools\eac3to\eac3to.exe
```

Use this full disc for manual and opt-in integration verification:

```text
D:\Downloads\[BDMV][アニメ][131213] 劇場版 STEINS;GATE 負荷領域のデジャヴ\BDISO
```

The full disc produces this eac3to title list:

Use this PowerShell command pattern. Pass each native argument as a separate array item.

```powershell
$exe = 'C:\Tools\eac3to\eac3to.exe'
$source = 'D:\Downloads\[BDMV][アニメ][131213] 劇場版 STEINS;GATE 負荷領域のデジャヴ\BDISO'
$arguments = @($source, '-showall')
& $exe @arguments
```

| eac3to title | MPLS | Clips | Duration | Chapters |
| --- | --- | --- | --- | ---: |
| 1 | `00000.mpls` | `00000.m2ts` | `1:30:02` | 14 |
| 2 | `00001.mpls` | `00001.m2ts`, `00002.m2ts` | `0:31:51` | 6 |
| 3 | `00005.mpls` | repeated `00008.m2ts` PlayItems | `0:28:02` | 0 |
| 4 | `00002.mpls` | `00003.m2ts` | `0:11:59` | 2 |

ChapterTool must return three entries. It must omit `00005.mpls` from imported entries because the playlist has no chapters. The parity manifest must retain `00005.mpls`.

The required chapter times are:

| MPLS | Chapter times |
| --- | --- |
| `00000.mpls` | `00:00:00.000`, `00:09:43.416`, `00:14:10.349`, `00:17:26.712`, `00:27:30.858`, `00:33:28.506`, `00:42:00.518`, `00:46:29.996`, `00:51:35.342`, `01:04:31.326`, `01:13:59.560`, `01:24:27.354`, `01:26:36.691`, `01:30:01.396` |
| `00001.mpls` | `00:00:00.000`, `00:06:02.362`, `00:08:22.001`, `00:13:41.554`, `00:15:55.488`, `00:31:50.108` |
| `00002.mpls` | `00:00:00.000`, `00:11:57.717` |

The repository fixtures must also have committed eac3to manifests:

| Fixture disc | MPLS files | eac3to titles | Chapter-bearing titles |
| --- | ---: | --- | --- |
| `Detective Conan Zero the Enforcer` | 9 | `00001` | `00001` with 18 chapters |
| `Detective Conan The Bride of Halloween/DISC1` | 4 | `00001` | `00001` with 20 chapters |
| `Detective Conan The Bride of Halloween/DISC2` | 17 | `00001`, `00002`, `00003` | 12, 7, and 2 chapters |
| `KIMETSU_NO_YAIBA_MUGENJO_HEN_P1_DISC1` | 4 | `00020`, `00000` | `00000` with 18 chapters |
| `MAYONAKA_PUNCH/MAYONAKA_PUNCH_DISC1` | 28 | `00001`, `01001` | `00001` with 35 chapters |
| `MAYONAKA_PUNCH/MAYONAKA_PUNCH_DISC2` | 18 | `00001`, `00015`, `00014`, `01001` | `00001`, `00015`, and `00014` with 33, 3, and 6 chapters |

## libbluray Reference Map

The repository contains libbluray source under `libbluray/`. Use these files as behavioral and format references:

| Concern | Reference file |
| --- | --- |
| MovieObject data structures | `libbluray/src/libbluray/hdmv/mobj_data.h` |
| MovieObject binary layout | `libbluray/src/libbluray/hdmv/mobj_parse.c` |
| HDMV instruction constants | `libbluray/src/libbluray/hdmv/hdmv_insn.h` |
| HDMV execution behavior | `libbluray/src/libbluray/hdmv/hdmv_vm.c` |
| INDEX HDMV and BD-J reference types | `libbluray/src/libbluray/bdnav/index_data.h` |
| BDJO structures | `libbluray/src/libbluray/bdj/bdjo_data.h` |
| BDJO binary layout | `libbluray/src/libbluray/bdj/bdjo_parse.c` |
| Playlist scan and duplicate policy | `libbluray/src/libbluray/bdnav/navigation.c` |
| PSR defaults and register behavior | `libbluray/src/libbluray/register.c` |

libbluray is licensed under GNU Lesser General Public License 2.1. Read `libbluray/COPYING` before implementation. Use the source as a compatibility reference. Reimplement the behavior in C#. Do not copy code verbatim without a license review. Record source-file provenance in implementation comments only where it is necessary to explain a non-obvious binary rule.

## Implementation Sequence

1. Commit eac3to reference manifests for all repository fixtures.
2. Add exact parity tests that fail against the current implementation.
3. Add `BdmvSourceLayout` and input-shape equivalence tests.
4. Replace the string-only INDEX object reference with typed HDMV and BD-J references.
5. Add the bounded MovieObject parser and parser tests.
6. Add the bounded HDMV resolver and instruction tests.
7. Add the BDJO parser and BD-J policy tests.
8. Add the libbluray-style playlist scanner and duplicate tests.
9. Add the aggregate MPLS projection and clip-collection tests.
10. Merge navigation and scan evidence through one parity discovery policy.
11. Route BDMV inputs to the corrected native importer.
12. Run fixture parity, full-disc parity, focused tests, and full solution tests.
13. Update `docs/code-map/core.md` and `docs/code-map/infrastructure.md` after implementation changes ownership or entry points.

## Required Tests

Core tests must cover:

- every implemented instruction group and option;
- immediate and register operands;
- GPR and PSR reads;
- rejected PSR writes;
- compare true and false control flow;
- `Goto`, object jump, object call, title jump, title call, and resume;
- `PlayPL`, `PlayPLPI`, and `PlayPLPM` event fields;
- arithmetic edge cases;
- deterministic random behavior;
- instruction, state, transition, event, and call-depth limits;
- malformed and truncated MovieObject data;
- BDJO explicit playlists, access-to-all, and autostart;
- primary and backup file selection;
- aggregate playlist chapters, duration, frame rate, clips, and angles.

Infrastructure tests must cover:

- equivalent results for a disc root, `BDMV` directory, and `index.bdmv`;
- navigation and scan evidence merging;
- structural duplicate filtering;
- repeated-segment filtering;
- deterministic title order;
- missing or corrupt INDEX, MovieObject, BDJO, and MPLS files;
- unsupported dynamic BD-J diagnostics;
- no-chapter candidate retention and entry omission;
- exact fixture manifest parity;
- exact full-disc values in an opt-in test.

The standard test run must not depend on `C:\Tools\eac3to` or the external full disc. An opt-in parity tool may regenerate manifests and compare live eac3to output.

## Acceptance Criteria

- The three accepted input shapes return equivalent entries.
- The importer never converts a MovieObject identifier directly to an MPLS name.
- HDMV navigation resolves register-based playlist operands.
- BDJO declarations contribute playlist evidence without JAR execution.
- The native candidate list matches the committed eac3to manifests.
- Entry order matches the committed eac3to manifests.
- One entry represents one complete chapter-bearing playlist.
- Chapter timestamps match the committed eac3to reference at millisecond precision.
- Clip collections match MPLS PlayItems and angle declarations.
- Unsupported dynamic BD-J selection produces a diagnostic and bounded fallback behavior.
- All parsers and interpreters reject unbounded work.
- Standalone `.mpls` behavior does not regress.

## Known Limitation

BD-J JAR execution is outside this change. A disc can compute a playlist selection inside an Xlet. BDJO declarations and playlist scanning cannot reproduce every such runtime decision. The importer must state this limitation through diagnostics. eac3to can remain an explicit fallback for this case when it is configured.
