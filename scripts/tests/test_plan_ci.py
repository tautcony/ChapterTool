"""Verify dependency selection, comparison ranges, and the required CI gate."""

import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch


SPEC = importlib.util.spec_from_file_location("plan_ci", Path(__file__).parents[1] / "ci/plan-ci.py")
planner = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(planner)
ROOT = Path(__file__).resolve().parents[2]


class PlanTests(unittest.TestCase):
    def labels(self, paths, **options):
        plan = planner.select_jobs(ROOT, paths, **options)
        return {item["label"] for item in plan["tests"]["include"]}, plan

    def test_core_reaches_every_host_and_test_project(self):
        labels, plan = self.labels(["src/ChapterTool.Core/Models/Chapter.cs"])
        self.assertEqual({"Core", "CommandLine", "Infrastructure", "Avalonia", "Avalonia.Headless", "Wasm"}, labels)
        self.assertTrue(plan["jobs"]["wasm-browser-e2e"])
        self.assertTrue(plan["jobs"]["pack-nodejs"])
        self.assertTrue(plan["jobs"]["pack-nuget"])
        self.assertFalse(plan["jobs"]["pack-dotnet"])
        self.assertFalse(plan["jobs"]["solution-build"])

    def test_infrastructure_selects_its_actual_consumers(self):
        labels, plan = self.labels(["src/ChapterTool.Infrastructure/Services/Settings.cs"])
        self.assertEqual({"Infrastructure", "CommandLine", "Avalonia", "Avalonia.Headless"}, labels)
        self.assertFalse(plan["jobs"]["pack-nodejs"])
        self.assertFalse(plan["jobs"]["wasm-browser-e2e"])

    def test_test_change_runs_only_its_project(self):
        labels, plan = self.labels(["tests/ChapterTool.Core.Tests/Editing/ChangedTests.cs"])
        self.assertEqual({"Core"}, labels)
        self.assertEqual(["dotnet-tests"], plan["required"])

    def test_shared_test_support_reaches_all_referencing_tests(self):
        labels, _ = self.labels(["tests/ChapterTool.TestSupport/Fakes.cs"])
        self.assertEqual({"Core", "CommandLine", "Infrastructure", "Avalonia", "Avalonia.Headless", "Wasm"}, labels)

    def test_shared_disc_fixtures_reach_browser_node_and_dotnet_consumers(self):
        labels, plan = self.labels(["tests/ChapterTool.Core.Tests/Fixtures/Importing/Disc/Xpl/VPLST001.XPL"])
        self.assertEqual({"Core", "CommandLine", "Infrastructure", "Avalonia", "Avalonia.Headless", "Wasm"}, labels)
        self.assertTrue(plan["jobs"]["wasm-browser-e2e"])
        self.assertTrue(plan["jobs"]["pack-nodejs"])
        self.assertFalse(plan["jobs"]["pack-dotnet"])

    def test_browser_test_change_does_not_run_desktop_or_node(self):
        labels, plan = self.labels(["tests/ChapterTool.Wasm.E2E/specs/smoke.spec.ts"])
        self.assertEqual(set(), labels)
        self.assertEqual(["wasm-browser-e2e"], plan["required"])

    def test_locales_select_shared_ui_resources_and_browser(self):
        labels, plan = self.labels([planner.LOCALES + "en-US.axaml"])
        self.assertEqual({"Avalonia", "Avalonia.Headless"}, labels)
        self.assertTrue(plan["jobs"]["resources"])
        self.assertTrue(plan["jobs"]["wasm-browser-e2e"])
        self.assertFalse(plan["jobs"]["pack-nodejs"])

    def test_desktop_dependency_project_changes_verify_runtime_packaging(self):
        _, plan = self.labels(["src/ChapterTool.Avalonia.UI/ChapterTool.Avalonia.UI.csproj"])
        self.assertTrue(plan["jobs"]["pack-dotnet"])
        _, plan = self.labels(["src/ChapterTool.Avalonia.UI/Views/MainView.axaml"])
        self.assertFalse(plan["jobs"]["pack-dotnet"])

    def test_docs_and_empty_changes_need_only_the_fixed_gate(self):
        for paths in ([], ["docs/code-map/testing.md", "AGENTS.md"], ["CHANGELOG.md"]):
            with self.subTest(paths=paths):
                _, plan = self.labels(paths, master=True)
                self.assertEqual([], plan["required"])

    def test_global_unknown_and_ci_inputs_use_full_gate(self):
        for path in ("Directory.Build.props", "ChapterTool.slnx", ".github/workflows/dotnet-ci.yml", "scripts/check-ci.py", "tests/TestFiles/minimal.txt", "new-build-input.json"):
            with self.subTest(path=path):
                _, plan = self.labels([path])
                self.assertEqual(list(planner.JOBS), plan["required"])

    def test_master_and_tag_checks_produce_all_release_artifacts(self):
        for options in ({"master": True}, {"full": True}):
            with self.subTest(options=options):
                _, plan = self.labels(["src/ChapterTool.Core/Models/Chapter.cs"], **options)
                self.assertTrue(all(plan["jobs"][job] for job in ("pack-dotnet", "pack-nodejs", "pack-nuget")))
                self.assertEqual(bool(options.get("full")), plan["jobs"]["solution-build"])

    def test_gate_rejects_failed_cancelled_missing_or_skipped_required_jobs(self):
        _, plan = self.labels(["tests/ChapterTool.Core.Tests/Changed.cs"])
        needs = {job: {"result": "success" if job in plan["required"] else "skipped"} for job in planner.JOBS}
        needs["plan"] = {"result": "success"}
        planner.verify_jobs(plan, needs)
        for result in ("failure", "cancelled", "skipped", None):
            with self.subTest(result=result):
                needs["dotnet-tests"] = {"result": result}
                with self.assertRaisesRegex(ValueError, "dotnet-tests"):
                    planner.verify_jobs(plan, needs)
        needs["plan"] = {"result": "failure"}
        with self.assertRaisesRegex(ValueError, "planning"):
            planner.verify_jobs(plan, needs)

    def test_planner_writes_actions_outputs_for_docs_only_pr(self):
        with tempfile.TemporaryDirectory() as temporary:
            event = Path(temporary) / "event.json"
            output = Path(temporary) / "output.txt"
            event.write_text(json.dumps({"pull_request": {"base": {"sha": "base"}, "head": {"sha": "head"}}}), encoding="utf-8")
            with patch.dict(os.environ, {"GITHUB_EVENT_NAME": "pull_request", "GITHUB_EVENT_PATH": str(event), "GITHUB_REF": "refs/pull/1/merge", "GITHUB_OUTPUT": str(output)}), \
                    patch.object(planner, "changed_paths", return_value=["docs/README.md"]):
                self.assertEqual(0, planner.main([]))
            values = dict(line.split("=", 1) for line in output.read_text(encoding="utf-8").splitlines())
            self.assertEqual([], json.loads(values["plan"])["required"])
            self.assertEqual("false", values["dotnet-tests"])

    def test_git_diff_includes_deleted_files_and_both_rename_paths(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)

            def git(*args):
                return subprocess.check_output(["git", *args], cwd=root).decode().strip()

            git("init", "-q")
            git("config", "user.name", "CI Test")
            git("config", "user.email", "ci@example.invalid")
            (root / "old.txt").write_text("old", encoding="utf-8")
            git("add", ".")
            git("commit", "-qm", "base")
            base = git("rev-parse", "HEAD")
            (root / "old.txt").rename(root / "new.txt")
            git("add", "-A")
            git("commit", "-qm", "rename")
            head = git("rev-parse", "HEAD")
            self.assertEqual({"old.txt", "new.txt"}, set(planner.changed_paths(root, "push", {"before": base, "after": head})))
            self.assertEqual([], planner.changed_paths(root, "push", {"before": head, "after": head}))

    def test_missing_comparison_history_uses_full_gate(self):
        with patch.object(planner.subprocess, "check_output", side_effect=subprocess.CalledProcessError(128, ["git", "diff"])):
            changed = planner.changed_paths(ROOT, "push", {"before": "old-sha", "after": "new-sha"})
        _, plan = self.labels(changed)
        self.assertEqual(list(planner.JOBS), plan["required"])


if __name__ == "__main__":
    unittest.main()
