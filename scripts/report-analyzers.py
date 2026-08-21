#!/usr/bin/env python3
"""Build the solution and summarize compiler SARIF diagnostics."""
import argparse
import json
import shutil
import subprocess
from collections import Counter
from pathlib import Path

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("-Configuration", default="Release")
    parser.add_argument("-NoRestore", action="store_true")
    parser.add_argument("-Prefix", default="")
    parser.add_argument("-Output", default="")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]; solution = root / "ChapterTool.slnx"; report = Path(args.Output) if args.Output else root / "artifacts/analyzers/analyzers.sarif"
    if shutil.which("dotnet") is None: raise RuntimeError("dotnet was not found on PATH")
    if not solution.is_file(): raise FileNotFoundError(f"solution was not found at {solution}")
    report.parent.mkdir(parents=True, exist_ok=True); report.unlink(missing_ok=True)
    command = ["dotnet", "build", str(solution), "--configuration", args.Configuration, f"-p:ErrorLog={report}"]
    if args.NoRestore: command.append("--no-restore")
    print(f"Building {solution}"); result = subprocess.run(command)
    if not report.is_file(): report.write_text(json.dumps({"version": "1.0.0", "runs": []}) + "\n", encoding="utf-8")
    data = json.loads(report.read_text(encoding="utf-8")); diagnostics = []
    for run in data.get("runs", []):
        for item in run.get("results", []):
            rule = item.get("ruleId") or "<unknown-rule>"
            if args.Prefix and not rule.startswith(args.Prefix): continue
            message = item.get("message", "")
            if isinstance(message, dict): message = message.get("text") or message.get("markdown") or ""
            location = (item.get("locations") or [{}])[0].get("physicalLocation") or {}
            artifact = location.get("artifactLocation") or {}; region = location.get("region") or {}
            diagnostics.append({"ruleId": rule, "level": item.get("level") or "none", "message": str(message), "file": artifact.get("uri") or "<no-file>", "line": region.get("startLine", 0), "column": region.get("startColumn", 0)})
    print(); print(f"Diagnostics with rule prefix '{args.Prefix}': {len(diagnostics)}" if args.Prefix else f"Diagnostics: {len(diagnostics)}"); print(f"SARIF report: {report}")
    if not diagnostics: print("No matching diagnostics were found."); return result.returncode
    for title, key in (("By rule", "ruleId"), ("By severity", "level"), ("By file", "file")):
        print(f"\n{title}")
        for value, count in sorted(Counter(item[key] for item in diagnostics).items()): print(f"  {value} {count}")
    print("\nDiagnostics")
    for item in diagnostics: print(f"  {item['ruleId']} [{item['level']}] {item['file']}:{item['line']}:{item['column']} {item['message']}")
    return result.returncode

if __name__ == "__main__":
    raise SystemExit(main())
