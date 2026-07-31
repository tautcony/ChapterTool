# Phase 2 Review: libbluray Test Coverage

> Date: 2026-07-30
> Files: libbluray Meson files, parser entry points, devtools, and repository test paths.
> Findings: INFO(1)
> Navigation: [Back to review index](../index.md) | [Fix checklist](../fix-checklist.md)

## Reviewed Files

- `libbluray/meson.build`
- `libbluray/meson_options.txt`
- `libbluray/src/meson.build`
- `libbluray/src/devtools/meson.build`
- `libbluray/src/devtools/mpls_dump.c`
- `libbluray/src/devtools/clpi_dump.c`
- `libbluray/src/libbluray/bluray.h`
- `libbluray/src/libbluray/bdnav/mpls_parse.c`
- `libbluray/src/libbluray/bdnav/clpi_parse.c`

## Findings

### P2-INFO-1: No automated parser test target exists

- Location: `libbluray/meson.build` and all nested `meson.build` files.
- Evidence: No `test()` declaration or test subdirectory exists. `mpls_dump` and `clpi_dump` are optional development executables. `bluray.h` labels `bd_read_mpls` and `bd_read_clpi` as testing/debugging APIs, but no test runner calls them.
- Impact: A default build validates compilation only. Parser changes can pass CI without fixture-level behavior checks.
- Direction: Add a small native test target with representative MPLS and CLPI fixtures, or add a cross-language comparison harness that calls the testing APIs.

## Missed-Risk Review

- Build defaults: checked. `enable_devtools` is false by default.
- Manual parser tools: checked. `mpls_dump -c` prints chapter marks; `clpi_dump` prints CLPI records.
- Public parser test APIs: checked. `bd_read_mpls` and `bd_read_clpi` are available.
- Automated fixtures, fuzzing, and Meson tests: none found.

## Uncovered Areas

- Homebrew `libbluray 1.5.0` is now available.
- Homebrew provides `bd_info`, `bd_list_titles`, the public headers, and the native library.
- Homebrew does not provide `mpls_dump` or `clpi_dump` binaries.
- The repository checkout is also `libbluray 1.5.0`.
- The devtool sources can compile directly against the Homebrew library without Meson or Ninja.
- Use [`../native-libbluray-parity.md`](../native-libbluray-parity.md) for the verified comparison procedure.

## Executed Comparison

The direct Homebrew comparison is complete for the current fixture corpus.

- Core and libbluray parsed 160/160 BDMV MPLS files.
- All 160 MPLS records matched on the compared fields.
- Core and libbluray parsed 244/244 BDMV CLPI files.
- All 244 CLPI records matched on common fields.
- Six complete BDMV discs matched in relevant-title mode for playlist set,
  duration, and chapter count.
- The independent MPLS set had 18 valid exact matches. Core rejected one
  malformed file that libbluray accepted as an empty playlist.
- The real PiP fixture matched. The earlier suspected PiP address-base defect
  is not confirmed.

The comparison used `bd_list_titles` without `-a` because that mode matches Core
relevant-title filtering. The `-a` option remains useful to document intentional
policy differences for no-chapter and repeated-segment candidates.

## Remaining Test Work

The following cases still need dedicated fixtures or behavioral comparison:

- primary-file damage and per-file `BDMV/BACKUP` fallback;
- HDMV conditional branch and title-call execution;
- BDJO dynamic navigation;
- INDEX access-control and hidden-title flags;
- non-empty CLPI SS and additional MPLS extension records.
