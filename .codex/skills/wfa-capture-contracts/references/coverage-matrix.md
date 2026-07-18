# Coverage matrix template

## Module index

| Module | Doc path | Responsibility |
| --- | --- | --- |
| 01 | modules/01-ui-shell.md | Main interaction shell and commands |
| 02 | modules/02-application-core.md | Application state and rules |
| … | … | … |

## Rows

| File | Primary owner | Referenced by | Disposition / notes |
| --- | --- | --- | --- |

Include production code, tests, fixtures, build/distribution files, and assets within the
declared migration roots. Mark generated/vendor content instead of treating it as owned code.

## Mechanical check

1. List all files under the declared legacy roots.
2. Ensure each relevant path appears exactly once as a primary owner.
3. Assign every orphan or explicitly mark it generated, vendor, obsolete, or out of scope.
