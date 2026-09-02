#!/usr/bin/env python3
"""Normalize line endings and UTF-8 BOMs in changed text files."""
import argparse
import subprocess
from pathlib import Path

EXTENSIONS = {".cs", ".md", ".axaml", ".json", ".xml", ".yml", ".yaml", ".props", ".targets", ".sln", ".slnx", ".sh", ".ps1", ".py", ".txt", ".csv", ".tsv"}
NAMES = {".editorconfig", ".gitattributes"}

def git_paths(root, *args):
    result = subprocess.run(["git", *args], cwd=root, check=True, capture_output=True, text=True)
    return {line.strip() for line in result.stdout.splitlines() if line.strip()}

def explicit_paths(root, values):
    candidates = set()
    for value in values:
        path = (root / value).resolve()
        if not path.exists():
            print(f"Warning: Path not found: {value}")
            continue
        candidates.update(str(item.relative_to(root)).replace("\\", "/") for item in (path.rglob("*") if path.is_dir() else [path]) if item.is_file())
    return candidates

def normalize_candidates(root, candidates, what_if):
    normalized, skipped = [], []
    for relative in sorted(candidates):
        path = root / relative
        if path.suffix.lower() not in EXTENSIONS and path.name.lower() not in NAMES:
            skipped.append(relative)
            continue
        if not path.is_file():
            continue
        raw = path.read_bytes()
        text = raw.decode("utf-8-sig")
        if not raw.startswith(b"\xef\xbb\xbf") and "\r" not in text:
            continue
        normalized.append(relative)
        if not what_if:
            path.write_text(text.replace("\r\n", "\n").replace("\r", "\n"), encoding="utf-8", newline="\n")
    return normalized, skipped

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("paths", nargs="*")
    parser.add_argument("-IncludeUntracked", "--include-untracked", dest="include_untracked", action="store_true")
    parser.add_argument("-WhatIf", "--what-if", dest="what_if", action="store_true")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    candidates = explicit_paths(root, args.paths)
    candidates.update(git_paths(root, "diff", "--name-only"))
    candidates.update(git_paths(root, "diff", "--cached", "--name-only"))
    if args.include_untracked:
        candidates.update(git_paths(root, "ls-files", "--others", "--exclude-standard"))
    normalized, skipped = normalize_candidates(root, candidates, args.what_if)
    print("Normalized files:\n" + "\n".join(f"  {p}" for p in normalized) if normalized else "No changed text files required normalization.")
    if skipped:
        print("Skipped non-text paths:\n" + "\n".join(f"  {p}" for p in skipped))

if __name__ == "__main__":
    raise SystemExit(main())
