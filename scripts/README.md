# Repository Scripts

These scripts support the repository tasks that users and maintainers run around ChapterTool: verify a change, produce a release artifact, keep generated localization files aligned, and inspect resource or analyzer output. Run them from the repository root unless a script says otherwise.

The tables below show what each script helps you do and what it needs to run.

## Verification And Release

| Script | Runtime and platform | Main dependencies | Use |
| --- | --- | --- | --- |
| `test-coverage.py` | Python 3; cross-platform | Python standard library, `defusedxml` (uv-managed), .NET SDK; optional `reportgenerator` | Build test projects, run their assemblies through VSTest, and collect coverage. |
| `report-analyzers.py` | Python 3; cross-platform | Python standard library, .NET SDK | Build the solution and summarize compiler and analyzer diagnostics. |
| `publish.sh` | Bash; Unix-like hosts or Git Bash | .NET SDK | Publish and validate Linux, macOS, or Windows runtime artifacts. macOS bundles require a macOS host. |
| `publish.ps1` | PowerShell 7; Windows | .NET SDK | Publish and validate Windows runtime artifacts. |

The CI workflow calls `axaml-to-json.py --check` and `publish.sh` directly. Release jobs use the same publish entry point. `publish.ps1` remains the Windows-native entry point for local release work and for PowerShell validation in CI.

## Repository Maintenance

| Script | Runtime and platform | Main dependencies | Use |
| --- | --- | --- | --- |
| `axaml-to-json.py` | Python 3; cross-platform | Python standard library | Generate Wasm locale JSON files from Avalonia AXAML files. Use `--check` to detect drift without writing files. |
| `audit-ui-resources.py` | Python 3; cross-platform | Python standard library | Audit Avalonia resource definitions and references. |
| `normalize-changed-text-files.py` | Python 3; cross-platform | Python standard library, Git | Normalize line endings and UTF-8 BOMs in changed text files. Use `--what-if` for a read-only check. |
| `generate-app-icons-macos.sh` | Bash; macOS only | ImageMagick, `iconutil` | Generate ICNS and ICO files from the SVG icon source. |

Maintenance scripts are manual entry points. They do not run as hidden build steps.


## Configuration

| Script | Runtime and platform | Main dependencies | Use |
| --- | --- | --- | --- |
| `coverage.runsettings` | .NET test configuration | Coverlet | Configure coverage collection. |

The scripts under `packages/chaptertool/scripts/` belong to the npm package. They use Node.js modules and are invoked through the package scripts in `packages/chaptertool/package.json`.

Run Python scripts with Python 3. Run Bash scripts with Bash. Run PowerShell scripts with PowerShell 7 (`pwsh`).

`scripts/pyproject.toml` pins Python dependencies for scripts that need third-party packages (`test-coverage.py`, `axaml-to-json.py`). Install them once with `uv sync --project scripts`, then run the script through `uv run --project scripts scripts/<name>.py ...` so the virtual environment is used. CI installs `uv` and invokes `axaml-to-json.py` through `uv run`.

Use `-SkipHtml` with `test-coverage.py` when you only need XML coverage output.

`ruff` is a dev dependency of the same environment. Run `uv run --project scripts ruff check scripts/` to lint the Python scripts. Rule `C901` keeps function cyclomatic complexity under 16. This matches the `CA1502: 16` threshold in `CodeMetricsConfig.txt`. Rules `E701` through `E703` require one statement per line. CI runs the same check.
