# MVVM patterns

## Responsibility split

| Layer | Owns |
| --- | --- |
| View | Layout, bindings, visuals, accessibility metadata |
| Code-behind | Control-only gestures and host interaction adapters |
| ViewModel | Bindable state and commands |
| Workflows/services | Multi-step use cases and async coordination |
| Core/Infrastructure | Application behavior and environment adapters |

## Observable state

Every UI-visible field notifies; collections are observable when collection semantics exist;
command availability tracks the state it depends on.

## Async commands

Observe exceptions, await work, define cancellation/reentrancy, and expose busy/error state.

## Projection

Separate application state from presentation projections when display-specific transformation
exists. Do not duplicate application rules in row/item ViewModels.

## Avoid god windows

Split by cohesive product workflow. Introduce a typed state aggregate and workflow collaborators
only when the existing state/orchestration complexity justifies them.

## Secondary surfaces

```text
Host interaction service
  -> resolve a dedicated ViewModel through narrow ports
  -> show/focus the View
```

The service owns lifetime and display; it does not construct a large imperative control tree.
