# Repository Scripts

These scripts support the repository tasks that users and maintainers run around ChapterTool: verify a change, produce a release artifact, keep generated localization files aligned, and inspect resource or analyzer output. Run them from the repository root unless a script says otherwise.

The tables below show what each script helps you do and what it needs to run.

## Verification And Release

| Script | Runtime and platform | Main dependencies | Use |
| --- | --- | --- | --- |
| `check-ci.py` | Python 3.9+; cross-platform | Python standard library; see prerequisites below | Run the checks shared with GitHub Actions. |
| `ci/review-wasm-baselines.py` | Python 3.9+; cross-platform | Python standard library | Extract a Linux baseline ZIP for review. Apply reviewed changes with `--apply`. |
| `test-coverage.py` | Python 3; cross-platform | Python standard library, `defusedxml` (uv-managed), .NET SDK; optional `reportgenerator` | Build test projects, run their assemblies through VSTest, and collect coverage. |
| `report-analyzers.py` | Python 3; cross-platform | Python standard library, .NET SDK | Build the solution and summarize compiler and analyzer diagnostics. |
| `publish.sh` | Bash; Unix-like hosts or Git Bash | .NET SDK | Publish and validate Linux, macOS, or Windows runtime artifacts. macOS bundles require a macOS host. |
| `publish.ps1` | PowerShell 7; Windows | .NET SDK | Publish and validate Windows runtime artifacts. |

The CI workflows call `check-ci.py` for build, test, browser, and packaging checks. The script calls `axaml-to-json.py --check` and the publish scripts. Release jobs use the same publish entry point. `publish.ps1` remains the Windows-native publish entry point.

### Check a change before pushing

Run this cross-platform script from the repository root:

```powershell
python scripts/check-ci.py
```

The script runs directly on Windows, macOS, or Linux. It shares build, test, browser, and packaging commands with `.github/workflows/dotnet-ci.yml` and `.github/workflows/wasm-browser-e2e.yml`. It uses `CI=true`, Release output, and locked Python and npm dependencies.

The default checks run in this order:

1. Check generated translations and Python lint. Run CI runner tests.
2. Validate PowerShell publish syntax. Restore and build `ChapterTool.slnx` in Release. Pack both NuGet packages. Run all .NET test projects sequentially in separate processes.
3. Build the Node.js WASM package. Run Node.js type checks and built-package tests.
4. Publish browser WASM in Release. Prepare the `/ChapterTool/` site. Run browser type checks, Chromium workflows, and WebKit modal and editing regressions.
5. Pack the npm tarball. Install it in a temporary consumer and run its Core API.
6. Publish and validate desktop artifacts. Windows and Linux hosts cover `win-x64` and `linux-x64`. Windows uses Git Bash for Linux cross-publishing. macOS hosts cover the `osx-arm64` DMG.

The script stops at the first failed check and returns a nonzero exit code. The default checks do not regenerate translations or screenshot baselines. Browser reports remain under `artifacts/wasm-e2e/`. Packages use the existing `artifacts/nuget/`, `artifacts/cli-nuget/`, `artifacts/npm/`, and `artifacts/publish/` directories.

### Prerequisites

Install Python 3.9+, .NET SDK 10.x, Node.js 22.x, uv, PowerShell 7, ffmpeg, and mkvtoolnix. These tools must be on `PATH`. On Windows, the script also finds MKVToolNix under `ProgramFiles`, `ProgramFiles(x86)`, and `LOCALAPPDATA`. It adds the discovered directory to child-process `PATH`. It does not change the user or system environment. Linux and macOS publish checks need Bash. Windows Linux cross-publishing needs Git for Windows with Git Bash.

Install the native WASM build workload once:

```powershell
dotnet workload install wasm-tools
```

The script installs locked Python and npm dependencies and the selected Playwright engines. Linux hosts may use `--install-browser-deps` to install Playwright system packages. Missing tools fail the gate before the build. They do not silently reduce test coverage.

The Python sync uses `--locked --no-build --no-install-project`. The checks need the dependencies but do not need the repository's editable Python package. This avoids a clean-environment failure with `--no-build`.

### Run selected checks

```powershell
# Inspect commands without running them.
python scripts/check-ci.py --plan

# Run only the build, resource, unit test, and NuGet gates.
python scripts/check-ci.py --stage build-test

# Run one gate. Its dependencies must already be prepared.
python scripts/check-ci.py --stage dotnet --step test-chaptertool-core-tests

# Check resource generation and script behavior.
python scripts/check-ci.py --stage resources

# Build and test the Node.js package independently.
python scripts/check-ci.py --stage node

# Publish and test browser workflows.
python scripts/check-ci.py --stage browser

# Review visual workflows on the current host after preparing the browser site.
python scripts/check-ci.py --stage visual-review

# Include the weekly three-engine acceptance suite.
python scripts/check-ci.py --browser-suite full

# Verify one desktop runtime.
python scripts/check-ci.py --stage pack-desktop --runtime win-x64
```

