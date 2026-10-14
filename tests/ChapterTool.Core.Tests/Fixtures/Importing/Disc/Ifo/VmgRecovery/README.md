# VMG recovery fixtures

These fixtures contain only IFO navigation metadata extracted from two DVD images. They contain no video or audio streams.

| Fixture | IFO files | Expected import |
| --- | --- | --- |
| `Srcl8925` | `VIDEO_TS.IFO`, `VTS_01_0.IFO` | One title with 3 chapters. |
| `TheresaDisc4` | `VIDEO_TS.IFO`, `VTS_01_0.IFO`, `VTS_02_0.IFO` | Three titles with 19, 2, and 1 chapters. |
| `DarlingFranxx` | `VIDEO_TS.IFO`, `VTS_01_0.IFO` | Three titles with 2 chapters each. FFmpeg reports starts at 0/104, 0/96, and 0/92 seconds. |

The IFO importer tests use these fixtures to check VMG title mapping, PTT/PGC chapter recovery, chapter counts, and known chapter start times. The test does not need FFmpeg or the original DVD images.

## Regression checklist

- [x] VMG title entries resolve to their referenced VTS IFO files.
- [x] SRCL-8925 imports all 3 chapter starts.
- [x] Theresa Disc 4 imports all 3 titles and all 19 starts for title 1.
- [x] DARLING in the FRANXX imports all 3 titles when invalid PTT references can be recovered from one-to-one ordinal PGCs.
- [x] Fixture directories contain only IFO files. Do not add VOB, BUP, or audio files.
