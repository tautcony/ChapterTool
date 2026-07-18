# Platform and infrastructure abstraction

## Principle

Anything needing OS handles, user dialogs, persistence, child processes, network, devices, or
other environment effects is **not** the application core.

## Typical service set

| Service | Responsibility |
| --- | --- |
| State store | Load/save validated versioned state |
| Process adapter | Run a child process with capture and cancellation, if present |
| Resource locator | Resolve a configured external dependency, if present |
| Clipboard/transfer | Host data exchange, if present |
| Interaction service | Confirmations and host dialogs, if present |
| Resource picker | Host file/resource selection, if present |
| Window service | Show secondary tools, if present |
| Diagnostics/logging | Structured operational evidence |
| Environment capability | Supported/unsupported result for an actual feature |

Prefer narrow ports over a mega `IPlatform`.

## Windows-only pattern

```csharp
bool IsSupported { get; }
// when false: UI hides/disables; APIs return Unsupported
```

## Lifetime

Use shared lifetimes only for services whose identity and resources require it; the composition
root owns the graph and disposal.