`--plan` prints the step key, name, directory, and command. `--step <key>` runs only that check. The option can be repeated. It does not run prerequisite steps. Unknown keys return a nonzero exit code. CI calls individual keys so each failure has a named Actions step and an error annotation.

`--stage pack-node` requires the `dist` output from `node` or `build-test`. The Node.js CI job builds this output once, tests it, and packs it in the same job. `--stage visual` requires the prepared site from `browser` and the matching Linux screenshot environment.

### CI scheduling

The `.NET 10 CI` workflow starts resource checks, .NET build and tests, Node.js build and packaging, the desktop runtime matrix, and the reusable browser workflow independently. Desktop packaging does not consume unit-test output. Each job uses a separate checkout. The release workflows still require a successful CI workflow, including browser checks, before they publish its artifacts.

The `WASM browser acceptance` workflow publishes and prepares one Release site. It uploads the site for the browser matrix and screenshot job. `.NET 10 CI` calls this workflow for pushes and pull requests. Chromium and WebKit run in parallel. Scheduled and manual runs use Chromium, Firefox, and WebKit in separate jobs. Full acceptance disables retries. The screenshot job runs alongside the browser matrix in the existing pinned Debian baseline environment. The reusable workflow avoids duplicate Chromium runs while keeping browser checks in the release gate.

Local checks run sequentially. Shared .NET project outputs must not be built or tested concurrently in one checkout. Browser jobs select one engine with `--browser-engine`. They share the commands used by the local browser suite.

The .NET solution check builds all projects in `ChapterTool.slnx`, including Node. It requires `wasm-tools`. The Node project keeps its native compilation and invariant globalization settings. Node.js packaging and browser publishing run in separate jobs with their own output checks.

Browser publishing uses the SDK prebuilt WASM runtime, as the original browser CI did without `wasm-tools`. The shared publish command sets `WasmBuildNative=false` and `WasmRunWasmOpt=false`. Installing the Node build workload on a local host must not change the browser artifact under test. These properties apply only to the browser publish command.

The browser publish step removes its generated publish directory first. Old hashed runtime files must not affect a local check. Browser reports and other artifact directories remain available.

### Review intentional screenshot changes

Run `WASM browser acceptance` manually with `update_visual_snapshots` enabled after an intentional layout change. This run prepares the Release site and generates baselines in the pinned Linux screenshot environment. It uploads `wasm-linux-baselines`. It does not run the browser acceptance matrix. The visual tests must still pass their workflow and geometry assertions. This mode disables retries.

Download the artifact. Run `python scripts/ci/review-wasm-baselines.py <downloaded-zip>` to extract its images into `artifacts/wasm-e2e/baseline-review/` and list the changes. Review each changed image. Run the same command with `--apply` to copy reviewed changes into `tests/ChapterTool.Wasm.E2E/specs/layout.spec.ts-snapshots/`. The tool must reject missing or duplicate baseline names before it writes committed images. Commit the reviewed images. The subsequent push must pass normal screenshot comparison. Push, pull request, and scheduled checks never regenerate baselines.

On a matching Linux host, the equivalent command is `python3 scripts/check-ci.py --stage visual --update-visual-snapshots`. Windows and macOS cannot generate the Linux baseline images. A baseline must not hide an unintended layout change or a host difference.

`python scripts/check-ci.py --stage visual-review` runs the same visual workflow assertions on Windows, macOS, or Linux. It requires the prepared site from `--stage browser`. It writes host review images under `artifacts/wasm-e2e/review-snapshots/`. It does not change or compare the committed Linux baselines. It disables retries. The Playwright configuration is `tests/ChapterTool.Wasm.E2E/playwright.review.config.ts`.

### Platform limits

The default command runs all shared host checks. It prints the remaining platform checks after success. Windows and macOS cannot compare Linux screenshot baselines. Linux filename and path behavior still require a Linux test run. DMG creation requires macOS. GitHub credentials, artifact uploads, and deployment permissions remain CI checks.

Browser behavior tests include responsive geometry, focus, real clicks, and modal workflows on each host. Fixed screenshot comparisons remain in the existing CI baseline environment. Run `python3 scripts/check-ci.py --stage visual` only in a matching Linux environment. Do not update baselines to hide a host difference.

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
