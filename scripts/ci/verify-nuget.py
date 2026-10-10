"""Install and execute the packed NuGet library and CLI in isolated consumers."""

from __future__ import annotations

import argparse
import os
from pathlib import Path
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zipfile


ROOT = Path(__file__).resolve().parents[2]


def package_metadata(directory: Path, package_id: str) -> tuple[Path, str]:
    archives = list(directory.glob("*.nupkg"))
    if len(archives) != 1:
        raise ValueError(f"Expected one {package_id} package in {directory}.")
    with zipfile.ZipFile(archives[0]) as archive:
        specs = [name for name in archive.namelist() if name.endswith(".nuspec")]
        if len(specs) != 1:
            raise ValueError("Expected one NuGet package manifest.")
        metadata = ET.fromstring(archive.read(specs[0]))
    fields = {node.tag.rsplit("}", 1)[-1]: node.text for node in metadata.iter()}
    if fields.get("id") != package_id or not fields.get("version"):
        raise ValueError(f"Unexpected NuGet package metadata: {archives[0]}.")
    return archives[0], fields["version"]


def verify(root: Path, expected_version: str | None = None, metadata_only: bool = False) -> None:
    core, version = package_metadata(root / "artifacts/nuget", "ChapterTool.Core")
    cli, cli_version = package_metadata(root / "artifacts/cli-nuget", "ChapterTool")
    if version != cli_version:
        raise ValueError("Core and CLI NuGet versions differ.")
    if expected_version and version != expected_version:
        raise ValueError(f"NuGet version {version} does not match release version {expected_version}.")
    if metadata_only:
        print(f"Core and CLI package metadata passed ({version}).")
        return
    with tempfile.TemporaryDirectory(prefix="chaptertool-nuget-") as temporary:
        consumer = Path(temporary)
        environment = {**os.environ, "NUGET_PACKAGES": str(consumer / "packages")}
        configuration = ET.Element("configuration")
        sources = ET.SubElement(configuration, "packageSources")
        ET.SubElement(sources, "clear")
        for name, source in (("core", str(core.parent)), ("cli", str(cli.parent)), ("nuget", "https://api.nuget.org/v3/index.json")):
            ET.SubElement(sources, "add", key=name, value=source)
        mappings = ET.SubElement(configuration, "packageSourceMapping")
        for name, pattern in (("core", "ChapterTool.Core"), ("cli", "ChapterTool"), ("nuget", "*")):
            source = ET.SubElement(mappings, "packageSource", key=name)
            ET.SubElement(source, "package", pattern=pattern)
        config = consumer / "NuGet.config"
        ET.ElementTree(configuration).write(config, encoding="utf-8", xml_declaration=True)
        (consumer / "Consumer.csproj").write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
            '<TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings>'
            '</PropertyGroup><ItemGroup>'
            f'<PackageReference Include="ChapterTool.Core" Version="[{version}]" />'
            '</ItemGroup></Project>', encoding="utf-8")
        (consumer / "Program.cs").write_text(
            'using ChapterTool.Core.Transform;\n'
            'var formatter = new ChapterTimeFormatter();\n'
            'if (formatter.Format(formatter.ParseOrZero("00:00:12.500")) != "00:00:12.500")'
            ' throw new Exception("Installed Core package failed.");\n', encoding="utf-8")
        commands = [
            ["dotnet", "restore", "Consumer.csproj", "--configfile", str(config)],
            ["dotnet", "run", "--project", "Consumer.csproj", "--configuration", "Release", "--no-restore"],
            ["dotnet", "tool", "install", "ChapterTool", "--version", cli_version, "--tool-path", str(consumer / "tools"), "--configfile", str(config)],
            [str(consumer / "tools" / ("chaptertool.exe" if os.name == "nt" else "chaptertool")), "--help"],
        ]
        for command in commands:
            subprocess.run(command, cwd=consumer, env=environment, check=True, timeout=180)
    print(f"Installed Core and CLI consumers passed ({version}).")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--expected-version")
    parser.add_argument("--metadata-only", action="store_true")
    options = parser.parse_args()
    verify(ROOT, options.expected_version, options.metadata_only)
