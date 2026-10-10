# Repository Scripts

These scripts support the repository tasks that users and maintainers run around ChapterTool: verify a change, produce a release artifact, keep generated localization files aligned, and inspect resource or analyzer output. Run them from the repository root unless a script says otherwise.

The tables below show what each script helps you do and what it needs to run.

## Verification And Release

| Script | Runtime and platform | Main dependencies | Use |
| --- | --- | --- | --- |
| `check-ci.py` | Python 3.9+; cross-platform | See prerequisites below | Run the checks shared with GitHub Actions. |
| `ci/plan-ci.py` | Python 3.9+; cross-platform | Git; Python standard library | Select affected project consumers. Verify required CI job results. |
| `ci/verify-nuget.py` | Python 3.9+; cross-platform | .NET SDK | Install and run packed Core and CLI consumers. Verify release package versions. |
| `ci/resolve-ci-run.py` | Python 3.9+; cross-platform | Git; GitHub CLI | Resolve a successful tag CI run for the checked-out commit. |
| `test-coverage.py` | Python 3; cross-platform | .NET SDK; uv dependencies; optional `reportgenerator` | Collect test coverage. |
| `report-analyzers.py` | Python 3; cross-platform | .NET SDK | Build the solution and summarize analyzer diagnostics. |
| `publish.sh` | Bash; Unix-like hosts or Git Bash | .NET SDK | Publish and validate desktop artifacts. macOS bundles require macOS. |
| `publish.ps1` | PowerShell 7; Windows | .NET SDK | Publish and validate Windows artifacts. |

`check-ci.py` owns build, test, browser, and packaging commands. CI workflows must select its stages and step keys. Publish jobs use the tested CI artifacts.

### Check a change before pushing

Run from the repository root:

```powershell
python scripts/check-ci.py
```

The script uses `CI=true`, Release output, and locked Python and npm dependencies. Local checks run sequentially. The script stops at the first failed check and returns a nonzero exit code.

The default checks run in this order:

1. Check generated translations and Python lint. Run CI script tests. Validate PowerShell publish syntax.
2. Restore and build `ChapterTool.slnx`. Pack both NuGet packages. Install and run isolated package consumers. Run all .NET test projects in separate processes.
3. Build the Node.js WASM package. Run type checks and built-package tests.
4. Publish browser WASM. Prepare the `/ChapterTool/` site. Run Chromium workflows and WebKit modal, editing, and layout regressions.
5. Pack the npm tarball. Install it in a temporary consumer and run its Core API.
6. Publish and validate desktop artifacts for the current host.

The script does not regenerate translations. Browser reports use `artifacts/wasm-e2e/`. Packages use `artifacts/nuget/`, `artifacts/cli-nuget/`, `artifacts/npm/`, and `artifacts/publish/`.

### Prerequisites

The full local gate requires Python 3.9+, .NET SDK 10.x, Node.js 22.x, uv, PowerShell 7, ffmpeg, and mkvtoolnix. These tools must be on `PATH`. Selected stages require only their own tools. Infrastructure tests require real media tools.

On Windows, the script also finds MKVToolNix under `ProgramFiles`, `ProgramFiles(x86)`, and `LOCALAPPDATA`. It changes only the child-process `PATH`. Linux cross-publishing on Windows requires Git Bash. DMG creation requires macOS.

The full solution and Node package builds require the native WASM workload:

```powershell
dotnet workload install wasm-tools
```

Isolated .NET test projects and browser publishing do not require this workload. The Python sync uses `--locked --no-build --no-install-project`. Linux browser checks may use `--install-browser-deps` to install system packages. Missing tools fail the selected check.

### Run selected checks

