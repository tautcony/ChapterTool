## Why

Several public and private methods have high Crap Scores and combine unrelated decisions. This makes behavior changes risky and leaves important parsing and UI workflows difficult to test. The change is needed now to reduce maintenance risk while preserving current import, export, CLI, and desktop behavior.

## What Changes

- Split high-complexity format mapping methods into data-driven lookups with explicit unknown-value behavior.
- Decompose CLI import, convert, inspect, validation, and output workflows into focused helpers.
- Extract decision tables and boundary handlers from Avalonia event handlers and view models.
- Separate BDMV/HDMV navigation and Windows registry probing into focused operations.
- Add or extend focused tests for preserved behavior and edge cases.
- Update code-map ownership notes if entry points or primary tests move.

## Capabilities

### New Capabilities
- `complexity-reduction`: Focused, behavior-preserving implementations for previously high-complexity methods.

### Modified Capabilities

## Impact

Affected projects are `ChapterTool.Core`, `ChapterTool.CommandLine`, `ChapterTool.Infrastructure`, and `ChapterTool.Avalonia.UI`, plus their focused test projects. Public signatures and user-visible behavior remain compatible unless an existing test documents otherwise. No new runtime dependencies are expected.
