"""Verify release artifact provenance before any publisher can use a CI run."""

import importlib.util
from pathlib import Path
import unittest
from unittest.mock import patch


SPEC = importlib.util.spec_from_file_location("resolve_ci_run", Path(__file__).parents[1] / "ci/resolve-ci-run.py")
resolver = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(resolver)


class ReleaseRunTests(unittest.TestCase):
    def valid_run(self):
        return {
            "event": "push", "status": "completed", "conclusion": "success", "head_branch": "v23.3.2", "head_sha": "tested-sha",
            "head_repository": {"full_name": "tautcony/ChapterTool"}, "repository": {"full_name": "tautcony/ChapterTool"},
            "path": ".github/workflows/dotnet-ci.yml",
        }

    def test_triggered_run_requires_exact_commit_and_successful_tag_gate(self):
        with patch.object(resolver, "gh_json", return_value=self.valid_run()):
            self.assertEqual(123, resolver.resolve_run("tautcony/ChapterTool", "v23.3.2", "tested-sha", "123"))
        for key, value in (("head_sha", "other-sha"), ("head_branch", "master"), ("event", "pull_request"),
                           ("conclusion", "failure"), ("status", "in_progress"), ("path", ".github/workflows/other.yml"),
                           ("head_repository", {"full_name": "fork/ChapterTool"})):
            with self.subTest(key=key), patch.object(resolver, "gh_json", return_value={**self.valid_run(), key: value}):
                with self.assertRaises(ValueError):
                    resolver.resolve_run("tautcony/ChapterTool", "v23.3.2", "tested-sha", "123")

    def test_manual_lookup_selects_a_successful_run_of_the_checked_out_commit(self):
        with patch.object(resolver, "gh_json", side_effect=[[{"databaseId": 456}], self.valid_run()]) as gh:
            self.assertEqual(456, resolver.resolve_run("tautcony/ChapterTool", "v23.3.2", "tested-sha"))
        self.assertIn("tested-sha", gh.call_args_list[0].args)
        self.assertEqual(("api", "repos/tautcony/ChapterTool/actions/runs/456"), gh.call_args_list[1].args)

    def test_manual_lookup_fails_when_no_successful_tag_run_exists(self):
        with patch.object(resolver, "gh_json", return_value=[]):
            with self.assertRaisesRegex(ValueError, "No successful"):
                resolver.resolve_run("tautcony/ChapterTool", "v23.3.2", "tested-sha")

    def test_invalid_tag_or_run_id_cannot_reach_github(self):
        with patch.object(resolver, "gh_json") as gh:
            for tag, run_id in (("master", "123"), ("v23.3.2", "../../other")):
                with self.subTest(tag=tag, run_id=run_id), self.assertRaises(ValueError):
                    resolver.resolve_run("tautcony/ChapterTool", tag, "tested-sha", run_id)
        gh.assert_not_called()


if __name__ == "__main__":
    unittest.main()
