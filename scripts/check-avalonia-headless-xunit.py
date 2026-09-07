#!/usr/bin/env python3
"""Check the xUnit dependency declared by Avalonia.Headless.XUnit."""

from __future__ import annotations

import argparse
import io
import re
import sys
import urllib.request
import zipfile
from pathlib import Path
from xml.etree import ElementTree


PACKAGE = "Avalonia.Headless.XUnit"
NUGET_BASE = "https://api.nuget.org/v3-flatcontainer"
XUNIT_ID = "xunit.v3.extensibility.core"
VERSION_PATTERN = re.compile(r"^(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:[-+].*)?$")


def load_json(url: str) -> dict:
    with urllib.request.urlopen(url, timeout=15) as response:
        import json

        return json.load(response)


def version_key(version: str) -> tuple[int, int, int, int, str]:
    match = VERSION_PATTERN.match(version)
    if not match:
        return (0, 0, 0, 0, version)
    major, minor, patch = (int(value or 0) for value in match.groups()[:3])
    prerelease = 0 if "-" in version else 1
    return (major, minor, patch, prerelease, version)


def latest_stable_version() -> str:
    versions = load_json(f"{NUGET_BASE}/{PACKAGE.lower()}/index.json")["versions"]
    stable = [version for version in versions if "-" not in version]
    if not stable:
        raise RuntimeError(f"No stable versions found for {PACKAGE}")
    return max(stable, key=version_key)


def dependency_version(version: str) -> str | None:
    url = f"{NUGET_BASE}/{PACKAGE.lower()}/{version}/{PACKAGE.lower()}.{version}.nupkg"
    with urllib.request.urlopen(url, timeout=15) as response:
        archive = zipfile.ZipFile(io.BytesIO(response.read()))
    nuspec_name = next(name for name in archive.namelist() if name.lower().endswith(".nuspec"))
    root = ElementTree.fromstring(archive.read(nuspec_name))
    for dependency in root.iter():
        if dependency.tag.rsplit("}", 1)[-1] != "dependency":
            continue
        if dependency.attrib.get("id", "").lower() == XUNIT_ID:
            return dependency.attrib.get("version")
    return None


def project_version(path: Path) -> str | None:
    root = ElementTree.parse(path).getroot()
    for reference in root.iter():
        if reference.tag.rsplit("}", 1)[-1] == "PackageReference" and reference.attrib.get("Include", "").lower() == "xunit.v3":
            return reference.attrib.get("Version")
    return None


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", help="NuGet package version to inspect (defaults to latest stable)")
    parser.add_argument("--project", type=Path, help="Project file whose xunit.v3 reference should be shown")
    args = parser.parse_args()

    try:
        version = args.version or latest_stable_version()
        dependency = dependency_version(version)
    except (OSError, ValueError, ElementTree.ParseError, StopIteration, RuntimeError) as error:
        print(f"Unable to inspect {PACKAGE}: {error}", file=sys.stderr)
        return 2

    print(f"{PACKAGE} {version}")
    print(f"Declared {XUNIT_ID}: {dependency or '(not declared)'}")
    if dependency is None:
        print("Result: no xUnit extensibility dependency was found.")
    elif version_key(dependency) >= version_key("4.0.0"):
        print("Result: the package declares xUnit 4-compatible extensibility.")
    else:
        print("Result: the package still declares an xUnit 3 extensibility dependency.")
    if args.project:
        print(f"Project xunit.v3: {project_version(args.project) or '(not found)'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
