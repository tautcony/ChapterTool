"""Resolve a successful tag CI run for the exact checked-out release commit."""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys


ROOT = Path(__file__).resolve().parents[2]


def gh_json(*arguments: str):
    return json.loads(subprocess.check_output(["gh", *arguments], cwd=ROOT, text=True, encoding="utf-8", timeout=60))


def validate_run(run: dict, repository: str, tag: str, sha: str) -> None:
    expected = {"event": "push", "status": "completed", "conclusion": "success", "head_branch": tag, "head_sha": sha}
    if any(run.get(key) != value for key, value in expected.items()):
        raise ValueError("Release CI must be a successful tag push for the checked-out commit.")
    if run.get("head_repository", {}).get("full_name") != repository or run.get("repository", {}).get("full_name") != repository:
        raise ValueError("Release CI must belong to this repository.")
    if run.get("path", "").split("@", 1)[0] != ".github/workflows/dotnet-ci.yml":
        raise ValueError("Release artifacts must come from dotnet-ci.yml.")


def resolve_run(repository: str, tag: str, sha: str, run_id: str | None = None) -> int:
    if not re.fullmatch(r"v\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?", tag):
        raise ValueError("Expected a version tag such as v23.3.2 or v23.3.2-rc.1.")
    if not run_id:
        candidates = gh_json("run", "list", "--repo", repository, "--workflow", "dotnet-ci.yml", "--branch", tag,
                             "--commit", sha, "--event", "push", "--status", "success", "--limit", "100", "--json", "databaseId")
        if not candidates:
            raise ValueError(f"No successful tag CI run found for {tag} at {sha}.")
        run_id = str(candidates[0]["databaseId"])
    if not run_id.isdecimal():
        raise ValueError("CI run ID must be an integer.")
    run = gh_json("api", f"repos/{repository}/actions/runs/{run_id}")
    validate_run(run, repository, tag, sha)
    return int(run_id)


def main(arguments: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tag", required=True)
    parser.add_argument("--run-id", default=os.environ.get("TRIGGER_RUN_ID") or None)
    options = parser.parse_args(arguments)
    try:
        sha = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
        run_id = resolve_run(os.environ["GITHUB_REPOSITORY"], options.tag, sha, options.run_id)
        with Path(os.environ["GITHUB_OUTPUT"]).open("a", encoding="utf-8") as output:
            output.write(f"run-id={run_id}\n")
        print(f"Using tested CI artifacts from run {run_id} ({options.tag}, {sha}).")
    except (KeyError, OSError, ValueError, subprocess.SubprocessError) as error:
        print(f"Release CI resolution failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
