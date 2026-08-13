# Blu-ray Parser Fix Plan

## Batch 1: Correct chapter time

Fix `P1-P1-1` first. Remove CLPI presentation-time addition from playlist-time calculations. Preserve CLPI lookup for packet mapping. Add a regression test that asserts the first chapter remains zero and that a later play item starts at the sum of MPLS durations.

## Batch 2: Restore format compatibility

Fix `P1-P2-1`, `P1-P2-2`, and `P1-P2-3`. Use one BDMV version policy. Correct INDEX bit extraction. Add primary and backup path fixtures. Verify BDMV discovery and standalone MPLS import.

## Batch 3: Complete extension and metadata contracts

Fix `P1-P2-4` if raw extension data is part of the supported parser contract. Otherwise remove the implication that the parser is libbluray-equivalent and document the intentional scope. Resolve `P1-INFO-1` with the same decision.

## Batch 4: Native libbluray verification

Build libbluray with `enable_devtools=true`. Run `mpls_dump -c` and `clpi_dump` on the Core fixtures. Add a repeatable comparison command or fixture capture. Do not treat successful compilation as parser verification.
