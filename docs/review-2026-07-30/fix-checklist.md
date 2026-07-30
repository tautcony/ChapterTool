# Blu-ray Parser Fix Checklist

> Source: `docs/review-2026-07-30/`
> Last updated: 2026-07-30

## P1-P1-1 — CLPI shifts playlist time

- Source: [`phase-1-parser.md`](./phases/phase-1-parser.md)
- Owner/batch: Unassigned / Batch 1
- Status: [x]

- [x] Confirm with the existing `StcAwarePtsTests` fixture and assert the exact first and later chapter times.
- [x] Remove `PresentationStartTime` from title-time accumulation.
- [x] Add regression coverage for one and multiple play items.
- [x] Keep CLPI STC and EP-map records available for packet lookup; the chapter-only Core workflow does not add the STC baseline to title time.
- [x] Run the focused Core import tests.

## P1-P2-1 — `0240` version rejection

- Source: [`phase-1-parser.md`](./phases/phase-1-parser.md)
- Owner/batch: Unassigned / Batch 2
- Status: [x]

- [x] Add `0240` binary header fixtures for MPLS, CLPI, and INDEX.
- [x] Accept `0240` in the three header readers.
- [x] Run Core import tests. Native BDMV tests are covered by the Infrastructure test run below.

## P1-P2-2 — INDEX AppInfo bit shift

- Source: [`phase-1-parser.md`](./phases/phase-1-parser.md)
- Owner/batch: Unassigned / Batch 2
- Status: [x]

- [x] Add independent bit-field test cases.
- [x] Correct output mode, content flag, and dynamic-range extraction.
- [x] Update the binary builder test data to encode the standard layout.
- [x] Run the Index importer tests.

## P1-P2-4 — MPLS extension parsing gap

- Source: [`phase-1-parser.md`](./phases/phase-1-parser.md)
- Owner/batch: Unassigned / Batch 3
- Status: [ ]

- [x] Confirm the extension address base with the real libbluray-readable `00020_Terminator2.mpls` fixture.
- [x] Add entry range validation and a known extension fixture.
- [x] Parse PiP, extension SubPath, and static metadata records while preserving raw payload data.
- [ ] Run native libbluray comparison tools. Meson and Ninja are unavailable in this environment.

## P1-INFO-1 — CLPI metadata omission

- Source: [`phase-1-parser.md`](./phases/phase-1-parser.md)
- Owner/batch: Unassigned / Batch 3
- Status: [x]

- [x] Define Core as a managed CLPI metadata parser used by the chapter importer. It does not read M2TS packets.
- [x] Add typed records and tests for ATC deltas, subtitle fonts, video coding type `0x20`, ISRC values, TS type information, and raw CLPI extension data.

The remaining native comparison command is an environment limitation. The checklist does not include the removed BACKUP fallback or libbluray automated-test-gap items.
