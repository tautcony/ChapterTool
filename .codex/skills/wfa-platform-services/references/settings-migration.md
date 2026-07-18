# Persisted state migration

## Separate concerns

| Kind | Handling |
| --- | --- |
| Legacy persisted state | versioned document or explicit adapter |
| Multiple legacy stores | ordered migrators into one logical update |
| Environment-owned preferences | isolate behind an optional platform adapter |
| Deployment-managed configuration | document ownership and replacement policy |

## Target pattern

1. Inventory all persisted fields, ownership, defaults, and compatibility requirements
2. Define a strongly typed document and schema version
3. On load, validate before mapping; reject unsupported future versions
4. Write migrated state atomically and preserve predecessor/last-known-good data
5. Unit test each mapping, default, invalid-state, and concurrent-update path

## Version rules

- Known older versions: upgrade in memory, validate, then write current
- Future/unsupported versions: **fail**, never write defaults over unreadable state
- Predecessor state: best-effort mapping with sources intact

## Concurrent updates

Coordinate read-modify-write at the deployment's required scope, validate the complete result,
and atomically replace it without clobbering unrelated updates.
