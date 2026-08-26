#!/usr/bin/env python3
"""Generate flat JSON locale files from Avalonia AXAML dictionaries."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from xml.etree import ElementTree

XAML_NAMESPACE = "http://schemas.microsoft.com/winfx/2006/xaml"
STRING_TAG = f"{{{XAML_NAMESPACE}}}String"
DEFAULT_SOURCE = Path("src/ChapterTool.Avalonia.UI/Localization/Resources/Locales")
DEFAULT_OUTPUT = Path("src/ChapterTool.Wasm/Resources/Locales")


def read_locale(path: Path) -> dict[str, str]:
    try:
        root = ElementTree.parse(path).getroot()
    except ElementTree.ParseError as error:
        raise ValueError(f"could not parse {path}: {error}") from error

    values: dict[str, str] = {}
    for element in root.iter(STRING_TAG):
        key = element.get(f"{{{XAML_NAMESPACE}}}Key") or element.get("Key")
        if not key:
            raise ValueError(f"{path}: x:String is missing x:Key")
        if key in values:
            raise ValueError(f"{path}: duplicate x:Key '{key}'")
        values[key] = element.text or ""
    if not values:
        raise ValueError(f"{path}: no x:String resources found")
    return values


def render(values: dict[str, str]) -> str:
    return json.dumps(values, ensure_ascii=False, indent=2) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-dir", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--check", action="store_true", help="fail when generated files differ")
    args = parser.parse_args()

    root = Path(__file__).resolve().parents[1]
    source_dir = (root / args.source_dir).resolve() if not args.source_dir.is_absolute() else args.source_dir
    output_dir = (root / args.output_dir).resolve() if not args.output_dir.is_absolute() else args.output_dir

    failed = False
    for source in sorted(source_dir.glob("*.axaml")):
        target = output_dir / f"{source.stem}.json"
        try:
            expected = render(read_locale(source))
        except ValueError as error:
            print(error, file=sys.stderr)
            return 2

        actual = target.read_text(encoding="utf-8") if target.exists() else None
        if args.check:
            if actual is None:
                print(f"out of date: {target}", file=sys.stderr)
                failed = True
                continue
            if actual != expected:
                print(f"out of date: {target}", file=sys.stderr)
                failed = True
        else:
            output_dir.mkdir(parents=True, exist_ok=True)
            target.write_text(expected, encoding="utf-8")
            print(f"generated {target}")

    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
