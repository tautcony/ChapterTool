"""Run the host checks shared by local development and GitHub Actions."""

from __future__ import annotations

import argparse
from dataclasses import dataclass, field
import os
from pathlib import Path
import platform
import re
import shutil
import subprocess
import sys
import time
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parent.parent
NODE = "packages/chaptertool"
E2E = "tests/ChapterTool.Wasm.E2E"
STAGES = ("resources", "dotnet", "test-dotnet", "pack-nuget", "node", "build-test", "browser", "pack-node", "pack-desktop")
DEFAULT_STAGES = ("resources", "dotnet", "node", "browser", "pack-node", "pack-desktop")


@dataclass
class Step:
    name: str
    args: list[str]
    cwd: str = "."
    env: dict[str, str] = field(default_factory=dict)

    @property
    def key(self) -> str:
        return re.sub(r"[^a-z0-9]+", "-", self.name.lower()).strip("-")


def dotnet_tests(root: Path) -> list[str]:
    projects = ET.parse(root / "ChapterTool.slnx").iter("Project")
    return [p.attrib["Path"].replace("\\", "/") for p in projects
            if p.attrib["Path"].replace("\\", "/").startswith("tests/")
            and p.attrib["Path"].endswith(".Tests.csproj")]


def resource_steps() -> list[Step]:
    return [
        Step("Sync locked Python tools", ["uv", "sync", "--locked", "--no-build", "--no-install-project", "--project", "scripts"]),
        Step("Verify localization resources", ["uv", "run", "--no-sync", "--project", "scripts", "python", "scripts/axaml-to-json.py", "--check"]),
        Step("Lint Python scripts", ["uv", "run", "--no-sync", "--project", "scripts", "ruff", "check", "scripts/"]),
        Step("Test CI runner", [sys.executable, "-m", "unittest", "discover", "-s", "scripts/tests", "-v"]),
        Step("Validate PowerShell publish script", ["pwsh", "-NoLogo", "-NoProfile", "-NonInteractive", "-File", "scripts/ci/check-powershell.ps1"]),
    ]


def dotnet_steps(root: Path) -> list[Step]:
    steps = [
        Step("Restore", ["dotnet", "restore", "ChapterTool.slnx"]),
        Step("Build Release", ["dotnet", "build", "ChapterTool.slnx", "--configuration", "Release", "--no-restore"]),
    ]
    for project, output in (("ChapterTool.Core", "nuget"), ("ChapterTool.CommandLine", "cli-nuget")):
        steps.append(Step(f"Pack {project}", ["dotnet", "pack", f"src/{project}/{project}.csproj", "--configuration", "Release", "--no-build", "--no-restore", "--output", f"artifacts/{output}", "/p:ContinuousIntegrationBuild=true"]))
    steps.append(Step("Verify NuGet consumers", [sys.executable, "scripts/ci/verify-nuget.py"]))
    for project in dotnet_tests(root):
        steps.append(Step(f"Test {Path(project).stem}", ["dotnet", "test", project, "--configuration", "Release", "--no-build", "--no-restore", "--timeout", "10m"]))
    return steps


def test_dotnet_steps(root: Path, project: str | None) -> list[Step]:
    projects = {Path(path).stem: path for path in dotnet_tests(root)}
    if project not in projects:
        raise ValueError("--stage test-dotnet requires --test-project from ChapterTool.slnx.")
    path = projects[project]
    return [
        Step("Restore test project", ["dotnet", "restore", path]),
        Step("Build test project", ["dotnet", "build", path, "--configuration", "Release", "--no-restore"]),
        Step("Run tests", ["dotnet", "test", path, "--configuration", "Release", "--no-build", "--no-restore", "--timeout", "10m"]),
    ]


