# Legacy vs new diff template (generic)

```markdown
# Legacy vs new implementation diff

Date:
Legacy root:
New roots:

## Module map
| Module | Legacy paths | New paths |

## Summary
- Ready as sole trunk:
- Blocks legacy deletion:
- Intentional product changes:

## Matrix
| Feature | New status | Diff | Impact | Decision | Owner |

## High-priority gaps
1. …
```

## How to gather evidence

- Compare declared capability lists and discoverability surfaces
- Diff command, shortcut, focus, and accessibility contracts
- Search unimplemented, unsupported, and placeholder paths
- Run the same representative scenarios on both sides while legacy still builds

## Severity

| Level | Examples |
| --- | --- |
| Blocker | Required capability missing, data corruption, crash |
| High | Common workflow wrong |
| Medium | Shortcut renames, polish |
| Low | Easter eggs, unused advanced tools |
