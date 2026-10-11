# ChapterTool

ChapterTool is a cross-platform command-line tool for multimedia chapter files. It can list formats, inspect chapter sources, and convert chapter data without the desktop application.

## Requirements

- Install a compatible .NET 10 runtime.
- Install `ffprobe` from FFmpeg to read chapters from general media containers.
- Install `mkvextract` from MKVToolNix to read Matroska chapters.
- Blu-ray `BDMV` folders use the managed importer.

ChapterTool searches standard platform locations for these external tools.

## Global Tool

Install the tool:

```bash
dotnet tool install --global ChapterTool
```

Update the tool:

```bash
dotnet tool update --global ChapterTool
```

Remove the tool:

```bash
dotnet tool uninstall --global ChapterTool
```

## Local Tool

Create a tool manifest and install ChapterTool:

```bash
dotnet new tool-manifest
dotnet tool install ChapterTool
```

Run the local tool:

```bash
dotnet tool run chaptertool --help
```

## Commands

```bash
chaptertool formats
chaptertool inspect input.mpls
chaptertool convert input.xml --format txt --output chapters.txt
chaptertool convert input.xml --format vtt --stdout
chaptertool --version
```

Use `chaptertool <command> --help` to see the options for a command.

`convert` accepts one optional expression input. Use `--expression` for Lua text or
`--expression-preset` for a built-in preset. The command rejects both options before it imports
the input. DotMake defines and binds all command-line options.

## Frame Rate Authority

Use `--frame-rate-ratio N/D` for an authoritative rational computation rate:

```bash
chaptertool convert input.mpls --format qpf --frame-rate-ratio 24000/1001 --stdout
chaptertool convert input.txt --format celltimes --frame-rate 23.976 --stdout
chaptertool convert input.mpls --format qpf --stdout
```

Ratio components must be positive Int64 integers.
The command reduces equivalent ratios, such as `48000/2002`.
`--frame-rate` supplies Approximate authority from a finite positive numeric value.
The command must reject both options together before import.
Invalid ratios, non-finite values, zero, and out-of-range numeric values exit with code 1.
The major-version-23 numeric option remains supported.

With no override, conversion uses the imported canonical rate.
MPLS and IFO metadata can supply an exact rate.
Unknown or mixed rates require an explicit override for FPS-dependent expressions and frame exports.
Detection suggestions must not become computation authority.
Time-only conversion remains available for Unknown content.
An explicit override applies to expression evaluation and export once.
`inspect` retains numeric FPS and adds canonical kind and value labels.
`--stdout` writes exported content only.

Frame arithmetic retains timestamp ticks and integer rate coefficients until rounding.
QPFile and Celltimes output round once to integer frames, with midpoints away from zero.
Display rounding must not change timestamps.
Lua arithmetic uses floating-point seconds.
Unknown `fps` is `nil`; time-only scripts can still run.
An unchanged Lua time result retains the original ticks.
Rational component text globals preserve large integers.
Numeric component globals require both integers to be at most `9007199254740991`.

## License

ChapterTool is distributed under the GPLv3+ license.
