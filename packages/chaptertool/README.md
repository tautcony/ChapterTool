# ChapterTool Node.js

Use `@chaptertool/node` to read, edit, transform, and export chapter markers from a Node.js application. It is useful for media pipelines, batch conversion, and services that need chapter support without a desktop UI.

The package runs the portable ChapterTool Core API through .NET WebAssembly. It does not use Blazor or require the .NET SDK on the consuming machine.

## Install And Use

```bash
npm install @chaptertool/node
```

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

The package accepts UTF-8 strings, `Buffer`, and `Uint8Array` input and supports the portable import and export formats listed below.

## Build From Source

Run the following command from this directory:

```bash
npm run build
```

The build requires the .NET 10 SDK. Run `npm run doctor` to inspect the active SDK and the optional WebAssembly build tools.

The build uses `wasm-tools` when it is installed. The build prints a warning and continues with the standard runtime when the workload is not installed. Install the workload with this command:

```bash
dotnet workload install wasm-tools
```

The build writes the distributable package to `dist/`.

Run `npm pack` to create an installable tarball. The `prepack` script builds the runtime before npm creates the tarball. Package consumers need Node.js only. They do not need the .NET SDK.

## Runtime And Format Boundaries

The package requires Node.js 20.x, 22.x, or 24 and later. It accepts UTF-8 strings, `Buffer`, and `Uint8Array` input. It supports the byte-based import formats provided by `ChapterTool.Core`. It does not run desktop tools such as `ffprobe`, `ffmpeg`, or `mkvtoolnix`.

Portable imports have a 64 MiB byte limit. Inputs above the limit return an error with code `INPUT_TOO_LARGE`.

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
