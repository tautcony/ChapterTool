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

- Meson configuration and native execution were not run because Meson and Ninja are not installed.
