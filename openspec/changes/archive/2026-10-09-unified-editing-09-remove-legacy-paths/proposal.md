## Why

The migration is complete only when the old projection and direct mutation paths cannot compete with the unified document session. This change closes the migration, updates navigation documentation, and records behavior and performance evidence.

## What Changes

- Remove persistent interactive projection state and obsolete parallel editable clip backups.
- Remove or constrain direct mutation and transform-during-export paths after all consumers have migrated.
- Update code maps and primary test ownership for the unified session model.
- Run complete cross-project verification, history/resource checks, browser workflows, and UI layout evidence.

## Capabilities

### New Capabilities
- `unified-editing-migration-completion`: No competing interactive state path remains and all migrated workflows are verified.

### Modified Capabilities
- None.

## Impact

Core, Avalonia, Wasm, CLI, Node.js, code maps, existing specifications, and the full test/build pipeline. Depends on `unified-editing-08-compatibility-adapters`.
