#!/usr/bin/env python3
"""Build tests sequentially and collect Cobertura coverage."""
import argparse
import shutil
import subprocess
from pathlib import Path

def run(args):
    subprocess.run(args, check=True)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("-Configuration", default="Release")
    parser.add_argument("-NoRestore", action="store_true")
    parser.add_argument("-NoBuild", action="store_true")
    parser.add_argument("-SkipHtml", action="store_true")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    output = root / "artifacts/coverage"; results = output / "test-results"; report_dir = output / "html"
    runsettings = root / "scripts/coverage.runsettings"
    tests = [root / p for p in ("tests/ChapterTool.Core.Tests/ChapterTool.Core.Tests.csproj", "tests/ChapterTool.Infrastructure.Tests/ChapterTool.Infrastructure.Tests.csproj", "tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj", "tests/ChapterTool.Avalonia.Headless.Tests/ChapterTool.Avalonia.Headless.Tests.csproj")]
    builds = [root / p for p in ("src/ChapterTool.Core/ChapterTool.Core.csproj", "src/ChapterTool.Infrastructure/ChapterTool.Infrastructure.csproj", "src/ChapterTool.Avalonia/ChapterTool.Avalonia.csproj")] + tests
    if shutil.which("dotnet") is None: raise RuntimeError("dotnet was not found on PATH")
    if not runsettings.is_file(): raise FileNotFoundError(f"coverage settings were not found at {runsettings}")
    for project in tests:
        if not project.is_file(): raise FileNotFoundError(f"test project was not found at {project}")
    shutil.rmtree(output, ignore_errors=True); results.mkdir(parents=True)
    if not args.NoBuild:
        for project in builds:
            command = ["dotnet", "build", str(project), "--configuration", args.Configuration, "-p:GenerateRuntimeConfigurationFiles=true", "-p:ProduceReferenceAssembly=true", "-p:GenerateReferenceAssembly=true"]
            if project in tests: command.append("--no-dependencies")
            if args.NoRestore: command.append("--no-restore")
            print(f"Building {project}"); run(command)
    for project in tests:
        command = ["dotnet", "test", str(project), "--configuration", args.Configuration, "--no-build", "--collect:XPlat Code Coverage", "--settings", str(runsettings), "--results-directory", str(results)]
        if args.NoRestore or not args.NoBuild: command.append("--no-restore")
        print(f"Collecting coverage from {project}"); run(command)
    coverage = list(results.rglob("coverage.cobertura.xml"))
    if not coverage: raise RuntimeError("no coverage.cobertura.xml file was produced")
    print("Coverage XML files:\n" + "\n".join(f"  {p}" for p in coverage))
    if args.SkipHtml: print("Skipped HTML report generation."); return 0
    if shutil.which("reportgenerator") is None:
        print("HTML report skipped: reportgenerator was not found on PATH.\nInstall it with: dotnet tool install -g dotnet-reportgenerator-globaltool\nOr rerun with -SkipHtml to intentionally produce XML only."); return 0
    report_dir.mkdir(parents=True, exist_ok=True)
    run(["reportgenerator", f"-reports:{results}/**/coverage.cobertura.xml", f"-targetdir:{report_dir}", "-filefilters:-*/obj/*;-*.g.cs", "-reporttypes:Html"])
    print(f"HTML coverage report: {report_dir / 'index.html'}")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