```powershell
# Print commands without running them.
python scripts/check-ci.py --plan

# Restore, build, and test one solution test project.
python scripts/check-ci.py --stage test-dotnet --test-project ChapterTool.Core.Tests

# Run one prepared check.
python scripts/check-ci.py --stage test-dotnet --test-project ChapterTool.Core.Tests --step run-tests

# Build packages and run installed consumers.
python scripts/check-ci.py --stage pack-nuget

# Build packages with one release version.
python scripts/check-ci.py --stage pack-nuget --package-version 23.3.2-rc.1

python scripts/check-ci.py --stage resources
python scripts/check-ci.py --stage node
python scripts/check-ci.py --stage browser
python scripts/check-ci.py --stage browser --browser-suite full --browser-engine webkit
python scripts/check-ci.py --stage pack-desktop --runtime win-x64
```

`--step` runs only the selected keys. It does not run prerequisites. Unknown keys fail before execution. `--stage pack-node` requires the `dist` output from `node` or `build-test`.

### CI scheduling

`.NET 10 CI` starts for every push and pull request. It also supports a weekly schedule and manual dispatch. The planning job reads `ChapterTool.slnx` and project references. It maps changed paths to test and host consumers. Shared Core fixtures also select the browser and Node consumers. Renames include both paths. Unknown inputs, CI scripts, and shared build configuration select all checks.

| Event or change | Checks |
| --- | --- |
| Documentation only | Planning and `CI ready`. |
| Pull request or feature branch | Affected tests, resources, browser, Node, and NuGet checks. |
| Desktop dependency project files or shared publish configuration | Desktop runtime matrix. |
| Non-documentation push to `master` | All consumers and distribution artifacts. |
| Version tag, weekly schedule, or manual dispatch | Full solution build, all consumers, all artifacts, and three browser engines. |

Each .NET matrix job restores and builds one test project. Headless uses its own process and checkout. A failure does not cancel the other test projects. Only the Infrastructure job installs media tools. Native WASM tools belong to the Node and full solution jobs.

`CI ready` runs after the selected jobs. It must reject failed, canceled, missing, or unexpectedly skipped required checks. Branch protection can require this fixed check name.

The reusable browser workflow publishes one Release site. Chromium runs the behavior suite. WebKit runs modal, editing, and layout regressions. Full acceptance adds Firefox and runs the complete suite in each engine. Tests use zero retries. CI stops after three failures or ten minutes per browser suite. Each engine uploads its diagnostics.

Browser publishing sets `WasmBuildNative=false` and `WasmRunWasmOpt=false`. A locally installed native workload must not change the browser artifact. The publish step clears its own output directory before building.

### Distribution and deployment

The NuGet job builds and packs both packages with the same tag version. It installs Core in a temporary .NET consumer. It installs the CLI tool and runs `--help`. Package source mapping and a fresh package cache ensure these consumers use the local artifacts.

The NuGet and npm publishers resolve a successful tag push for the exact checked-out SHA. They download its artifacts. They do not rebuild packages. GitHub Release downloads only the six named distribution artifacts. Browser reports and test diagnostics are not release attachments.

Pages deployment runs after `CI ready` on `master` pushes and manual CI runs. It uploads the same `wasm-prepared-site` tested by the browser jobs. To deploy manually, dispatch `.NET 10 CI` on `master`.

### Layout verification and platform limits

Browser E2E and Avalonia Headless tests verify layout and interaction behavior. E2E checks include responsive geometry, focus, real clicks, and long-content scrolling. Screenshots support failure diagnosis or manual review. No pixel baseline generation or comparison is maintained.

`specs/expression-preview-screenshots.spec.ts` always checks English and Chinese layout behavior. Set `E2E_CAPTURE_REVIEW=1` when manual review images are needed. The images use `artifacts/expression-preview/`.

Local checks cannot establish GitHub upload or deployment permissions. Linux filename behavior requires Linux. DMG creation requires macOS. Physical mobile keyboard behavior requires a device check.

### Evidence from recent failures

