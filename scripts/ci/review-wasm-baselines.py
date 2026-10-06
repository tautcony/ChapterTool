"""Extract the Linux baseline artifact for review and optionally apply its images."""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path, PurePosixPath
import sys
import zipfile


ROOT = Path(__file__).resolve().parents[2]
BASELINES = ROOT / "tests/ChapterTool.Wasm.E2E/specs/layout.spec.ts-snapshots"
REVIEW = ROOT / "artifacts/wasm-e2e/baseline-review"


def review_baselines(archive: Path, baselines: Path, output: Path, apply: bool = False) -> tuple[list[str], int]:
    images = {}
    with zipfile.ZipFile(archive) as bundle:
        for member in bundle.infolist():
            if member.is_dir() or not member.filename.endswith("-chromium-linux.png"):
                continue
            name = PurePosixPath(member.filename).name
            if name in images:
                raise ValueError(f"Duplicate baseline in artifact: {name}")
            images[name] = bundle.read(member)
    expected = {path.name for path in baselines.glob("*.png")}
    if not expected or set(images) != expected:
        difference = ", ".join(sorted(set(images) ^ expected))
        raise ValueError(f"Baseline membership differs: {difference or 'no committed baselines'}")
    changed = sorted(name for name, data in images.items() if (baselines / name).read_bytes() != data)
    output.mkdir(parents=True, exist_ok=True)
    for name, data in images.items():
        (output / name).write_bytes(data)
    if apply:
        for name in changed:
            (baselines / name).write_bytes(images[name])
    return changed, len(images) - len(changed)


def main(arguments: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path, help="Downloaded wasm-linux-baselines ZIP artifact.")
    parser.add_argument("--apply", action="store_true", help="Copy reviewed changes into the committed Linux baselines.")
    options = parser.parse_args(arguments)
    try:
        changed, unchanged = review_baselines(options.archive, BASELINES, REVIEW, options.apply)
    except (OSError, ValueError, zipfile.BadZipFile) as error:
        print(f"Baseline review failed: {error}", file=sys.stderr)
        return 1
    for name in changed:
        digest = hashlib.sha256((REVIEW / name).read_bytes()).hexdigest()
        print(f"{name} {digest}")
    print(f"{'Applied' if options.apply else 'Prepared for review'}: {len(changed)} changed; {unchanged} unchanged.")
    print(f"Review images: {REVIEW}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
