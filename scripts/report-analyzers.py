#!/usr/bin/env python3
"""Build C# projects sequentially and summarize compiler SARIF diagnostics."""
import argparse
import json
import os
import re
import shutil
import subprocess
import urllib.parse
from collections import Counter
from pathlib import Path


def load_sarif(path):
    # The compiler appends one SARIF document per target framework, so plain
    # json.load fails on multi-target projects; decode them in sequence.
    decoder = json.JSONDecoder()
    text = path.read_text(encoding="utf-8")
    runs = []
    index = 0
    while True:
        while index < len(text) and text[index].isspace():
            index += 1
        if index >= len(text):
            break
        obj, end = decoder.raw_decode(text, index)
        index = end
        runs.extend(obj.get("runs", []))
    return {"version": "1.0.0", "runs": runs}


def result_location(item):
    # SARIF 1.0.0 stores the location in resultFile; 2.1.0 uses physicalLocation.
    loc = (item.get("locations") or [{}])[0]
    region = {}
    if "resultFile" in loc:
        file = (loc["resultFile"] or {}).get("uri") or ""
        region = (loc["resultFile"] or {}).get("region") or {}
    else:
        phys = loc.get("physicalLocation") or {}
        file = (phys.get("artifactLocation") or {}).get("uri") or ""
        region = phys.get("region") or {}
    return file, region.get("startLine", 0), region.get("startColumn", 0)


def uri_to_path(uri):
    parsed = urllib.parse.urlparse(uri)
    if parsed.scheme and parsed.scheme != "file":
        return uri
    return urllib.parse.unquote(parsed.path)


_TYPE_RE = re.compile(r"\b(?:class|struct|interface|record|enum)\s+([A-Za-z_][A-Za-z0-9_]*)")


class _SourceMasker:
    """Mask comments and string literals, preserving character positions."""

    _BLOCK_START = re.compile(r"/\*")
    _BLOCK_END = re.compile(r"\*/")
    _MASK = re.compile(
        r"//[^\n]*"
        r"|/\*.*?\*/"
        r'|@"(?:""|[^"])*"'
        r'|"(?:\\.|[^"\\\n])*"'
        r"|\'(?:\\.|[^\'\\\n])\'"
    )

    def __init__(self):
        self.in_block_comment = False

    def __call__(self, line):
        if self.in_block_comment:
            end = _SourceMasker._BLOCK_END.search(line)
            if end is None:
                return " " * len(line)
            self.in_block_comment = False
            line = " " * (end.start() + 2) + line[end.end():]
        masked = _SourceMasker._MASK.sub(lambda m: " " * len(m.group()), line)
        open_ = _SourceMasker._BLOCK_START.search(masked)
        if open_:
            self.in_block_comment = True
            masked = masked[:open_.start()] + " " * (len(masked) - open_.start())
        return masked


_type_cache: dict = {}


def enclosing_type(source_path, target_line):
    # SARIF carries no enclosing type; scan the source, track brace depth, and
    # pick the innermost type whose body spans the diagnostic line.
    if source_path not in _type_cache:
        try:
            lines = Path(source_path).read_text(encoding="utf-8").splitlines()
        except OSError:
            return ""
        masker = _SourceMasker()
        depth_after = [0] * (len(lines) + 1)
        types = []
        depth = 0
        for i, line in enumerate(lines, 1):
            masked = masker(line)
            match = _TYPE_RE.search(masked)
            if match:
                types.append((match.group(1), i, depth, "{" in masked[match.end():]))
            depth += masked.count("{") - masked.count("}")
            depth_after[i] = depth
        _type_cache[source_path] = (depth_after, types)
    depth_after, types = _type_cache[source_path]
    if target_line < 1 or target_line > len(depth_after) - 1:
        return ""
    best, best_depth = "", -1
    for name, decl, d, body_on_decl in types:
        if target_line < decl:
            continue
        if body_on_decl:
            close = decl if depth_after[decl] == d else next((k for k in range(decl + 1, len(depth_after)) if depth_after[k] == d), len(depth_after) - 1)
        else:
            open_at = next((k for k in range(decl + 1, len(depth_after)) if depth_after[k] > d), None)
            if open_at is None:
                continue
            close = next((k for k in range(open_at + 1, len(depth_after)) if depth_after[k] == d), len(depth_after) - 1)
        if target_line > close:
            continue
        if d > best_depth:
            best, best_depth = name, d
    return best