def nuget_steps(version: str | None) -> list[Step]:
    if version and not re.fullmatch(r"\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?", version):
        raise ValueError("Invalid NuGet package version.")
    properties = [f"-p:Version={version}", f"-p:PackageVersion={version}"] if version else []
    steps = []
    for project, output in (("ChapterTool.Core", "nuget"), ("ChapterTool.CommandLine", "cli-nuget")):
        path = f"src/{project}/{project}.csproj"
        steps.extend([
            Step(f"Restore {project}", ["dotnet", "restore", path]),
            Step(f"Build {project}", ["dotnet", "build", path, "--configuration", "Release", "--no-restore", *properties]),
            Step(f"Pack {project}", ["dotnet", "pack", path, "--configuration", "Release", "--no-build", "--no-restore", "--output", f"artifacts/{output}", *properties, "/p:ContinuousIntegrationBuild=true"]),
        ])
    steps.append(Step("Verify NuGet consumers", [sys.executable, "scripts/ci/verify-nuget.py"]))
    return steps


def node_steps() -> list[Step]:
    return [
        Step("Install npm dependencies", ["npm", "ci", "--ignore-scripts"], NODE),
        Step("Build Node.js package", ["npm", "run", "build"], NODE),
        Step("Type check Node.js package", ["npm", "run", "typecheck"], NODE),
        Step("Test built Node.js package", ["npm", "run", "test:built"], NODE),
    ]


def build_steps(root: Path) -> list[Step]:
    return resource_steps() + dotnet_steps(root) + node_steps()


def browser_steps(suite: str, install_deps: bool, engine: str | None = None) -> list[Step]:
    project = "src/ChapterTool.Wasm/ChapterTool.Wasm.csproj"
    steps = [
        Step("Restore WASM", ["dotnet", "restore", project]),
        Step("Publish Release WASM", ["dotnet", "publish", project, "--configuration", "Release", "--no-restore", "--output", "artifacts/wasm-e2e/publish", "-p:WasmBuildNative=false", "-p:WasmRunWasmOpt=false"]),
        Step("Install browser test dependencies", ["npm", "ci"], E2E),
        Step("Prepare Pages-shaped site", ["npm", "run", "prepare:site"], E2E, {"CHAPTERTOOL_E2E_PUBLISH": "artifacts/wasm-e2e/publish/wwwroot"}),
        Step("Type check browser tests", ["npm", "run", "typecheck"], E2E),
    ]
    engines = ["chromium", "webkit"] if suite == "pr" else ["chromium"]
    if suite == "full":
        engines = ["chromium", "firefox", "webkit"]
    if engine:
        if engine not in engines:
            raise ValueError(f"{engine} is not part of the {suite} browser suite.")
        engines = [engine]
    steps.append(Step("Install browser engines", ["npm", "exec", "--", "playwright", "install", *(["--with-deps"] if install_deps else []), *engines], E2E))
    args = ["npm", "run", "test:e2e", "--"]
    if suite == "full":
        steps.append(Step("Run full browser acceptance", [*args, *([f"--project={engine}"] if engine else [])], E2E, {"E2E_RUN_NAME": engine} if engine else {}))
    elif "chromium" in engines:
        steps.append(Step("Run Chromium E2E", [*args, "--project=chromium"], E2E))
    if suite == "pr" and "webkit" in engines:
        steps.append(Step("Run WebKit regressions", [*args, "--project=webkit", "modal-layout.spec.ts", "unified-editing.spec.ts", "layout-behavior.spec.ts"], E2E, {"E2E_RUN_NAME": "webkit"}))
    return steps


def bash_executable() -> str:
    if os.name == "nt":
        # Avoid System32/bash.exe, which starts WSL instead of Git Bash.
        git = shutil.which("git")
        if git:
            for candidate in (Path(git).parent.parent / "bin/bash.exe", Path(git).parent / "bash.exe"):
                if candidate.is_file():
                    return str(candidate)
        raise ValueError("Linux cross-publishing on Windows requires Git for Windows with Git Bash.")
    return "bash"


