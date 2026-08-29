#!/usr/bin/env python3
"""Build tests sequentially and collect Cobertura coverage."""
import argparse
import os
import shutil
import subprocess
import sys
import defusedxml.ElementTree as ET
from pathlib import Path


def run(args):
    subprocess.run(args, check=True)


def build_projects(args, builds, tests):
    for project in builds:
        command = ["dotnet", "build", str(project), "--configuration", args.Configuration, "-p:GenerateRuntimeConfigurationFiles=true", "-p:ProduceReferenceAssembly=true", "-p:GenerateReferenceAssembly=true"]
        if project in tests:
            command.append("--no-dependencies")
        if args.NoRestore:
            command.append("--no-restore")
        print(f"Building {project}")
        run(command)


def coverlet_adapter(project):
    # The collector version comes from the project's PackageReference.
    collector = next((ref for ref in ET.parse(project).getroot().iter("PackageReference") if ref.get("Include") == "coverlet.collector"), None)
    if collector is None or not collector.get("Version"):
        raise RuntimeError(f"coverlet.collector is not declared with a version in {project}")
    package_root = Path(os.environ.get("NUGET_PACKAGES", Path.home() / ".nuget" / "packages"))
    return package_root / "coverlet.collector" / collector.get("Version") / "build" / "net10.0"


def collect_coverage(args, tests, runsettings, results):
    for project in tests:
        assembly = project.parent / "bin" / args.Configuration / "net10.0" / f"{project.stem}.dll"
        adapter_path = coverlet_adapter(project)
        if not assembly.is_file():
            raise FileNotFoundError(f"test assembly was not found at {assembly}")
        if not adapter_path.is_dir():
            raise FileNotFoundError(f"Coverlet adapter was not found at {adapter_path}")
        # global.json opts dotnet test into Microsoft.Testing.Platform, which does not
        # load Coverlet's VSTest data collector. Run the built module through vstest.
        command = ["dotnet", "vstest", str(assembly), "--collect:XPlat Code Coverage", f"/TestAdapterPath:{adapter_path}", f"/Settings:{runsettings}", f"/ResultsDirectory:{results}"]
        print(f"Collecting coverage from {project}")
        run(command)


REPORTGENERATOR_PACKAGE = "dotnet-reportgenerator-globaltool"


def find_reportgenerator():
    command = shutil.which("reportgenerator")
    if command:
        return command

    tools_dir = Path.home() / ".dotnet" / "tools"
    for name in ("reportgenerator", "reportgenerator.exe"):
        candidate = tools_dir / name
        if candidate.is_file():
            return str(candidate)
    return None


def prepare_html_report(skip_html):
    if skip_html:
        return None

    command = find_reportgenerator()
    if command:
        return command

    print("reportgenerator was not found. HTML coverage requires the .NET global tool.")
    if not sys.stdin.isatty():
        print("HTML report skipped in a non-interactive session. Rerun with -SkipHtml to intentionally produce XML only.")
        return None

    try:
        answer = input(f"Install {REPORTGENERATOR_PACKAGE} now? [Y/n]: ").strip().lower()
    except EOFError:
        answer = "n"
    if answer not in ("", "y", "yes"):
        print("HTML report skipped. Rerun without -SkipHtml after installing reportgenerator.")
        return None

    run(["dotnet", "tool", "install", "-g", REPORTGENERATOR_PACKAGE])
    command = find_reportgenerator()
    if command is None:
        print("HTML report skipped: reportgenerator was installed but is not available yet. Restart the shell and rerun the script.")
    return command


def write_html_report(results, report_dir, command):
    if command is None:
        return
    report_dir.mkdir(parents=True, exist_ok=True)
    run([command, f"-reports:{results}/**/coverage.cobertura.xml", f"-targetdir:{report_dir}", "-filefilters:-*/obj/*;-*.g.cs", "-reporttypes:Html"])
    print(f"HTML coverage report: {report_dir / 'index.html'}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("-Configuration", default="Release")
    parser.add_argument("-NoRestore", action="store_true")
    parser.add_argument("-NoBuild", action="store_true")
    parser.add_argument("-SkipHtml", action="store_true")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    output = root / "artifacts/coverage"
    results = output / "test-results"
    report_dir = output / "html"
    runsettings = root / "scripts/coverage.runsettings"
    tests = [root / p for p in ("tests/ChapterTool.Core.Tests/ChapterTool.Core.Tests.csproj", "tests/ChapterTool.Infrastructure.Tests/ChapterTool.Infrastructure.Tests.csproj", "tests/ChapterTool.CommandLine.Tests/ChapterTool.CommandLine.Tests.csproj", "tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj", "tests/ChapterTool.Avalonia.Headless.Tests/ChapterTool.Avalonia.Headless.Tests.csproj")]
    builds = [root / p for p in ("src/ChapterTool.Core/ChapterTool.Core.csproj", "src/ChapterTool.Infrastructure/ChapterTool.Infrastructure.csproj", "src/ChapterTool.CommandLine/ChapterTool.CommandLine.csproj", "src/ChapterTool.Avalonia/ChapterTool.Avalonia.csproj")] + tests
    if shutil.which("dotnet") is None:
        raise RuntimeError("dotnet was not found on PATH")
    if not runsettings.is_file():
        raise FileNotFoundError(f"coverage settings were not found at {runsettings}")
    for project in tests:
        if not project.is_file():
            raise FileNotFoundError(f"test project was not found at {project}")
    html_report_command = prepare_html_report(args.SkipHtml)
    shutil.rmtree(output, ignore_errors=True)
    results.mkdir(parents=True)
    if not args.NoBuild:
        build_projects(args, builds, tests)
    collect_coverage(args, tests, runsettings, results)
    coverage = list(results.rglob("coverage.cobertura.xml"))
    if not coverage:
        raise RuntimeError("no coverage.cobertura.xml file was produced")
    print("Coverage XML files:\n" + "\n".join(f"  {p}" for p in coverage))
    if args.SkipHtml:
        print("Skipped HTML report generation.")
        return 0
    write_html_report(results, report_dir, html_report_command)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