- Run [37161159868](https://github.com/tautcony/ChapterTool/actions/runs/37161159868) failed translation verification and WASM site preparation. Commits `aab34ce`, `9255568`, and `686d7d2` corrected generated resources, repository-relative paths, and the execution directory.
- Runs [33588611328](https://github.com/tautcony/ChapterTool/actions/runs/33588611328) and [33594442570](https://github.com/tautcony/ChapterTool/actions/runs/33594442570) also failed translation verification.
- Run [37167108459](https://github.com/tautcony/ChapterTool/actions/runs/37167108459) failed the .NET test step. A successful test run for one project does not cover the full solution.
- Commit `dc666da` corrected platform-specific newline expectations. Commits `5997fb3` and `1c58b1b` corrected filename extraction across platforms.

The shared entry point checks resources and workflows before a push. Platform CI jobs also verify operating system behavior. Keep shared build, test, and packaging commands in `check-ci.py`. CI workflows must select those stages and step keys instead of duplicating commands.

## Repository Maintenance

| Script | Runtime and platform | Main dependencies | Use |
| --- | --- | --- | --- |
| `axaml-to-json.py` | Python 3; cross-platform | Python standard library | Generate Wasm locale JSON files from Avalonia AXAML files. Use `--check` to detect drift without writing files. |
| `audit-ui-resources.py` | Python 3; cross-platform | Python standard library | Audit Avalonia resource definitions and references. |
| `normalize-changed-text-files.py` | Python 3; cross-platform | Python standard library, Git | Normalize line endings and UTF-8 BOMs in changed text files. Use `--what-if` for a read-only check. |
| `generate-app-icons-macos.sh` | Bash; macOS only | ImageMagick, `iconutil` | Generate ICNS and ICO files from the SVG icon source. |
| `subset-shortcut-font.py` | Python 3; cross-platform | FontTools (`pyftsubset`) | Keep only the ASCII gesture text and platform-neutral shortcut symbols in the bundled Avalonia font. |

Maintenance scripts are manual entry points. They do not run as hidden build steps.

### Subset the Avalonia shortcut font

Run this command from the repository root after changing shortcut display text:

```bash
uv run --project scripts subset-shortcut-font
```

The script invokes `pyftsubset` with a UTF-8 character file, preserves the
`vert`, `vrtr`, `vrt2`, and `vkna` layout features, and writes the result to
`src/ChapterTool.Avalonia.UI/Assets/Fonts/Iosevka-Regular.ttf` from the
original font at `src/ChapterTool.Avalonia.UI/Assets/Fonts/source/`. Use
`--check` to verify that the checked-in font is current. Use `--extra-text`
when a new shortcut symbol is added.

The equivalent verification commands are `uv run --project scripts
check-shortcut-font` and `uv run --project scripts lint-scripts`.


## Configuration

| Script | Runtime and platform | Main dependencies | Use |
| --- | --- | --- | --- |
| `coverage.runsettings` | .NET test configuration | Coverlet | Configure coverage collection. |

The scripts under `packages/chaptertool/scripts/` belong to the npm package. They use Node.js modules and are invoked through the package scripts in `packages/chaptertool/package.json`.

Run Python scripts with Python 3. Run Bash scripts with Bash. Run PowerShell scripts with PowerShell 7 (`pwsh`).

`scripts/pyproject.toml` pins Python dependencies for scripts that need third-party packages (`test-coverage.py`, `axaml-to-json.py`). Install them once with `uv sync --project scripts`, then run the script through `uv run --project scripts scripts/<name>.py ...` so the virtual environment is used. CI installs `uv` and invokes `axaml-to-json.py` through `uv run`.

Use `-SkipHtml` with `test-coverage.py` when you only need XML coverage output.

`ruff` is a dev dependency of the same environment. Run `uv run --project scripts ruff check scripts/` to lint the Python scripts. Rule `C901` keeps function cyclomatic complexity under 16. This matches the `CA1502: 16` threshold in `CodeMetricsConfig.txt`. Rules `E701` through `E703` require one statement per line. CI runs the same check.

### Check Avalonia Headless xUnit compatibility

Run this command to inspect the latest stable `Avalonia.Headless.XUnit` package
and its declared xUnit extensibility dependency:

```bash
python3 scripts/check-avalonia-headless-xunit.py \
  --project tests/ChapterTool.Avalonia.Headless.Tests/ChapterTool.Avalonia.Headless.Tests.csproj
```

Use `--version <version>` to inspect a specific NuGet version.