_SOURCES = ("src/ChapterTool.Contracts/ChapterTool.Contracts.csproj", "src/ChapterTool.Core/ChapterTool.Core.csproj", "src/ChapterTool.Infrastructure/ChapterTool.Infrastructure.csproj", "src/ChapterTool.CommandLine/ChapterTool.CommandLine.csproj", "src/ChapterTool.Avalonia.UI/ChapterTool.Avalonia.UI.csproj", "src/ChapterTool.Avalonia/ChapterTool.Avalonia.csproj")
_TESTS = ("tests/ChapterTool.TestSupport/ChapterTool.TestSupport.csproj", "tests/ChapterTool.Core.Tests/ChapterTool.Core.Tests.csproj", "tests/ChapterTool.Infrastructure.Tests/ChapterTool.Infrastructure.Tests.csproj", "tests/ChapterTool.CommandLine.Tests/ChapterTool.CommandLine.Tests.csproj", "tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj", "tests/ChapterTool.Avalonia.Headless.Tests/ChapterTool.Avalonia.Headless.Tests.csproj", "tests/ChapterTool.Wasm.Tests/ChapterTool.Wasm.Tests.csproj")


def build_projects(root, raw_dir, args):
    # Build each project once; per target framework: <stem>.<tfm>.sarif
    projects = [(root / p, False) for p in _SOURCES] + [(root / p, True) for p in _TESTS]
    for project, _ in projects:
        if not project.is_file():
            raise FileNotFoundError(f"project was not found at {project}")
    shutil.rmtree(raw_dir, ignore_errors=True)
    raw_dir.mkdir(parents=True)
    last_code = 0
    for project, is_test in projects:
        raw = raw_dir / project.stem
        for old in raw_dir.glob(f"{project.stem}.*.sarif"):
            old.unlink()
        command = ["dotnet", "build", str(project), "--configuration", args.Configuration, f"-p:ReportErrorLog={raw}"]
        if is_test:
            command.append("--no-dependencies")
        else:
            command.append("-p:BuildProjectReferences=false")
        if args.Rebuild:
            command.append("-t:Rebuild")
        if args.NoRestore:
            command.append("--no-restore")
        print(f"Building {project}")
        result = subprocess.run(command)
        last_code = result.returncode
        if result.returncode != 0:
            break
    return last_code


def message_text(item):
    message = item.get("message", "")
    if isinstance(message, dict):
        message = message.get("text") or message.get("markdown") or ""
    return str(message)


def resolve_location(item, root):
    # Returns (repo-relative file, enclosing type, line, column).
    uri, line, column = result_location(item)
    if not uri:
        return "<no-file>", "", line, column
    source_path = uri_to_path(uri)
    file = os.path.relpath(source_path, root) if source_path.startswith(str(root)) else source_path
    return file, enclosing_type(source_path, line), line, column


def collect_diagnostics(raw_dir, args, root):
    runs = []
    for raw in sorted(raw_dir.glob("*.sarif")):
        runs.extend(load_sarif(raw).get("runs", []))
    diagnostics = []
    seen = set()
    for run in runs:
        for item in run.get("results", []):
            rule = item.get("ruleId") or "<unknown-rule>"
            if args.Prefix and not rule.startswith(args.Prefix):
                continue
            message = message_text(item)
            file, type_name, line, column = resolve_location(item, root)
            key = (rule, file, line, column, message)
            if key in seen:
                continue
            seen.add(key)
            diagnostics.append({"ruleId": rule, "level": item.get("level") or "none", "message": message, "file": file, "line": line, "column": column, "type": type_name})
    return diagnostics


def print_report(diagnostics, report, prefix, last_code):
    report.parent.mkdir(parents=True, exist_ok=True)
    report.write_text(json.dumps({"version": "1.0.0", "runs": [{"results": diagnostics}]}) + "\n", encoding="utf-8")
    print()
    if prefix:
        print(f"Diagnostics with rule prefix '{prefix}': {len(diagnostics)}")
    else:
        print(f"Diagnostics: {len(diagnostics)}")
    print(f"SARIF report: {report}")
    if not diagnostics:
        print("No matching diagnostics were found.")
        return last_code
    for title, key in (("By rule", "ruleId"), ("By severity", "level"), ("By file", "file"), ("By type", "type")):
        print(f"\n{title}")
        for value, count in sorted(Counter(item[key] for item in diagnostics).items()):
            print(f"  {value or '<unknown>'} {count}")
    print("\nDiagnostics")
    for item in diagnostics:
        target = item['type'] or "<unknown>"
        print(f"  {item['ruleId']} [{item['level']}] {item['file']}:{item['line']}:{item['column']} {target} :: {item['message']}")
    return last_code if last_code else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("-Configuration", default="Release")
    parser.add_argument("-NoRestore", action="store_true")
    parser.add_argument("-Prefix", default="")
    parser.add_argument("-Output", default="")
    parser.add_argument("-Rebuild", action="store_true", help="force a full rebuild so diagnostics are regenerated even when sources are up to date")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    raw_dir = root / "artifacts/analyzers/raw"
    report = Path(args.Output) if args.Output else root / "artifacts/analyzers/analyzers.sarif"
    if shutil.which("dotnet") is None:
        raise RuntimeError("dotnet was not found on PATH")
    last_code = build_projects(root, raw_dir, args)
    diagnostics = collect_diagnostics(raw_dir, args, root)
    return print_report(diagnostics, report, args.Prefix, last_code)


if __name__ == "__main__":
    raise SystemExit(main())
