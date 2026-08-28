#!/usr/bin/env python3
"""Build tests sequentially and collect Cobertura coverage."""
import argparse
import os
import shutil
import subprocess
import xml.etree.ElementTree as ET
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
        assembly = project.parent / "bin" / args.Configuration / "net10.0" / f"{project.stem}.dll"
        root_element = ET.parse(project).getroot()
        collector = next((ref for ref in root_element.iter("PackageReference")
                          if ref.get("Include") == "coverlet.collector"), None)
        if collector is None or not collector.get("Version"):
            raise RuntimeError(f"coverlet.collector is not declared with a version in {project}")
        package_root = Path(os.environ.get("NUGET_PACKAGES", Path.home() / ".nuget" / "packages"))
        adapter_path = package_root / "coverlet.collector" / collector.get("Version") / "build" / "net10.0"
        if not assembly.is_file(): raise FileNotFoundError(f"test assembly was not found at {assembly}")
        if not adapter_path.is_dir(): raise FileNotFoundError(f"Coverlet adapter was not found at {adapter_path}")
        # global.json opts dotnet test into Microsoft.Testing.Platform, which does not
        # load Coverlet's VSTest data collector. Run the built module through vstest.
        command = ["dotnet", "vstest", str(assembly), "--collect:XPlat Code Coverage",
                   f"/TestAdapterPath:{adapter_path}", f"/Settings:{runsettings}",
                   f"/ResultsDirectory:{results}"]
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
