"""Entry points for repository scripts."""

from __future__ import annotations

import runpy
import subprocess
from pathlib import Path


ROOT = Path(__file__).parent
SUBSET_SCRIPT = ROOT / "subset-shortcut-font.py"


def _subset(arguments: list[str]) -> int:
    namespace = runpy.run_path(str(SUBSET_SCRIPT), run_name="chaptertool_subset_shortcut_font")
    return int(namespace["main"](arguments))


def subset_shortcut_font() -> int:
    return _subset([])


def check_shortcut_font() -> int:
    return _subset(["--check"])


def lint_scripts() -> int:
    return subprocess.run(["ruff", "check", "scripts/"], cwd=ROOT.parent, check=False).returncode