def desktop_steps(runtimes: list[str]) -> list[Step]:
    steps = []
    project = "src/ChapterTool.Avalonia/ChapterTool.Avalonia.csproj"
    for runtime in runtimes:
        if runtime == "osx-arm64" and platform.system() != "Darwin":
            raise ValueError("macOS DMG packaging requires a macOS host.")
        steps.append(Step(f"Restore desktop {runtime}", ["dotnet", "restore", project, "--runtime", runtime]))
        command = [bash_executable(), "scripts/publish.sh"] if os.name != "nt" or runtime != "win-x64" else ["pwsh", "-NoLogo", "-NoProfile", "-NonInteractive", "-File", "scripts/publish.ps1"]
        steps.append(Step(f"Publish desktop {runtime}", [*command, "-Configuration", "Release", "-Runtime", runtime, "-NoRestore", "-PublishSingleFile"]))
    return steps


def plan(options: argparse.Namespace, root: Path) -> list[Step]:
    stages = DEFAULT_STAGES if options.stage == "all" else (options.stage,)
    steps = []
    for stage in stages:
        if stage == "resources":
            steps.extend(resource_steps())
        elif stage == "dotnet":
            steps.extend(dotnet_steps(root))
        elif stage == "test-dotnet":
            steps.extend(test_dotnet_steps(root, options.test_project))
        elif stage == "pack-nuget":
            steps.extend(nuget_steps(options.package_version))
        elif stage == "node":
            steps.extend(node_steps())
        elif stage == "build-test":
            steps.extend(build_steps(root))
        elif stage == "browser":
            steps.extend(browser_steps(options.browser_suite, options.install_browser_deps, options.browser_engine))
        elif stage == "pack-node":
            steps.append(Step("Pack and verify npm tarball", ["npm", "run", "pack:verify"], NODE))
        elif stage == "pack-desktop":
            runtimes = options.runtime or (["osx-arm64"] if platform.system() == "Darwin" else ["win-x64", "linux-x64"])
            steps.extend(desktop_steps(runtimes))
    if options.step:
        unknown = set(options.step) - {step.key for step in steps}
        if unknown:
            raise ValueError(f"Unknown step(s) for {options.stage}: {', '.join(sorted(unknown))}. Use --plan to list step keys.")
        steps = [step for step in steps if step.key in options.step]
    return steps


def tool_environment() -> dict[str, str]:
    environment = dict(os.environ)
    if platform.system() == "Windows":
        for name in ("ProgramFiles", "ProgramFiles(x86)", "LOCALAPPDATA"):
            root = os.environ.get(name)
            if not root:
                continue
            directory = Path(root) / "MKVToolNix"
            if all((directory / f"{tool}.exe").is_file() for tool in ("mkvextract", "mkvmerge")):
                environment["PATH"] = os.pathsep.join((environment.get("PATH", ""), str(directory)))
                break
    return environment


def run_step(step: Step, root: Path, dry_run: bool = False) -> None:
    print(f"\n[{step.key}] {step.name} ({step.cwd})\n  {subprocess.list2cmdline(step.args)}", flush=True)
    if dry_run:
        return
    if step.key == "publish-release-wasm":
        output = (root / "artifacts/wasm-e2e/publish").resolve()
        if output != root.resolve() / "artifacts/wasm-e2e/publish":
            raise ValueError("WASM publish output must stay in the workspace artifact directory.")
        if output.exists():
            shutil.rmtree(output)
    if step.key in ("pack-chaptertool-core", "pack-chaptertool-commandline"):
        directory = root / ("artifacts/nuget" if step.key == "pack-chaptertool-core" else "artifacts/cli-nuget")
        for pattern in ("*.nupkg", "*.snupkg"):
            for package in directory.glob(pattern):
                package.unlink()
    args = step.args.copy()
    args[0] = shutil.which(args[0]) or args[0]
    environment = {**tool_environment(), "CI": "true", "BINARYEN_CORES": "1", **step.env}
    subprocess.run(args, cwd=root / step.cwd, env=environment, check=True, timeout=900)


