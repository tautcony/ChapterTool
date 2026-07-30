# Blu-ray Parser Review Index

> Date: 2026-07-30
> Scope: `libbluray` and the ChapterTool Core and Infrastructure Blu-ray file parsers.
> Conclusion: Core reads common MPLS files, but it has a confirmed chapter-time error when CLPI data is present and several format-compatibility gaps.

## Start Here

1. Read [`summary.md`](./summary.md) for the findings and impact.
2. Read [`fix-checklist.md`](./fix-checklist.md) to start remediation.
3. Read [`phases/phase-1-parser.md`](./phases/phase-1-parser.md) for code evidence.
4. Read [`phases/phase-2-libbluray-testing.md`](./phases/phase-2-libbluray-testing.md) for the libbluray test review.

## Findings

| ID | Level | Status | Topic |
|---|---|---|---|
| P1-P1-1 | P1 | Open | Core adds CLPI presentation start time to chapter timeline |
| P1-P2-1 | P2 | Open | Core rejects valid `0240` files |
| P1-P2-2 | P2 | Open | Core reads `index.bdmv` AppInfo flags at the wrong bit positions |
| P1-P2-3 | P2 | Open | Core does not use `BACKUP` parser paths |
| P1-P2-4 | P2 | Open | Core does not parse MPLS extension entries like libbluray |
| P1-INFO-1 | INFO | Open | Core omits non-chapter CLPI metadata |
| P2-INFO-1 | INFO | Open | libbluray has no automated parser test target in this checkout |

## Limits

- The libbluray source does not include a test directory or Meson test target.
- Meson and Ninja are not installed in the review environment, so libbluray was not built.
- The focused Core import tests passed.
