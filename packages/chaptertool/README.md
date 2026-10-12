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
const exported = await tool.serialize(chapterSet, { format: "xml" });

if (!exported.success) {
  throw new Error(exported.diagnostics.map((item) => item.message).join("\n"));
}

console.log(exported.content);
```

## Transform And Serialize

Use an explicit content operation when chapter values must change. `serialize` writes the
supplied snapshot as-is and never replays transforms.

```js
const rate = { kind: "rational", numerator: "24000", denominator: "1001" };
const transformed = await tool.applyExpression(chapterSet, true, "t + 1", rate);
const output = await tool.serialize(transformed.chapterSet, { format: "xml" });
```

The package exposes only exact chapter content contracts. Apply expressions, naming, numbering,
and frame-rate changes through content operations before serialization.

The package accepts UTF-8 strings, `Buffer`, and `Uint8Array` input and supports the portable import and export formats listed below.

## Exact Ticks And Frame Rates

The API uses tick strings and tagged frame-rate values. Rational components and approximate
values remain strings so JavaScript numbers do not round them.

```ts
const imported = await tool.import(await readFile("input.mpls"), {
  fileName: "input.mpls"
});
const source = imported.groups[0].entries[0].chapterSet;
const rate = { kind: "rational", numerator: "24000", denominator: "1001" } as const;
const edited = await tool.editFrame(source, 0, "1.5", rate);
if (!edited.success || !edited.chapterSet) throw new Error("Frame edit failed");
const output = await tool.serialize(edited.chapterSet, { format: "qpf" });
```

Exact wire values use these shapes:

```json
{
  "title": "Example",
  "sourceName": null,
  "importFormat": "Ogm",
  "frameRate": { "kind": "rational", "numerator": "24000", "denominator": "1001" },
  "durationTicks": null,
  "chapters": [{ "displayNumber": 1, "name": "Opening", "startTicks": "9007199254740993", "endTicks": null, "framesInfo": "", "frameAccuracy": "Neutral", "kind": "Marker" }]
}
```

`startTicks` and `endTicks` use signed Int64 decimal strings.
One tick is 100 ns.
An absent end uses `null`.
`durationTicks: null` means Unknown; `"0"` means a known zero duration.
Rational components must be positive Int64 strings and are reduced on output.
Approximate uses `{ "kind": "approximate", "value": "23.976" }`.
Its value must be a positive invariant decimal string within the .NET decimal range.
Unknown uses `{ "kind": "unknown" }` with no payload.
Conflicting payload fields, non-finite values, exponents, and invalid components return diagnostics.
Parse failures return `chapterSet: null`.
Operation failures preserve input content where available.

Optional `segments` preserve `id`, `name`, `startTicks`, nullable `durationTicks`, `frameRate`, and `sourceFrameRate`.
Current rates can change during conversion; source rates retain their original authority.
Scalar combination must reject conflicting segment rates.
A known rate plus Unknown, different rates, or Rational plus Approximate resolves to Unknown.
The same numeric display does not make different kinds equal.

Auto uses known content authority for computation.
Detection supplies display suggestions only when authority is missing or mixed.
Frame edits, shifts, zones, and frame exports then require an explicit computation rate.
Time-only edits and exports remain available.
Pass `computationRate` in exact output options to export mixed content at an explicit rate.
This override must not change the supplied content or source metadata.

Exact methods cover import, cell edits, rename, insert, delete, shifts, zones, combine, append,
frame-rate detection, frame projection, conversion, expressions, and serialization.
`updateFrames` refreshes display metadata and must not assign a detected content rate.
`serialize` writes the supplied snapshot without replaying an expression.

Frame arithmetic retains ticks and integer rate coefficients until its rounding boundary.
Fractional frame edits round once to ticks, with midpoints away from zero.
FPS conversion rounds to an integer source frame, then rounds once to target ticks.
Frame exports round once to an integer frame, with midpoints away from zero.
Display rounding must not change content.
Lua uses floating-point seconds; changed arithmetic results are not exact rational arithmetic.
An unchanged Lua time result retains the original ticks.
Unknown FPS is `nil` in Lua.
`fpsRateKind` exposes authority, and rational component text preserves large integers.
Numeric ratio components are available only when both are at most `9007199254740991`.

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
