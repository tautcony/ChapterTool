#!/usr/bin/env python3
"""Audit Avalonia resource definitions and references."""
import argparse
import re
from pathlib import Path

SYSTEM_PREFIXES = ("System", "AccentButton", "Theme", "ContentControl", "TextControl", "ComboBox", "CheckBox", "RadioButton", "Slider", "TabView", "ScrollBar", "DataGrid", "Flyout", "MenuFlyout", "ToolTip", "ButtonBackground", "ButtonForeground", "ButtonBorder", "ButtonPadding")
FRAMEWORK_KEYS = {"MenuInputGestureTextMargin", "OverlayCornerRadius", "TooltipDataValidationErrors"}
# Fluent template resources use unqualified PascalCase keys. Application
# resources in this repository use a dotted namespace such as ChapterTool.* or
# Brush.*. Keep this structural rule broad so new Fluent controls do not need a
# maintained key list. Results are reported as possible framework keys, not as
# proof of runtime reachability.
IMPLICIT_FRAMEWORK_PATTERN = re.compile(r"^[A-Z][A-Za-z0-9]*$")
KEY = re.compile(r'x:Key\s*=\s*"([^"]+)"')
REF = re.compile(r"\{(?:DynamicResource|StaticResource)\s+([^}\s,]+)")
IMPORTED = re.compile(r"ImportedThemeColorKeys\s*\{\s*get;\s*\}\s*=\s*\[(.*?)\];", re.S)
QUOTED = re.compile(r'"([^"]+)"')
CONST = re.compile(r'public const string \w+ = "([^"]+)";')

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path)
    root = (parser.parse_args().repo_root or Path(__file__).resolve().parents[1]).resolve()
    src = root / "src"
    definitions = [src / "ChapterTool.Avalonia.UI/Resources" / name for name in ("Themes.axaml", "SharedResources.axaml", "Styles.axaml")]
    service = src / "ChapterTool.Avalonia/Services/AvaloniaThemeApplicationService.cs"
    for path in [*definitions, service]:
        if not path.is_file():
            raise FileNotFoundError(f"file not found: {path}")
    defined, referenced, local, service_keys = set(), set(), set(), set()
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
        local.update(m.group(1) for m in KEY.finditer(text))
        referenced.update(m.group(1) for m in REF.finditer(text))
    for path in src.rglob("*.cs"):
        if path.resolve() == service.resolve():
            continue
        referenced.update(m.group(1) for m in QUOTED.finditer(path.read_text(encoding="utf-8")) if m.group(1) in defined)
    unresolved = sorted(k for k in referenced if k not in defined and k not in local and k not in FRAMEWORK_KEYS and not any(k.startswith(p) for p in SYSTEM_PREFIXES))
    unconsumed = sorted(defined - referenced)
    implicit = sorted(k for k in unconsumed if IMPLICIT_FRAMEWORK_PATTERN.fullmatch(k))
    explicit_candidates = sorted(set(unconsumed) - set(implicit))
    print("UI resource audit")
    print(f"Repo: {root}\nDefined keys: {len(defined)}\nReferenced keys: {len(referenced)}\nService-written keys: {len(service_keys)}\n")
    print(f"Unresolved references ({len(unresolved)}):\n" + ("  (none)" if not unresolved else "\n".join(f"  {k}" for k in unresolved)))
    print(f"\nPossible framework-template definitions ({len(implicit)}):")
    if implicit:
        print("\n".join(f"  {key} ({definition_sources.get(key, '<unknown source>')})" for key in implicit))
    else:
        print("  (none)")
    print("These unqualified keys may be resolved by Avalonia Fluent templates or other framework resources. Confirm against the active theme before removal.")
    print(f"\nApplication definitions without a detected consumer ({len(explicit_candidates)}):")
    if explicit_candidates:
        print("\n".join(f"  {key} ({definition_sources.get(key, '<unknown source>')})" for key in explicit_candidates))
    else:
        print("  (none)")
    print("Review these candidates before removal. Static analysis cannot prove that a resource is unreachable at runtime.")

if __name__ == "__main__":
    raise SystemExit(main())
