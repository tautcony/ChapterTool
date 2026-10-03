## 1. Compatibility boundary

- [x] 1.1 Add/retain pure serializer APIs and adapters from legacy Core export options to one-shot document operations.
- [x] 1.2 Update CLI conversion composition so DotMake-bound expression options transform once before serialization.
- [x] 1.3 Migrate Node.js wrappers to explicit transform-then-serialize behavior and document caller migration.

## 2. Public behavior verification

- [x] 2.1 Add API behavior tests comparing compatibility calls with explicit transform plus serialization across supported formats.
- [x] 2.2 Add CLI tests for inline expression, preset, conflicts, unknown presets, and no-expression default.
- [x] 2.3 Add Node.js package tests for transforms, repeated calls, and serialization without double application.
- [x] 2.4 Run Core, CommandLine, and Node.js checks sequentially; verify CLI arguments remain defined and parsed only through DotMake.
