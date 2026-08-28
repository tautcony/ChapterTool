# Repository Scripts

Each script name identifies one repository task. The file extension identifies the runtime. Platform-specific names identify a required host platform.

## Verification And Release

| Script | Runtime and platform | Main dependencies | Use |
| --- | --- | --- | --- |
| `test-coverage.py` | Python 3; cross-platform | Python standard library, `defusedxml` (uv-managed), .NET SDK; optional `reportgenerator` | Build test projects, run their assemblies through VSTest, and collect coverage. |
| `report-analyzers.py` | Python 3; cross-platform | Python standard library, .NET SDK | Build the solution and summarize SARIF diagnostics. |
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

Maintenance scripts are manual entry points. They do not run as hidden build steps. Run them from the repository root, except when a script documents another working directory.

`audit-ui-resources.py` reports unqualified PascalCase keys as possible framework-template resources and reports namespaced application keys separately. Avalonia Fluent has many implicit resource families, including buttons, check boxes, combo boxes, text boxes, calendars, menus, flyouts, sliders, tabs, and scroll bars. The structural rule avoids a fixed list, but it is not proof of runtime use. Confirm possible framework keys against the active theme and review application candidates before removal.

## Configuration

| Script | Runtime and platform | Main dependencies | Use |
| --- | --- | --- | --- |
| `coverage.runsettings` | .NET test configuration | Coverlet | Configure coverage collection. |

The two publish scripts are separate entry points because macOS bundle creation and Windows publishing use different platform APIs. Their output layout and option names must remain aligned.

Each publish script rejects debug symbols and development diagnostics. Single-file publication also rejects duplicate top-level assemblies.

The scripts under `packages/chaptertool/scripts/` belong to the npm package. They use Node.js modules and are invoked through the package scripts in `packages/chaptertool/package.json`.

Run Python scripts with Python 3. Run Bash scripts with Bash. Run PowerShell scripts with PowerShell 7 (`pwsh`).

`scripts/pyproject.toml` pins Python dependencies for scripts that need third-party packages (`test-coverage.py`, `axaml-to-json.py`). Install them once with `uv sync --project scripts`, then run the script through `uv run --project scripts scripts/<name>.py ...` so the virtual environment is used. CI installs `uv` and invokes `axaml-to-json.py` through `uv run`.
