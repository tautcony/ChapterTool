## 1. Move Runtime Composition

- [x] 1.1 Move `IChapterImporterRegistry` and `RuntimeChapterImporterRegistry` into the Infrastructure import namespace without changing importer routing or fallback rules.
- [x] 1.2 Add an Infrastructure runtime factory for the importer registry, exporter, settings directory, process runner, tool locator, and shared Lua expression engine.
- [x] 1.3 Update Avalonia load services, composition, and tests to consume the moved registry contract and factory.

## 2. Extract Command-Line Library

- [x] 2.1 Add `ChapterTool.CommandLine` targeting `net10.0` with Core, Infrastructure, and DotMake dependencies.
- [x] 2.2 Move the four CLI source files into the CommandLine library and update namespaces and dependency injection to use `IChapterImporterRegistry`.
- [x] 2.3 Add a product-owned facade and typed launch/run records that hide DotMake result types while preserving command binding and desktop launch analysis.
- [x] 2.4 Keep CLI workflow behavior for formats, inspect, convert, diagnostics, output encoding, fallback imports, and expression options.

## 3. Add Hosts

- [x] 3.1 Add `ChapterTool.Cli` as a `net10.0` executable that delegates complete arguments to the CommandLine facade.
- [x] 3.2 Update the Avalonia project and `Program.Main` to reference CommandLine, consume typed desktop decisions, and initialize Sentry only for GUI startup.
- [x] 3.3 Add both new projects to `ChapterTool.Avalonia.slnx` and remove the direct DotMake dependency from Avalonia.

## 4. Verify Functional Migration

- [x] 4.1 Move or adapt CLI unit tests to the new ownership boundary and cover standalone help, explicit commands, plain-path rejection, GUI path compatibility, and exit-code categories.
- [x] 4.2 Update code maps for CommandLine, standalone host, Infrastructure runtime composition, Avalonia compatibility, and test ownership.
- [x] 4.3 Run focused tests and builds, then run the full solution tests sequentially and validate the OpenSpec change.
