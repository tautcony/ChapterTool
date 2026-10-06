"""Behavior checks for reviewing and applying generated Linux screenshot artifacts."""

import importlib.util
from pathlib import Path
import tempfile
import unittest
import zipfile


SPEC = importlib.util.spec_from_file_location("review_wasm_baselines", Path(__file__).parents[1] / "ci/review-wasm-baselines.py")
review = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(review)


class BaselineReviewTests(unittest.TestCase):
    def test_review_extracts_images_without_changing_committed_baselines(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            baselines = root / "baselines"
            baselines.mkdir()
            name = "wasm-default-chromium-linux.png"
            (baselines / name).write_bytes(b"old image")
            archive = root / "artifact.zip"
            with zipfile.ZipFile(archive, "w") as bundle:
                bundle.writestr(f"nested/{name}", b"new image")
            changed, unchanged = review.review_baselines(archive, baselines, root / "review")
            self.assertEqual([name], changed)
            self.assertEqual(0, unchanged)
            self.assertEqual(b"new image", (root / "review" / name).read_bytes())
            self.assertEqual(b"old image", (baselines / name).read_bytes())

    def test_apply_updates_changed_images_and_preserves_unchanged_images(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            baselines = root / "baselines"
            baselines.mkdir()
            changed_name = "wasm-default-chromium-linux.png"
            unchanged_name = "wasm-wide-chromium-linux.png"
            (baselines / changed_name).write_bytes(b"old")
            (baselines / unchanged_name).write_bytes(b"unchanged")
            archive = root / "artifact.zip"
            with zipfile.ZipFile(archive, "w") as bundle:
                bundle.writestr(changed_name, b"new")
                bundle.writestr(unchanged_name, b"unchanged")
            changed, unchanged = review.review_baselines(archive, baselines, root / "review", apply=True)
            self.assertEqual([changed_name], changed)
            self.assertEqual(1, unchanged)
            self.assertEqual(b"new", (baselines / changed_name).read_bytes())
            self.assertEqual(b"unchanged", (baselines / unchanged_name).read_bytes())

    def test_incomplete_artifact_fails_before_applying_any_images(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            baselines = root / "baselines"
            baselines.mkdir()
            name = "wasm-default-chromium-linux.png"
            missing = "wasm-wide-chromium-linux.png"
            (baselines / name).write_bytes(b"old")
            (baselines / missing).write_bytes(b"old wide")
            archive = root / "artifact.zip"
            with zipfile.ZipFile(archive, "w") as bundle:
                bundle.writestr(name, b"new")
            with self.assertRaisesRegex(ValueError, "membership differs"):
                review.review_baselines(archive, baselines, root / "review", apply=True)
            self.assertEqual(b"old", (baselines / name).read_bytes())
            self.assertEqual(b"old wide", (baselines / missing).read_bytes())
            self.assertFalse((root / "review").exists())

    def test_duplicate_names_fail_before_applying_images(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            baselines = root / "baselines"
            baselines.mkdir()
            name = "wasm-default-chromium-linux.png"
            (baselines / name).write_bytes(b"old")
            archive = root / "artifact.zip"
            with zipfile.ZipFile(archive, "w") as bundle:
                bundle.writestr(f"one/{name}", b"new one")
                bundle.writestr(f"two/{name}", b"new two")
            with self.assertRaisesRegex(ValueError, "Duplicate baseline"):
                review.review_baselines(archive, baselines, root / "review", apply=True)
            self.assertEqual(b"old", (baselines / name).read_bytes())


if __name__ == "__main__":
    unittest.main()
