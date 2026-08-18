# Blu-ray Parser Review Index

> Date: 2026-07-30
> Scope: `libbluray` and the ChapterTool Core and Infrastructure Blu-ray file parsers.
> Conclusion: Native comparison confirms parity for the tested MPLS, CLPI, and relevant-title fixtures. Remaining differences are malformed-input strictness, title-filter policy, and untested fallback/navigation/metadata scenarios.

## Start Here

1. Read [`summary.md`](./summary.md) for the findings and impact.
2. Record implementation choices in [`implementation-decisions.md`](./implementation-decisions.md).
3. Use [`native-libbluray-parity.md`](./native-libbluray-parity.md) for native comparison commands.
4. Read [`fix-checklist.md`](./fix-checklist.md) to start remediation.
5. Read [`phases/phase-1-parser.md`](./phases/phase-1-parser.md) for code evidence.
6. Read [`phases/phase-2-libbluray-testing.md`](./phases/phase-2-libbluray-testing.md) for the libbluray test review.

## Findings

| ID | Level | Status | Topic |
|---|---|---|---|
| P1-P1-1 | P1 | Not reproduced | CLPI presentation-time timeline hypothesis |
| P1-P2-1 | P2 | Uncovered | `0240` version support |
| P1-P2-2 | P2 | Uncovered | `index.bdmv` AppInfo flag layout |
| P1-P2-3 | P2 | Uncovered | Per-file `BACKUP` fallback |
| P1-P2-4 | P2 | Partly covered | MPLS extension entries |
| P1-INFO-1 | INFO | Partly covered | Non-chapter CLPI metadata |
| P2-INFO-1 | INFO | Open | libbluray has no automated parser test target in this checkout |

## Limits

- The libbluray source does not include a test directory or Meson test target.
- Homebrew `libbluray 1.5.0` is installed and matches the repository checkout.
- The repository devtools can compile directly against the Homebrew library.
- The executed native comparison covers 160 MPLS files, 244 CLPI files, and six discs.
- BACKUP fallback, navigation execution, BDJO, INDEX access flags, and additional extension fixtures remain uncovered.
- The focused Core import tests passed.
