# Repository Scripts

Each script name identifies its action. The file extension identifies the runtime. Platform-specific names identify a required host platform.

| Script | Runtime and platform | Main dependencies | Use |
| --- | --- | --- | --- |
| `axaml-to-json.py` | Python 3; cross-platform | Python standard library | Generate or check Wasm locale JSON files from Avalonia AXAML files. |
| `audit-ui-resources.py` | Python 3; cross-platform | Python standard library | Audit Avalonia resource definitions and references. |
| `normalize-changed-text-files.py` | Python 3; cross-platform | Python standard library, Git | Normalize line endings and UTF-8 BOMs in changed text files. |
| `test-coverage.py` | Python 3; cross-platform | Python standard library, .NET SDK; optional `reportgenerator` | Run test projects in sequence and collect coverage. |
| `report-analyzers.py` | Python 3; cross-platform | Python standard library, .NET SDK | Build the solution and summarize SARIF diagnostics. |
| `publish.sh` | Bash; Unix-like hosts or Git Bash | .NET SDK | Publish and validate Linux, macOS, or Windows runtime artifacts. macOS bundles require a macOS host. |
| `publish.ps1` | PowerShell 7; Windows | .NET SDK | Publish and validate Windows runtime artifacts. |
| `generate-app-icons-macos.sh` | Bash; macOS only | ImageMagick, `iconutil` | Generate ICNS and ICO files from the SVG icon source. |
| `coverage.runsettings` | .NET test configuration | Coverlet | Configure coverage collection. |

The two publish scripts are separate entry points because macOS bundle creation and Windows publishing use different platform APIs. Their output layout and option names must remain aligned.

Each publish script rejects debug symbols and development diagnostics. Single-file publication also rejects duplicate top-level assemblies.

The scripts under `packages/chaptertool/scripts/` belong to the npm package. They use Node.js modules and are invoked through the package scripts in `packages/chaptertool/package.json`.

Run Python scripts with Python 3. Run Bash scripts with Bash. Run PowerShell scripts with PowerShell 7 (`pwsh`).
