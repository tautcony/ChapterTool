#!/usr/bin/env python3
"""Audit Avalonia resource definitions against repository and verified Fluent references."""
import argparse
import json
import re
from pathlib import Path
import xml.etree.ElementTree as ET

KEY = re.compile(r'x:Key\s*=\s*"([^"]+)"')
REF = re.compile(r"\{(?:DynamicResource|StaticResource)\s+([^}\s,]+)")
IMPORTED = re.compile(r"ImportedThemeColorKeys\s*\{\s*get;\s*\}\s*=\s*\[(.*?)\];", re.S)
QUOTED = re.compile(r'"([^"]+)"')
CONST = re.compile(r'public const string \w+ = "([^"]+)";')
NONE_LABEL = "  (none)"
FLUENT_REFERENCE_MANIFEST = Path(__file__).with_name("avalonia-fluent-resource-references.json")


def fluent_resource_evidence(root: Path) -> tuple[str, str, dict[str, set[str]]]:
    manifest = json.loads(FLUENT_REFERENCE_MANIFEST.read_text(encoding="utf-8"))
    package_name = manifest["package"]["id"]
    expected_version = manifest["package"]["version"]
    versions = set()
    for path in (root / "src").rglob("*.csproj"):
        project = ET.parse(path).getroot()  # nosec B314
        for reference in project.iter("PackageReference"):
            if reference.attrib.get("Include", "").casefold() == package_name.casefold():
                version = reference.attrib.get("Version")
                if version:
                    versions.add(version)

    if versions != {expected_version}:
        found = ", ".join(sorted(versions)) or "no explicit version"
        raise ValueError(
            f"{package_name} resource evidence is pinned to {expected_version}, "
            f"but project references specify {found}. Update the evidence from "
            "the matching upstream source before running this audit."
        )

    references: dict[str, set[str]] = {}
    for key, sources in manifest["references"].items():
        references[key] = set(sources)
    return package_name, expected_version, references


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path)
    root = (parser.parse_args().repo_root or Path(__file__).resolve().parents[2]).resolve()
    src = root / "src"
    definitions = [src / "ChapterTool.Avalonia.UI/Resources" / name for name in ("Themes.axaml", "SharedResources.axaml", "Styles.axaml")]
    service = src / "ChapterTool.Avalonia/Services/AvaloniaThemeApplicationService.cs"
    for path in [*definitions, service]:
        if not path.is_file():
            raise FileNotFoundError(f"file not found: {path}")

    package_name, package_version, fluent_evidence = fluent_resource_evidence(root)
    fluent_references = set(fluent_evidence)
    defined, referenced, service_keys = set(), set(), set()
    definition_sources = {}
    for path in definitions:
        text = path.read_text(encoding="utf-8")
        for match in KEY.finditer(text):
            key = match.group(1)
            if key not in {"Light", "Dark"}:
                defined.add(key)
                definition_sources.setdefault(key, path.relative_to(root).as_posix())
        for block in re.findall(r"<Style\.Resources>(.*?)</Style\.Resources>", text, re.S):
            referenced.update(m.group(1) for m in KEY.finditer(block))

    text = service.read_text(encoding="utf-8")
    imported = IMPORTED.search(text)
    if imported:
        service_keys.update(m.group(1) for m in QUOTED.finditer(imported.group(1)))
    service_keys.update(m.group(1) for m in CONST.finditer(text))
    defined.update(service_keys)
    for key in service_keys:
        definition_sources.setdefault(key, service.relative_to(root).as_posix())

    for path in src.rglob("*.axaml"):
        text = path.read_text(encoding="utf-8")
        referenced.update(m.group(1) for m in REF.finditer(text))
    for path in src.rglob("*.cs"):
        if path.resolve() == service.resolve():
            continue
        referenced.update(m.group(1) for m in QUOTED.finditer(path.read_text(encoding="utf-8")) if m.group(1) in defined)

    unconsumed = sorted(defined - referenced)
    externally_consumed = sorted(set(unconsumed) & fluent_references)
    candidates = sorted(set(unconsumed) - fluent_references)
    print("UI resource audit")
    print(f"Repo: {root}\nDefined keys: {len(defined)}\nRepository references: {len(referenced)}\nService-written keys: {len(service_keys)}\n")
    print(f"Definitions consumed by verified {package_name} {package_version} templates ({len(externally_consumed)}):")
    print(
        "\n".join(
            f"  {key} (defined in {definition_sources.get(key, '<unknown source>')}; "
            f"referenced by {', '.join(sorted(fluent_evidence[key]))})"
            for key in externally_consumed
        )
        if externally_consumed
        else NONE_LABEL
    )
    print(f"\nApplication definitions with no detected repository or verified Fluent consumer ({len(candidates)}):")
    print("\n".join(f"  {key} ({definition_sources.get(key, '<unknown source>')})" for key in candidates) if candidates else NONE_LABEL)
    print("This report lists source-reference evidence. Review dynamic or reflection-based consumers before removal.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, KeyError, json.JSONDecodeError) as error:
        raise SystemExit(f"UI resource audit failed: {error}") from error
