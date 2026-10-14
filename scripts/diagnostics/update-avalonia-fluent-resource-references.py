#!/usr/bin/env python3
"""Refresh Fluent resource-reference evidence from the exact NuGet source commit."""
import argparse
from concurrent.futures import ThreadPoolExecutor
from io import BytesIO
import json
from pathlib import Path
import re
import urllib.request
import xml.etree.ElementTree as ET
from zipfile import ZipFile


ROOT = Path(__file__).resolve().parents[2]
MANIFEST = Path(__file__).with_name("avalonia-fluent-resource-references.json")
PACKAGE_ID = "Avalonia.Themes.Fluent"
EXPECTED_REPOSITORY = "https://github.com/AvaloniaUI/Avalonia/"
SOURCE_DIRECTORY = "src/Avalonia.Themes.Fluent/"
PACKAGE_REFERENCE = "https://api.nuget.org/v3-flatcontainer/avalonia.themes.fluent/{version}/avalonia.themes.fluent.{version}.nupkg"
SOURCE_TREE_API = "https://api.github.com/repos/AvaloniaUI/Avalonia/git/trees/{commit}?recursive=1"
RAW_SOURCE = "https://raw.githubusercontent.com/AvaloniaUI/Avalonia/{commit}/{path}"
MARKUP_RESOURCE = re.compile(r"\{(?:DynamicResource|StaticResource)([^{}]*)\}")
RESOURCE_KEY_ARGUMENT = re.compile(r"(?:^|,)\s*ResourceKey\s*=\s*([^,\s]+)")
USER_AGENT = "ChapterTool-UI-resource-audit/1.0"


def pinned_version() -> str:
    versions = set()
    for path in (ROOT / "src").rglob("*.csproj"):
        project = ET.parse(path).getroot()  # nosec B314
        for reference in project.iter("PackageReference"):
            if reference.attrib.get("Include", "").casefold() == PACKAGE_ID.casefold():
                version = reference.attrib.get("Version")
                if version:
                    versions.add(version)
    if len(versions) != 1:
        raise ValueError(f"Expected one explicit {PACKAGE_ID} version, found: {sorted(versions)}")
    return versions.pop()


def download(url: str) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT, "Accept": "application/vnd.github+json"})
    with urllib.request.urlopen(request, timeout=30) as response:  # nosec B310
        return response.read()


def package_source_commit(version: str) -> tuple[str, str]:
    package = ZipFile(BytesIO(download(PACKAGE_REFERENCE.format(version=version))))
    with package:
        nuspec_name = next(name for name in package.namelist() if name.endswith(".nuspec"))
        metadata = ET.fromstring(package.read(nuspec_name))  # nosec B314
    details = next((node for node in metadata.iter() if node.tag.rsplit("}", 1)[-1] == "metadata"), None)
    if details is None:
        raise ValueError(f"{PACKAGE_ID} {version} package has no metadata")
    package_values = {node.tag.rsplit("}", 1)[-1]: node.text for node in details}
    if package_values.get("id", "").casefold() != PACKAGE_ID.casefold() or package_values.get("version") != version:
        raise ValueError(f"NuGet package metadata does not match {PACKAGE_ID} {version}")
    repository = next((node for node in details if node.tag.rsplit("}", 1)[-1] == "repository"), None)
    if repository is None:
        raise ValueError(f"{PACKAGE_ID} {version} package has no source repository metadata")
    repository_url = repository.attrib.get("url", "").rstrip("/") + "/"
    commit = repository.attrib.get("commit", "")
    if repository_url != EXPECTED_REPOSITORY or not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise ValueError(f"Unexpected source metadata for {PACKAGE_ID} {version}: {repository.attrib}")
    return repository_url, commit


def resource_key_references(text: str) -> set[str]:
    references = set()
    for match in MARKUP_RESOURCE.finditer(text):
        arguments = match.group(1)
        if not arguments or not arguments[0].isspace():
            continue
        arguments = arguments.strip()
        if not arguments:
            continue
        named_key = RESOURCE_KEY_ARGUMENT.search(arguments)
        key = named_key.group(1) if named_key else arguments.split(",", 1)[0].strip().split()[0]
        if key:
            references.add(key)
    return references


def source_files(commit: str) -> list[str]:
    tree = json.loads(download(SOURCE_TREE_API.format(commit=commit)))
    if tree.get("truncated"):
        raise ValueError(f"GitHub returned a truncated source tree for commit {commit}")
    paths = [
        entry["path"]
        for entry in tree.get("tree", [])
        if entry.get("type") == "blob"
        and entry["path"].startswith(SOURCE_DIRECTORY)
        and entry["path"].endswith((".xaml", ".axaml"))
    ]
    if not paths:
        raise ValueError(f"No Fluent XAML files found in commit {commit}")
    return sorted(paths)


def collect_references(commit: str) -> tuple[dict[str, list[str]], list[str]]:
    paths = source_files(commit)
    references: dict[str, set[str]] = {}
    def scan(path: str) -> tuple[str, set[str]]:
        source = download(RAW_SOURCE.format(commit=commit, path=path))
        document = ET.fromstring(source)  # nosec B314
        keys = set()
        for element in document.iter():
            values = [*element.attrib.values()]
            if element.text:
                values.append(element.text)
            for value in values:
                keys.update(resource_key_references(value))
            if element.tag.rsplit("}", 1)[-1] in {"StaticResource", "DynamicResource"}:
                key = element.attrib.get("ResourceKey")
                if key:
                    keys.add(key)
        return path, keys

    with ThreadPoolExecutor(max_workers=8) as executor:
        for path, keys in executor.map(scan, paths):
            for key in keys:
                references.setdefault(key, set()).add(path)
    return {key: sorted(paths) for key, paths in sorted(references.items())}, paths


def generate_manifest() -> dict[str, object]:
    version = pinned_version()
    repository_url, commit = package_source_commit(version)
    references, scanned_files = collect_references(commit)
    return {
        "package": {"id": PACKAGE_ID, "version": version},
        "repository": repository_url,
        "commit": commit,
        "sourceDirectory": SOURCE_DIRECTORY.rstrip("/"),
        "sourceTreeApi": SOURCE_TREE_API.format(commit=commit),
        "scannedFiles": scanned_files,
        "scannedFileExtensions": [".xaml", ".axaml"],
        "scannedReferenceKinds": ["DynamicResource", "StaticResource"],
        "scannedReferenceCount": sum(len(paths) for paths in references.values()),
        "references": references,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="report whether the committed manifest is current without writing it")
    options = parser.parse_args()
    generated = json.dumps(generate_manifest(), ensure_ascii=False, indent=2) + "\n"
    if options.check:
        current = MANIFEST.read_text(encoding="utf-8") if MANIFEST.exists() else ""
        if current != generated:
            print(f"Fluent resource evidence is out of date: {MANIFEST}")
            return 1
        print(f"Fluent resource evidence is current: {MANIFEST}")
        return 0
    MANIFEST.write_text(generated, encoding="utf-8")
    manifest = json.loads(generated)
    print(
        f"Updated {MANIFEST} from {manifest['package']['id']} {manifest['package']['version']} "
        f"at {manifest['commit']}: {len(manifest['references'])} keys, "
        f"{manifest['scannedReferenceCount']} references."
    )
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError, StopIteration, ET.ParseError) as error:
        raise SystemExit(f"Fluent resource evidence update failed: {error}") from error
