## Why

The CLI currently forces expression projection off even though the GUI and Core export pipeline already support Lua expression transforms and built-in presets. This makes the same chapter conversion produce different results depending on whether it runs in the GUI or CLI.

## What Changes

- Add structured CLI options to apply a Lua expression or select a built-in expression preset during conversion.
- Resolve preset identifiers through the shared expression engine and pass the selected source into the Core export projection.
- Enable the shared Lua expression engine in the default CLI export service.
- Report expression diagnostics through the existing CLI diagnostic output and return a failure code when expression evaluation prevents a valid export.
- Keep expression options opt-in so existing CLI conversions remain unchanged.

## Capabilities

### New Capabilities

### Modified Capabilities

- `command-line-conversion-workflows`: CLI conversion can apply the same expression-based time projection that the GUI uses.

## Impact

- `src/ChapterTool.Avalonia/Cli` command definitions, request mapping, validation, and conversion execution.
- `src/ChapterTool.Avalonia/Composition/AppCompositionRoot.cs` shared CLI export composition.
- CLI and Core transform tests.
- The CLI help and `formats` scope text will advertise the new opt-in expression capability.
