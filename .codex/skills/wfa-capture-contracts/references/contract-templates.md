# Contract templates (generic)

## Window / app state

| State field | Meaning | Default | Persistence |
| --- | --- | --- | --- |
| Current context | active document, resource, or workflow context | empty | session |
| Selection / mode | current user scope | default | session |
| Product options | user-configurable behavior | product default | session or settings |
| Pending changes | unsaved or staged state | none | session |
| Status / progress | feedback | empty / 0 | session |

Use **semantic** names, not control names.

## Command contract

```markdown
### <CommandName>
- Entry: …
- Preconditions: …
- Flow: numbered steps
- Success state: …
- Failure: clear stale state; diagnostic; log
- Missing dependency: typed diagnostic distinct from an unsupported capability
```

## Boundary operation contract

```markdown
### Operation: <name>
- Inputs and validation
- External dependencies, if any
- State and side effects
- Partial-success and cancellation policy
- Diagnostics
- Must not: hidden UI prompts or unbounded resource use
- Scenarios: …
```

## External-effect contract

```markdown
### Effect: <name>
- External target and ownership
- Input/state shape
- Options and compatibility rules
- Atomicity, retry, and recovery behavior
- Encoding/serialization rules when relevant
- Goldens/scenarios
```

## Shortcut matrix

| Gesture | Command | Legacy quirks |

## Aux window contract

- Lifetime (hide-on-close vs dispose)
- Content source
- Placement rules (optional, platform-sensitive)
