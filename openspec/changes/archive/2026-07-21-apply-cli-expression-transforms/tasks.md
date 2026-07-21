## 1. CLI Expression Surface

- [x] 1.1 Add structured `--expression` and `--expression-preset` options to the convert command and carry them in `CliConvertRequest`.
- [x] 1.2 Resolve preset identifiers through the shared expression engine, reject conflicting or unknown expression options, and map the selected source into `ChapterExportOptions`.
- [x] 1.3 Enable the shared Lua expression engine in the default CLI export factory and update CLI scope/help text.

## 2. Verification

- [x] 2.1 Add CLI tests for inline expressions, built-in presets, unchanged default behavior, unknown presets, and conflicting options.
- [x] 2.2 Run focused CLI/Core tests, validate the OpenSpec change, and update the applicable code-map documentation if ownership or entry points changed.
