# Contracts Code Map

`src/ChapterTool.Contracts` owns host-neutral settings models and platform contracts.

Use this project when a desktop, browser, or command-line host needs the same persisted data shape or service boundary.

## Ownership

- settings models:
  - `src/ChapterTool.Contracts/Configuration/ChapterToolSettings.cs`
  - `src/ChapterTool.Contracts/Configuration/AppSettings.cs`
  - `src/ChapterTool.Contracts/Configuration/FontSettings.cs`
  - `src/ChapterTool.Contracts/Configuration/ThemeSettings.cs`
  - `src/ChapterTool.Contracts/Configuration/ThemePresetCatalog.cs`
  - `src/ChapterTool.Contracts/Configuration/DeleteRowsTimingModes.cs`
  - `src/ChapterTool.Contracts/Configuration/FrameDisplayModes.cs`

`DeleteRowsTimingModes` and `FrameDisplayModes` parse and serialize the persisted editing-preference ids in `AppSettings`. `DeleteRowsTimingMode` selects whether deleted-row timing stays unchanged or shifts to restart at zero. `FrameDisplayMode` selects integer rounding or fixed decimal places for frame values.

- settings store contract:
  - `src/ChapterTool.Contracts/PlatformPorts/ISettingsStore.cs`
- shared platform contracts:
  - `src/ChapterTool.Contracts/PlatformPorts/IApplicationLogService.cs`
  - `src/ChapterTool.Contracts/PlatformPorts/IClipboardService.cs`
  - `src/ChapterTool.Contracts/PlatformPorts/IExternalToolLocator.cs`
  - `src/ChapterTool.Contracts/PlatformPorts/IShellService.cs`
  - `src/ChapterTool.Contracts/PlatformPorts/ExternalToolLocation.cs`

The project has no Avalonia dependency. Infrastructure implements the shared runtime boundaries. Avalonia UI consumes the contracts through its own adapters.
