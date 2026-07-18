---
name: wfa-avalonia-ui
description: >
  Phase D of a WinForms-to-Avalonia migration: build the Avalonia UI shell with composition,
  MVVM state and commands, responsive XAML, accessibility, localization boundaries, and
  behavior-focused unit/Headless tests. Use only when an explicit migration is ready to port
  Forms or when the orchestrator routes here. Prefer this over control-by-control ports.
---

# Phase D — Avalonia UI shell

Present Phase B/C capabilities through a **bindable** Avalonia shell. Code-behind is limited
to control-only gestures and host interaction adapters. Business flow lives in
ViewModels/workflows.

Read [MVVM patterns](references/mvvm-patterns.md) for responsibility boundaries,
[control mapping](references/control-mapping.md) only while translating legacy interaction
semantics, and [modernization](references/modernization.md) for the completion checklist.

## Preconditions

- Core APIs and platform abstractions available (fakes OK)
- Phase A command catalog for the main window

## Workflow

### 1. Host and composition

- Avalonia `App` + entry
- Composition root builds services, ViewModels, main window
- Constructor injection over `new` inside Views

### 2. Commands and state

- Map the command catalog to `ICommand` / async commands
- `INotifyPropertyChanged` (or small base / toolkit)
- Eliminate field-by-field `Refresh()` as the primary sync

### 3. Layout and accessibility

Derive workflow zones from Phase A contracts. Use responsive panels, stable constraints, and
appropriate scrolling; avoid absolute positioning for normal controls. Preserve accessible
names, focus order, keyboard navigation, and visible focus.

### 4. Bindings

- Use compiled bindings and `x:DataType`; document any deliberate exception
- Stable automation ids and accessible names for interaction surfaces

### 5. Collections and editing, when present

- Observable item projections when the product displays collections
- Edits route through application commands and reproject derived state
- Context actions bind to the same command semantics as other entry points

### 6. Shortcuts

- Prefer declared key bindings
- Do not intercept editing/navigation gestures from the focused control
- Document intentional gesture changes

### 7. Secondary surfaces, when present

- Dedicated View + ViewModel per cohesive surface
- Window service shows/focuses; does not build large control trees

### 8. Localization

- External resources for UI strings
- Presentation-layer localization manager
- Core free of UI culture for algorithms

### 9. State decomposition, when complexity grows

If the main VM bloats early, introduce a typed state aggregate and workflow collaborators only
when evidence justifies them; full decomposition patterns are Phase G.

### 10. Tests

| Project | Content |
| --- | --- |
| Avalonia unit | VM/commands/services — **no** Headless attributes |
| Avalonia Headless | separate project; behavior-focused UI |

## Early modernization (do not defer forever)

1. Composition root
2. Observable VM; kill pervasive Refresh
3. Real command surfaces (no hidden shims)
4. Awaited async commands
5. Injected boundary adapters discovered in Phase B/C
6. Secondary surfaces as XAML+VM when present

## Corrections seen in practice

- A state change or option must have the same semantics across every affected view and command.
- Layout is part of behavior: verify the supported default, wide, narrow, scaling, and localization states.
- Group controls by product task rather than by implementation type.
- Numeric inputs need explicit empty, range, increment, and validation semantics from the contract.
- Window gestures must not steal input from the focused editor/control.
- Do not use source-text XAML tests or accidentally start the desktop app from unit tests.
- Reuse application services instead of reimplementing rules in individual windows.

## Verification layers

1. ViewModel tests
2. Headless interactions, focus, accessibility, localization, and responsive-state behavior
3. Executable smoke on every supported target environment

## Deliverables

- [ ] App runs with composition root
- [ ] Phase A workflows are reachable through commands and bindings
- [ ] Bindings-driven UI
- [ ] Accessibility and responsive layout contracts verified
- [ ] Unit + Headless coverage for critical paths

## Handoff

- Parity/cutover → `wfa-parity-verify`
- Product evolution → `wfa-post-port-evolve`
- God VM / security / isolation → `wfa-harden-decompose`
