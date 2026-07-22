# ChapterTool Node.js

This package exposes the portable ChapterTool Core API to Node.js through .NET WebAssembly. It does not use Blazor or a browser user interface.

## Build

Run the following command from this directory:

```bash
npm run build
```

The build requires the .NET 10 SDK. Run `npm run doctor` to inspect the active SDK and the optional WebAssembly build tools.

The build uses `wasm-tools` when it is installed. The build prints a warning and continues with the standard runtime when the workload is not installed. Install the workload with this command:

```bash
dotnet workload install wasm-tools
```

The build publishes the pure WebAssembly host, transpiles and bundles the TypeScript API for the `es2022` target, and writes all package files to `dist/`. Files in `src/`, `scripts/`, and `test/` are not included in the npm tarball.

The package uses `tsdown` to bundle the ESM entry point and generate the TypeScript declaration file. The .NET WebAssembly runtime stays as a package-local external asset.

Run `npm pack` to create an installable tarball. The `prepack` script builds the runtime before npm creates the tarball. Package consumers need Node.js only. They do not need the .NET SDK.

## Use

```js
import { ChapterTool } from "@chaptertool/node";
import { readFile } from "node:fs/promises";

const tool = new ChapterTool();
const imported = await tool.import(await readFile("chapters.txt"), {
  fileName: "chapters.txt"
});

const chapterSet = imported.groups[0].entries[0].chapterSet;
const exported = await tool.export(chapterSet, { format: "xml" });

if (!exported.success) {
  throw new Error(exported.diagnostics.map((item) => item.message).join("\n"));
}

console.log(exported.content);
```

## Runtime Contract

The package validates public JavaScript inputs before it calls the .NET runtime. Invalid option objects, strings, booleans, numbers, and chapter indexes produce a JavaScript error.

The package transfers complex values across the .NET WebAssembly boundary as JSON strings. This conversion is internal to the package. Callers use JavaScript objects and arrays through the typed API.

The WebAssembly runtime is initialized once per Node.js process. Concurrent `ChapterTool` instances share the initialized runtime. A failed startup can be retried by a later API operation.

The package requires Node.js 20, 22, 24, or later. It accepts UTF-8 strings, `Buffer`, and `Uint8Array` input. It supports the byte-based import formats provided by `ChapterTool.Core`. It does not run desktop tools such as `ffprobe`, `ffmpeg`, `mkvtoolnix`, or `eac3to`.

Supported export codes are `txt`, `xml`, `qpf`, `timecodes`, `tsmuxer`, `cue`, `json`, `vtt`, and `celltimes`.

## Core API

The package provides these Core capability groups:

- Byte-based chapter import and all Core export formats.
- Chapter editing, segment combination, and MPLS append operations.
- Frame rate detection, frame metadata calculation, and frame rate conversion.
- Expression evaluation, expression analysis, symbols, and presets.
- Output projection, time formatting, Celltimes conversion, and QPFile conversion.
- Export format, import format, XML language, and output encoding metadata.

The package does not provide interactive workspace state. It does not provide row selection, reload state, browser downloads, progress UI, logs, localization, settings, or file pickers.
