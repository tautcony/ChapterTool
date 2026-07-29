# ChapterTool

ChapterTool is a cross-platform command-line tool for multimedia chapter files. It can list formats, inspect chapter sources, and convert chapter data without the desktop application.

## Requirements

- Install a compatible .NET 10 runtime.
- Install `ffprobe` from FFmpeg to read chapters from general media containers.
- Install `mkvextract` from MKVToolNix to read Matroska chapters.
- Install `eac3to` to read Blu-ray `BDMV` folders.

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

## License

ChapterTool is distributed under the GPLv3+ license.