def preflight(options: argparse.Namespace) -> None:
    steps = plan(options, ROOT)
    commands = {step.args[0] for step in steps}
    if "npm" in commands:
        version = subprocess.check_output(["node", "--version"], text=True, timeout=30).strip()
        if not version.startswith("v22."):
            raise ValueError(f"CI requires Node.js 22.x; found {version}.")
    if "dotnet" in commands or any(step.key.startswith(("build-node", "publish-desktop")) for step in steps):
        sdk = subprocess.check_output(["dotnet", "--version"], text=True, timeout=30).strip()
        if not sdk.startswith("10."):
            raise ValueError(f"CI requires .NET SDK 10.x; found {sdk}.")
    required = commands.copy()
    if any(step.key == "test-chaptertool-infrastructure-tests" or
           (step.key == "run-tests" and options.test_project == "ChapterTool.Infrastructure.Tests") for step in steps):
        required.update(("ffmpeg", "ffprobe", "mkvextract", "mkvmerge"))
    environment = tool_environment()
    missing = sorted(name for name in required if not shutil.which(name, path=environment.get("PATH")))
    if missing:
        raise ValueError(f"Missing CI tools on PATH: {', '.join(missing)}. See scripts/README.md for setup.")
    if any(step.key in ("build-release", "build-node-js-package") for step in steps):
        workloads = subprocess.check_output(["dotnet", "workload", "list"], encoding="utf-8", timeout=30)
        if "wasm-tools" not in workloads:
            raise ValueError("CI requires wasm-tools. Run 'dotnet workload install wasm-tools' once, then retry.")
    if options.stage == "pack-node" and not (ROOT / NODE / "dist/index.mjs").is_file():
        raise ValueError("pack-node needs the dist output from --stage node or --stage build-test.")


def parser() -> argparse.ArgumentParser:
    result = argparse.ArgumentParser(description=__doc__)
    result.add_argument("--stage", choices=("all", *STAGES), default="all")
    result.add_argument("--step", action="append", help="Run a step key from --plan. Dependencies must already be prepared. May be repeated.")
    result.add_argument("--test-project", help="Select one solution test project by name for --stage test-dotnet.")
    result.add_argument("--package-version", help="Version to build and pack for --stage pack-nuget.")
    result.add_argument("--browser-suite", choices=("chromium", "pr", "full"), default="pr")
    result.add_argument("--browser-engine", choices=("chromium", "firefox", "webkit"), help="Select one engine from the browser suite for an isolated CI job.")
    result.add_argument("--runtime", action="append", choices=("win-x64", "linux-x64", "osx-arm64"))
    result.add_argument("--install-browser-deps", action="store_true", help="Install Playwright system packages on Linux.")
    result.add_argument("--plan", action="store_true", help="Print commands without running checks.")
    return result


def main(arguments: list[str] | None = None) -> int:
    options = parser().parse_args(arguments)
    started = time.monotonic()
    current_step = "prerequisites"
    try:
        steps = plan(options, ROOT)
        if not options.plan:
            preflight(options)
        for step in steps:
            current_step = step.key
            run_step(step, ROOT, options.plan)
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        print(f"\nCI check failed [{current_step}]: {error}", file=sys.stderr, flush=True)
        if os.environ.get("GITHUB_ACTIONS") == "true":
            message = str(error).replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")
            print(f"::error title=CI check {current_step}::{message}", flush=True)
        return 1
    print(f"\n{'Plan printed' if options.plan else 'Selected host CI checks passed'} ({time.monotonic() - started:.1f}s).", flush=True)
    if options.stage in ("all", "pack-desktop") and platform.system() != "Darwin":
        print("macOS DMG must still be verified on a macOS host.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
