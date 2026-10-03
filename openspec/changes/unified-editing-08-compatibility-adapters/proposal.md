## Why

The interactive document model needs one-shot transforms, but Core, CLI, and Node.js callers may rely on current conversion/export options. Compatibility adapters must preserve those entry points without reintroducing persistent projection state or double application.

## What Changes

- Keep public Core and Node.js APIs usable through explicit compatibility adapters.
- Convert legacy naming/expression options into one candidate transform, then call pure serialization.
- Preserve CLI `--expression` and `--expression-preset` semantics using `DotMake.CommandLine` arguments.
- Define deprecation and removal conditions for legacy transform-bearing export options.

## Capabilities

### New Capabilities
- `one-shot-transform-compatibility`: One-time conversion transforms for Core, CLI, and Node.js.

### Modified Capabilities
- None.

## Impact

Core public API, CLI workflows, Node.js wrapper, command-line tests, package/API documentation. Depends on `unified-editing-07-session-lifecycle`.
