"""Verify that release metadata cannot select a stale or mismatched package."""

import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile


SPEC = importlib.util.spec_from_file_location("verify_nuget", Path(__file__).parents[1] / "ci/verify-nuget.py")
verifier = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(verifier)


class PackageTests(unittest.TestCase):
    def package(self, root, directory, name, version):
        feed = root / "artifacts" / directory
        feed.mkdir(parents=True, exist_ok=True)
        archive = feed / f"{name}.{version}.nupkg"
        with zipfile.ZipFile(archive, "w") as bundle:
            bundle.writestr(f"{name}.nuspec", '<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">'
                            f'<metadata><id>{name}</id><version>{version}</version></metadata></package>')
        return archive

    def test_metadata_accepts_matching_tag_without_installing_again(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.package(root, "nuget", "ChapterTool.Core", "23.3.2")
            self.package(root, "cli-nuget", "ChapterTool", "23.3.2")
            with patch.object(verifier.subprocess, "run") as run:
                verifier.verify(root, "23.3.2", metadata_only=True)
            run.assert_not_called()

    def test_version_mismatch_fails_before_install_or_publication(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.package(root, "nuget", "ChapterTool.Core", "23.3.2")
            self.package(root, "cli-nuget", "ChapterTool", "23.3.1")
            with patch.object(verifier.subprocess, "run") as run, self.assertRaisesRegex(ValueError, "versions differ"):
                verifier.verify(root, "23.3.2")
            run.assert_not_called()

    def test_stale_packages_cannot_pass_release_metadata(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.package(root, "nuget", "ChapterTool.Core", "23.3.1")
            self.package(root, "cli-nuget", "ChapterTool", "23.3.1")
            with self.assertRaisesRegex(ValueError, "does not match"):
                verifier.verify(root, "23.3.2", metadata_only=True)
            self.package(root, "nuget", "ChapterTool.Core", "23.3.2")
            with self.assertRaisesRegex(ValueError, "Expected one"):
                verifier.verify(root, "23.3.2", metadata_only=True)


if __name__ == "__main__":
    unittest.main()
