#!/usr/bin/env python3
"""Subset the Avalonia shortcut-symbol font with FontTools."""

from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path


DEFAULT_SOURCE = Path("src/ChapterTool.Avalonia.UI/Assets/Fonts/source/Iosevka-Regular.ttf")
DEFAULT_OUTPUT = Path("src/ChapterTool.Avalonia.UI/Assets/Fonts/Iosevka-Regular.ttf")
KEEP_LAYOUT_FEATURES = "vert,vrtr,vrt2,vkna"


def required_text(extra_text: str) -> str:
    # Shortcut text boxes accept canonical ASCII gestures. The remaining
    # characters are the platform-neutral symbols produced for those gestures.
    ascii_gestures = "".join(chr(value) for value in range(0x20, 0x7F))
    symbols = "⌘⌃⌥⇧⇞⇟↑↓←→↵⎋⌫⌦⎀␠"
    return "".join(sorted(set(ascii_gestures + symbols + extra_text)))


def find_pyftsubset(path: str | None) -> str:
    if path:
        return path

    executable = shutil.which("pyftsubset")
    if executable:
        return executable

    raise RuntimeError(
        "pyftsubset was not found. Install FontTools with "
        "'uv sync --project scripts' or pass --pyftsubset."
    )


def supported_options(executable: str) -> str:
    result = subprocess.run([executable, "--help"], check=False, capture_output=True, text=True)
    return result.stdout + result.stderr


def run_subset(executable: str, source: Path, output: Path, text_file: Path) -> None:
    command = [
        executable,
        str(source),
        f"--text-file={text_file}",
        f"--output-file={output}",
        "--name-languages=*",
        f"--layout-features={KEEP_LAYOUT_FEATURES}",
    ]
    help_text = supported_options(executable)
    if "--no-prune-codepage-ranges" in help_text:
        command.append("--no-prune-codepage-ranges")
    if "--drop-tables" in help_text:
        command.append("--drop-tables+=BASE")
    try:
        subprocess.run(command, check=True, text=True)
    except FileNotFoundError as error:
        raise RuntimeError(f"FontTools command was not found: {executable}") from error
    except subprocess.CalledProcessError as error:
        raise RuntimeError(f"FontTools failed with exit code {error.returncode}") from error


def subset_font(
    source: Path,
    output: Path,
    executable: str,
    extra_text: str,
    check: bool,
) -> int:
    if not source.is_file():
        raise RuntimeError(f"Source font was not found: {source}")
    if source.resolve() == output.resolve():
        raise RuntimeError("Source and output must be different files")

    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="chaptertool-font-") as temporary:
        temporary_path = Path(temporary)
        text_file = temporary_path / "characters.txt"
        generated_font = temporary_path / output.name
        text_file.write_text(required_text(extra_text), encoding="utf-8")
        run_subset(executable, source, generated_font, text_file)

        if check:
            if not output.is_file() or output.read_bytes() != generated_font.read_bytes():
                print(f"{output} is not up to date", file=sys.stderr)
                return 1
            print(f"{output} is up to date")
            return 0

        os.replace(generated_font, output)
        print(f"Wrote {output} ({output.stat().st_size:,} bytes)")
        return 0


def main(arguments: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE, help="Original font path")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT, help="Subset font path")
    parser.add_argument("--pyftsubset", help="Path to the pyftsubset executable")
    parser.add_argument("--extra-text", default="", help="Additional characters to include")
    parser.add_argument("--check", action="store_true", help="Fail when the output is not current")
    args = parser.parse_args(arguments)

    try:
        return subset_font(args.source, args.output, find_pyftsubset(args.pyftsubset), args.extra_text, args.check)
    except RuntimeError as error:
        print(f"error: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
