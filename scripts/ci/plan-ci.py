"""Select CI consumers from changed paths and verify their final job results."""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[2]
LOCALES = "src/ChapterTool.Avalonia.UI/Localization/Resources/Locales/"
JOBS = ("resources", "dotnet-tests", "solution-build", "wasm-browser-e2e", "pack-nuget", "pack-nodejs", "pack-dotnet")


def project_graph(root: Path) -> dict[str, set[str]]:
    paths = [p.attrib["Path"].replace("\\", "/") for p in ET.parse(root / "ChapterTool.slnx").iter("Project")]
    graph = {}
    for path in paths:
        file = root / path
        references = ET.parse(file).iter("ProjectReference")
        graph[path] = {
            (file.parent / p.attrib["Include"].replace("\\", "/")).resolve().relative_to(root.resolve()).as_posix()
            for p in references
        }
    for references in list(graph.values()):
        if not references <= graph.keys():
            raise ValueError("CI project references must be present in ChapterTool.slnx.")
    return graph


def dependencies(project: str, graph: dict[str, set[str]]) -> set[str]:
    found = set()
    pending = [project]
    while pending:
        current = pending.pop()
        if current not in found:
            found.add(current)
            pending.extend(graph[current])
    return found


def documentation(path: str) -> bool:
    return path.startswith(("docs/", "openspec/", ".agents/", ".codex/", ".codegraph/", "libbluray/")) or (
        "/" not in path and (path.endswith(".md") or path.startswith("LICENSE"))
    )


def select_jobs(root: Path, changed: list[str], full: bool = False, master: bool = False) -> dict:
    graph = project_graph(root)
    directories = {path: path.rsplit("/", 1)[0] + "/" for path in graph}
    substantive = [path for path in changed if not documentation(path)]
    known = (*directories.values(), "tests/ChapterTool.Wasm.E2E/", "packages/chaptertool/", LOCALES)
    # Unknown inputs, shared build configuration, and CI edits use the full gate.
    shared = any(not path.startswith(known) for path in substantive)
    # Browser, Node, and other .NET suites load Core fixtures outside ProjectReference.
    shared_fixtures = any(path.startswith("tests/ChapterTool.Core.Tests/Fixtures/") for path in substantive)
    all_consumers = full or shared or shared_fixtures or (master and bool(substantive))
    affected = {project for project, directory in directories.items() if any(path.startswith(directory) for path in substantive)}

    def consumes(project: str) -> bool:
        return all_consumers or bool(dependencies(project, graph) & affected)

    tests = [project for project in graph if project.startswith("tests/") and project.endswith(".Tests.csproj") and consumes(project)]
    browser = consumes("src/ChapterTool.Wasm/ChapterTool.Wasm.csproj") or any(
        path.startswith(("tests/ChapterTool.Wasm.E2E/", LOCALES)) for path in substantive
    )
    node = consumes("src/ChapterTool.Node/ChapterTool.Node.csproj") or any(path.startswith("packages/chaptertool/") for path in substantive)
    nuget = any(consumes(f"src/{name}/{name}.csproj") for name in ("ChapterTool.Core", "ChapterTool.CommandLine"))
    desktop_projects = dependencies("src/ChapterTool.Avalonia/ChapterTool.Avalonia.csproj", graph)
    desktop = full or (master and bool(substantive)) or shared or any(path in desktop_projects for path in substantive)
    selected = dict(zip(JOBS, (
        all_consumers or any(path.startswith(LOCALES) for path in substantive),
        bool(tests), full or shared, browser, nuget, node, desktop,
    )))
    return {
        "required": [job for job in JOBS if selected[job]],
        "jobs": selected,
        "tests": {"include": [{
            "project": Path(project).stem,
            "label": Path(project).stem.removeprefix("ChapterTool.").removesuffix(".Tests"),
            "media": Path(project).stem in ("ChapterTool.Infrastructure.Tests", "ChapterTool.Avalonia.Tests"),
        } for project in tests]},
    }


def changed_paths(root: Path, event_name: str, event: dict) -> list[str]:
    if event_name == "pull_request":
        base = event["pull_request"]["base"]["sha"]
        head = event["pull_request"]["head"]["sha"]
        revision = f"{base}...{head}"
    elif event_name == "push" and event.get("before", "").strip("0"):
        revisions = [event["before"], event["after"]]
    else:
        # New branches and missing comparison commits must not reduce coverage.
        return ["global.json"]
    if event_name == "pull_request":
        revisions = [revision]
    args = ["git", "diff", "--name-only", "--no-renames", "-z", *revisions, "--"]
    try:
        result = subprocess.check_output(args, cwd=root, stderr=subprocess.PIPE)
    except subprocess.CalledProcessError:
        # Force pushes can make the previous commit unavailable in a fresh checkout.
        return ["global.json"]
    return [path for path in result.decode("utf-8").split("\0") if path]


def verify_jobs(plan: dict, needs: dict) -> None:
    if needs.get("plan", {}).get("result") != "success":
        raise ValueError("CI planning did not succeed.")
    for job in JOBS:
        result = needs.get(job, {}).get("result")
        allowed = {"success"} if job in plan["required"] else {"skipped"}
        if result not in allowed:
            raise ValueError(f"CI job {job}: expected {sorted(allowed)}, received {result}.")


def main(arguments: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify", action="store_true")
    options = parser.parse_args(arguments)
    try:
        if options.verify:
            verify_jobs(json.loads(os.environ["CI_PLAN"]), json.loads(os.environ["CI_NEEDS"]))
            print("All required CI jobs passed.")
            return 0
        event_name = os.environ["GITHUB_EVENT_NAME"]
        event = json.loads(Path(os.environ["GITHUB_EVENT_PATH"]).read_text(encoding="utf-8"))
        full = event_name in ("schedule", "workflow_dispatch") or os.environ["GITHUB_REF"].startswith("refs/tags/")
        changed = [] if full else changed_paths(ROOT, event_name, event)
        plan = select_jobs(ROOT, changed, full, os.environ["GITHUB_REF"] == "refs/heads/master")
        print(json.dumps(plan, indent=2))
        with Path(os.environ["GITHUB_OUTPUT"]).open("a", encoding="utf-8") as output:
            output.write(f"plan={json.dumps(plan, separators=(',', ':'))}\n")
            output.write(f"tests={json.dumps(plan['tests'], separators=(',', ':'))}\n")
            for job, selected in plan["jobs"].items():
                output.write(f"{job}={str(selected).lower()}\n")
    except (KeyError, OSError, ValueError, subprocess.SubprocessError) as error:
        print(f"CI planning failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
