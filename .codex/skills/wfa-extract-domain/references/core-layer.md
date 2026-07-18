# Application core layer

## Include vs exclude

| Include | Exclude |
| --- | --- |
| Domain models and aggregates | Window/control types |
| Deterministic validation/translation | File pickers, UI prompts |
| Pure rules and state transitions | Environment capabilities |
| Boundary contracts and result types | View styling and presentation resources |
| Diagnostic codes / result types | Concrete log sinks |
| External-effect interfaces | Process runner implementations |

## Result-oriented APIs (illustrative)

```csharp
public sealed class OperationResult<TValue>
{
    public TValue? Value { get; init; }
    public IReadOnlyList<Diagnostic> Diagnostics { get; init; }
}

public interface IOperation<TInput, TValue>
{
    Task<OperationResult<TValue>> ExecuteAsync(TInput input, CancellationToken ct);
}

public sealed class OperationOptions
{
    // Domain options only; no UI control types or environment handles.
}
```

## Replace UI-shaped APIs

| Legacy smell | Core replacement |
| --- | --- |
| Update controls directly | Projection → bindable values |
| combo.SelectedIndex | Semantic option or state type |
| Notification.ShowError | Diagnostic + severity |
| Progress UI magic numbers | Explicit progress contract or no progress |

## Multi-item sources

Model groups/editions explicitly. **Which** item is selected is session/UI state;
combine/split rules can still be pure functions in Core.
