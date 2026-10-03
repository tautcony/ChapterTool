## Context

Avalonia and Wasm currently coordinate their own chapter editing and projection paths. Both need to present the Core session history without duplicating it.

## Goals / Non-Goals

**Goals:** Provide consistent undo/redo/history workflows, keyboard routing, status, and per-document Core session use in both hosts.

**Non-Goals:** Change the history storage model or add persistence across refresh/restart.

## Decisions

- Keep history authoritative in Core. Host view models request commands and project visible node labels only.
- Use the existing shortcut catalog and conflict validation for application shortcuts; preserve platform defaults and text-control ownership.
- Virtualize the history panel and create detailed entries on demand.
- Keep host presentation adapters separate where platform behavior differs, but share transaction and navigation semantics.
- Localize history-lifetime and result messages through the shared translation source; generate browser JSON from AXAML.

## Risks / Trade-offs

- [Shortcut handling may override editor behavior] → Exercise real focus and draft workflows in Headless and browser tests.
- [History branches can be large] → Limit display object creation by visible/expanded range while keeping nodes in Core.
