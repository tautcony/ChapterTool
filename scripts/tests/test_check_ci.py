"""Behavior checks for the shared cross-platform CI runner."""

import contextlib
import importlib.util
import io
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch


SPEC = importlib.util.spec_from_file_location("check_ci", Path(__file__).parents[1] / "check-ci.py")
ci = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = ci
SPEC.loader.exec_module(ci)


class RunnerTests(unittest.TestCase):
    def test_failure_stops_checks_and_returns_failure(self):
        steps = [ci.Step("first", [sys.executable, "-c", "raise SystemExit(7)"]),
                 ci.Step("second", [sys.executable, "-c", "raise AssertionError('must not run')"])]
        with patch.object(ci, "plan", return_value=steps), patch.object(ci, "preflight"), \
                patch.object(ci, "run_step", wraps=ci.run_step) as run, \
                contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(1, ci.main(["--stage", "pack-node"]))
        self.assertEqual(1, run.call_count)

    def test_plan_runs_no_commands_or_preflight(self):
        with patch.object(ci, "preflight") as preflight, patch.object(ci.subprocess, "run") as run, \
                contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, ci.main(["--plan"]))
        preflight.assert_not_called()
        run.assert_not_called()

    def test_selected_step_runs_only_that_check(self):
        with patch.object(ci, "preflight"), patch.object(ci, "run_step") as run, \
                contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, ci.main(["--stage", "resources", "--step", "lint-python-scripts"]))
        self.assertEqual(["lint-python-scripts"], [call.args[0].key for call in run.call_args_list])

    def test_unknown_step_fails_without_running_commands(self):
        with patch.object(ci, "preflight") as preflight, patch.object(ci, "run_step") as run, \
                contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(1, ci.main(["--stage", "resources", "--step", "typo"]))
        preflight.assert_not_called()
        run.assert_not_called()

    def test_resource_step_does_not_require_dotnet_node_or_media_tools(self):
        options = ci.parser().parse_args(["--stage", "resources", "--step", "lint-python-scripts"])
        with patch.object(ci.shutil, "which", side_effect=lambda name, **kwargs: "/bin/uv" if name == "uv" else None), \
                patch.object(ci.subprocess, "check_output") as check:
            ci.preflight(options)
        check.assert_not_called()

    def test_full_solution_build_requires_native_workload(self):
        options = ci.parser().parse_args(["--stage", "dotnet", "--step", "build-release"])
        with patch.object(ci.shutil, "which", return_value="dotnet"), \
                patch.object(ci.subprocess, "check_output", side_effect=["10.0.401", "No workloads"]):
            with self.assertRaisesRegex(ValueError, "wasm-tools"):
                ci.preflight(options)

    def test_node_build_requires_workload_instead_of_reducing_coverage(self):
        options = ci.parser().parse_args(["--stage", "node", "--step", "build-node-js-package"])
        with patch.object(ci.shutil, "which", return_value="dotnet"), \
                patch.object(ci.subprocess, "check_output", side_effect=["v22.16.0", "10.0.401", "No workloads"]):
            with self.assertRaisesRegex(ValueError, "wasm-tools"):
                ci.preflight(options)

    def test_browser_publish_uses_same_runtime_without_native_workload(self):
        options = ci.parser().parse_args(["--stage", "browser", "--step", "publish-release-wasm"])
        with patch.object(ci.shutil, "which", return_value="dotnet"), \
                patch.object(ci.subprocess, "check_output", return_value="10.0.401") as check:
            ci.preflight(options)
        check.assert_called_once_with(["dotnet", "--version"], text=True, timeout=30)

    def test_timeout_returns_failure(self):
        step = ci.Step("typecheck", ["npm", "run", "typecheck"])
        with patch.object(ci, "plan", return_value=[step]), patch.object(ci, "preflight"), \
                patch.object(ci.subprocess, "run", side_effect=subprocess.TimeoutExpired(step.args, 900)), \
                contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(1, ci.main(["--stage", "pack-node"]))

    def test_actions_error_annotation_names_the_failed_step(self):
        output = io.StringIO()
        errors = io.StringIO()
        step = ci.Step("Browser type check", ["npm", "run", "typecheck"])
        with patch.object(ci, "plan", return_value=[step]), patch.object(ci, "preflight"), \
                patch.object(ci, "run_step", side_effect=ValueError("first line\nsecond line%")), \
                patch.dict(ci.os.environ, {"GITHUB_ACTIONS": "true"}), \
                contextlib.redirect_stdout(output), contextlib.redirect_stderr(errors):
            self.assertEqual(1, ci.main(["--stage", "browser"]))
        self.assertIn("CI check failed [browser-type-check]", errors.getvalue())
        self.assertIn("::error title=CI check browser-type-check::first line%0Asecond line%25", output.getvalue())

    def test_step_sets_ci_and_scoped_environment_without_changing_parent(self):
        before = dict(os.environ)
        with patch.object(ci.subprocess, "run") as run, contextlib.redirect_stdout(io.StringIO()):
            ci.run_step(ci.Step("prepare", [sys.executable, "--version"], ci.E2E,
                                {"CHAPTERTOOL_E2E_PUBLISH": "artifacts/wasm-e2e/publish/wwwroot"}), ci.ROOT)
        self.assertEqual(ci.ROOT / ci.E2E, run.call_args.kwargs["cwd"])
        self.assertEqual("true", run.call_args.kwargs["env"]["CI"])
        self.assertEqual("1", run.call_args.kwargs["env"]["BINARYEN_CORES"])
        self.assertEqual("artifacts/wasm-e2e/publish/wwwroot", run.call_args.kwargs["env"]["CHAPTERTOOL_E2E_PUBLISH"])
        self.assertEqual(before, dict(os.environ))

    def test_windows_mkvtoolnix_install_is_visible_to_child_processes(self):
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary) / "MKVToolNix"
            directory.mkdir()
            for tool in ("mkvextract", "mkvmerge"):
                (directory / f"{tool}.exe").touch()
            with patch.object(ci.platform, "system", return_value="Windows"), \
                    patch.dict(os.environ, {"ProgramFiles": temporary, "PATH": "original"}), \
                    patch.object(ci.subprocess, "run") as run, contextlib.redirect_stdout(io.StringIO()):
                ci.run_step(ci.Step("test", [sys.executable, "--version"]), ci.ROOT)
                self.assertEqual("original", os.environ["PATH"])
        self.assertEqual(os.pathsep.join(("original", str(directory))), run.call_args.kwargs["env"]["PATH"])

    def test_browser_publish_removes_stale_output_and_keeps_diagnostics(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            output = root / "artifacts/wasm-e2e/publish"
            output.mkdir(parents=True)
            (output / "stale.wasm").touch()
            diagnostics = output.parent / "report"
            diagnostics.mkdir()
            with patch.object(ci.subprocess, "run") as run, contextlib.redirect_stdout(io.StringIO()):
                ci.run_step(ci.browser_steps("pr", False)[1], root)
            self.assertFalse(output.exists())
            self.assertTrue(diagnostics.exists())
            run.assert_called_once()

    def test_solution_membership_drives_separate_test_processes(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "ChapterTool.slnx").write_text(
                '<Solution><Folder><Project Path="tests/One.Tests/One.Tests.csproj" />'
                '<Project Path="tests\\Two.Tests\\Two.Tests.csproj" />'
                '<Project Path="tests/Shared/Shared.csproj" /></Folder></Solution>', encoding="utf-8")
            tests = [step for step in ci.build_steps(root) if step.args[:2] == ["dotnet", "test"]]
        self.assertEqual(["tests/One.Tests/One.Tests.csproj", "tests/Two.Tests/Two.Tests.csproj"], [step.args[2] for step in tests])
        self.assertTrue(all("--no-build" in step.args and "--no-restore" in step.args for step in tests))

    def test_full_acceptance_installs_all_engines(self):
        steps = ci.browser_steps("full", True)
        install = next(step for step in steps if step.name == "Install browser engines")
        self.assertEqual(["--with-deps", "chromium", "firefox", "webkit"], install.args[-4:])
        self.assertEqual({}, steps[-1].env)

    def test_engine_selection_keeps_full_coverage_in_isolated_jobs(self):
        for engine in ("chromium", "firefox", "webkit"):
            with self.subTest(engine=engine):
                steps = ci.browser_steps("full", True, engine)
                install = next(step for step in steps if step.key == "install-browser-engines")
                self.assertEqual(["--with-deps", engine], install.args[-2:])
                self.assertEqual(["npm", "run", "test:e2e", "--", f"--project={engine}"], steps[-1].args)
                self.assertEqual({"E2E_RUN_NAME": engine}, steps[-1].env)

    def test_pr_webkit_selection_does_not_run_chromium(self):
        steps = ci.browser_steps("pr", False, "webkit")
        tests = [step for step in steps if "test:e2e" in step.args]
        self.assertEqual(1, len(tests))
        self.assertEqual("run-webkit-regressions", tests[0].key)
        self.assertEqual(["modal-layout.spec.ts", "unified-editing.spec.ts", "layout-behavior.spec.ts"], tests[0].args[-3:])
        self.assertEqual("webkit", tests[0].env["E2E_RUN_NAME"])

    def test_isolated_project_build_needs_no_native_workload(self):
        options = ci.parser().parse_args(["--stage", "test-dotnet", "--test-project", "ChapterTool.Core.Tests"])
        steps = ci.plan(options, ci.ROOT)
        self.assertEqual(["restore-test-project", "build-test-project", "run-tests"], [step.key for step in steps])
        self.assertTrue(all(step.args[2].endswith("ChapterTool.Core.Tests.csproj") for step in steps))
        with patch.object(ci.shutil, "which", return_value="dotnet"), \
                patch.object(ci.subprocess, "check_output", return_value="10.0.401") as check:
            ci.preflight(options)
        check.assert_called_once_with(["dotnet", "--version"], text=True, timeout=30)

    def test_unknown_test_project_fails_before_running_commands(self):
        with patch.object(ci, "run_step") as run, contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(1, ci.main(["--stage", "test-dotnet", "--test-project", "../Other"]))
        run.assert_not_called()

    def test_isolated_media_consumers_require_real_tools(self):
        for project in ("ChapterTool.Infrastructure.Tests", "ChapterTool.Avalonia.Tests"):
            with self.subTest(project=project):
                options = ci.parser().parse_args(["--stage", "test-dotnet", "--test-project", project, "--step", "run-tests"])
                with patch.object(ci.shutil, "which", side_effect=lambda name, **kwargs: "dotnet" if name == "dotnet" else None), \
                        patch.object(ci.subprocess, "check_output", return_value="10.0.401"):
                    with self.assertRaisesRegex(ValueError, "ffmpeg.*ffprobe.*mkvextract.*mkvmerge"):
                        ci.preflight(options)

    def test_release_nuget_version_applies_to_build_and_pack(self):
        steps = ci.nuget_steps("23.3.2-rc.1")
        versioned = [step for step in steps if step.args[:2] in (["dotnet", "build"], ["dotnet", "pack"])]
        self.assertEqual(4, len(versioned))
        self.assertTrue(all("-p:Version=23.3.2-rc.1" in step.args and "-p:PackageVersion=23.3.2-rc.1" in step.args for step in versioned))
        self.assertEqual("verify-nuget-consumers", steps[-1].key)
        with self.assertRaisesRegex(ValueError, "Invalid NuGet"):
            ci.nuget_steps("--invalid")

    def test_invalid_engine_for_suite_fails_before_commands(self):
        with self.assertRaisesRegex(ValueError, "not part of"):
            ci.browser_steps("pr", False, "firefox")

    def test_unsupported_macos_packaging_fails_before_commands_run(self):
        with patch.object(ci.platform, "system", return_value="Windows"):
            with self.assertRaisesRegex(ValueError, "macOS host"):
                ci.desktop_steps(["osx-arm64"])

    def test_windows_linux_publish_uses_git_bash(self):
        with patch.object(ci.os, "name", "nt"), patch.object(ci, "bash_executable", return_value="git-bash"):
            steps = ci.desktop_steps(["linux-x64"])
        self.assertEqual(["git-bash", "scripts/publish.sh"], steps[-1].args[:2])


if __name__ == "__main__":
    unittest.main()
