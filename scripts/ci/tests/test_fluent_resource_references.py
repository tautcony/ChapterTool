"""Behavior checks for Fluent resource-reference extraction."""

import importlib.util
from pathlib import Path
import subprocess
import sys
import unittest


SCRIPT = Path(__file__).resolve().parents[2] / "diagnostics" / "update-avalonia-fluent-resource-references.py"
SPEC = importlib.util.spec_from_file_location("fluent_resource_references", SCRIPT)
references = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(references)


class ResourceReferenceTests(unittest.TestCase):
    def test_positional_and_named_keys_preserve_reference_syntax(self):
        source = """
            {DynamicResource AccentBrush}
            {StaticResource ResourceKey = ControlTheme, Other = value}
            {DynamicResource Other=value, ResourceKey=ForegroundBrush}
            {StaticResource\tTabKey, Other=value}
            {DynamicResource\nMultilineKey}
            {Binding Path=Name} {StaticResourceNoSpace}
        """
        self.assertEqual(
            {"AccentBrush", "ControlTheme", "ForegroundBrush", "TabKey", "MultilineKey"},
            references.resource_key_references(source),
        )

    def test_empty_or_incomplete_references_do_not_hide_later_keys(self):
        source = "{DynamicResource   } {StaticResource} {StaticResource Missing {DynamicResource ValidKey}"
        self.assertEqual({"ValidKey"}, references.resource_key_references(source))

    def test_long_unterminated_whitespace_completes_without_backtracking_explosion(self):
        code = """
import importlib.util
import sys
spec = importlib.util.spec_from_file_location("references", sys.argv[1])
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
source = "{DynamicResource " + " " * 100_000 + "MissingClose\\n{StaticResource After}"
assert module.resource_key_references(source) == {"After"}
"""
        subprocess.run([sys.executable, "-c", code, str(SCRIPT)], check=True, timeout=10, capture_output=True)


if __name__ == "__main__":
    unittest.main()
