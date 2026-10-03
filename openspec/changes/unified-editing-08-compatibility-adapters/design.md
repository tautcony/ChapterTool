## Context

Interactive state moves to the document session. Stateless consumers still need convenient one-shot conversion, and CLI transform flags are an existing user-facing contract.

## Goals / Non-Goals

**Goals:** Preserve supported Core, CLI, and Node.js behavior while routing all output through pure serialization after a single transform pass.

**Non-Goals:** Persist stateful history in conversion calls or define raw CLI argument parsing outside DotMake.

## Decisions

- Keep a clearly named compatibility facade that adapts old option shapes to operation candidates and then to the pure serializer.
- Keep CLI command and option definitions in `DotMake.CommandLine`; reject conflicting options before source import.
- Reuse the shared expression engine and format services for parity.
- State the deprecation window and replacement API in public API and Node.js package documentation.

## Risks / Trade-offs

- [Stateless adapters cannot detect already transformed values] → Document the boundary and make the pure serialization API obvious for callers that retain results.
- [Compatibility layer becomes permanent] → Record its version policy and schedule removal only in a future explicitly versioned change.
