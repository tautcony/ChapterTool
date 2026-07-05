# Phase 5 — Build / Publish Scripts Review

> Scope: `scripts/**`, `.github/workflows/dotnet-ci.yml`, `.gitignore`.
> Reviewer: sub-agent bg_183f5248 + main-agent verification.

## Phase Summary

No P0/P1 found. `publish.sh` has `set -euo pipefail`, no `eval`/backticks, mostly correct quoting. Main risks are argument-parse robustness under `set -u` and interrupt resilience during macOS bundle restructuring. `publish.ps1` is solid PowerShell. CI workflow change (blame-hang flags) is benign.

## Files Reviewed

- `scripts/publish.sh` (new, 103 lines)
- `scripts/publish.ps1` (modified, 63 lines)
- `.github/workflows/dotnet-ci.yml` (1 line changed)
- `.gitignore` (1 line: `.omo/`)

## Shell-script Audit (publish.sh)

| Line | Pattern | Verdict |
|------|---------|---------|
| `:8` | `set -euo pipefail` | ✅ |
| throughout | quoting | ✅ mostly correct (`"$repo_root"`, `"$output"`) |
| `:8` | no `eval`/backticks | ✅ |
| `:76` | `mkdir -p` | ✅ idempotent |
| `:38-39` | `BASH_SOURCE[0]` for CWD-independence | ✅ |
| `:50-55` | `dotnet publish` args | ✅ valid |
| `:19,23` | arg value guard | ⚠️ missing `$#` check before consuming `$2` |
| `:79` | `for item in "$output"/*` | ⚠️ no `nullglob` → literal `*` on empty |
| `:82` | `mv "$item"` loop | ⚠️ non-atomic, no trap |
| `:94-95` | `[[ -f ... ]] && chmod +x` | ⚠️ silent skip if exe missing (see Phase 4) |

No secrets/credentials found. RID list consistent with CI matrix (`win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`).

## PowerShell Audit (publish.ps1)

- `param()` + `$ErrorActionPreference = "Stop"` ✅
- CWD-independent via `$PSScriptRoot` ✅
- `Test-Path` guard before `Remove-Item` ✅
- `Copy-Item -Force`, `New-Item -ItemType Directory -Force` ✅
- Missing `[CmdletBinding()]` and `#Requires -Version` (minor)
- No rollback on interrupt during move (`:36-38`) — P3

## Findings

### [P2] Missing value guard in bash arg parsing
- **Location:** `scripts/publish.sh:19, 23`
- **Trigger:** `./scripts/publish.sh -Runtime` (trailing key without value). Under `set -u`, `$2` is unbound → abrupt exit with cryptic shell error.
- **Impact:** Poor operator UX; brittle automation.
- **Fix:**
  ```bash
  [[ $# -ge 2 ]] || { echo "ERROR: $1 requires a value" >&2; exit 2; }
  ```
- **Confidence:** High

### [P2] No trap/cleanup on interrupt during macOS bundle restructuring
- **Location:** `scripts/publish.sh:75-83`
- **Trigger:** SIGINT/termination or command failure mid-loop while moving files into `.app/Contents/MacOS/`.
- **Impact:** Partial bundle state (some files in root, some in `.app`). Re-runs fail or require manual cleanup.
- **Fix:** Add `trap` for `INT TERM` with cleanup of partially built `.app`, or stage into temp dir then atomic `mv` rename.
- **Confidence:** Medium-High

### [P3] Glob-empty edge case on literal pattern
- **Location:** `scripts/publish.sh:79-83`
- **Trigger:** Empty output dir (interrupted prior step).
- **Impact:** `mv` tries literal `*` → noisy failure.
- **Fix:** `shopt -s nullglob` in local scope or guard with array-length check.
- **Confidence:** High

### [P3] publish.ps1 no rollback on interrupt during move
- **Location:** `scripts/publish.ps1:36-38`
- **Trigger:** Interrupt during `Copy-Item`/`Remove-Item` sequence.
- **Impact:** Partial artifact state.
- **Fix:** `try/finally` with cleanup, or stage-then-rename pattern.
- **Confidence:** Medium

## CI Workflow Diff

`.github/workflows/dotnet-ci.yml:30`: `dotnet test` gains `--blame-hang --blame-hang-timeout 10m --blame-hang-dump-type full`. Improves hang diagnostics. Does not alter matrix, runtime list, or publish wiring. **Benign.**

## 漏检复盘 (Missed-pattern Retrospective)

- **默认分支/未知输入**: 参数解析覆盖；发现 bash 缺 `$2` 存在性保护 (P2)。
- **异步/中断路径**: 串行命令链无竞态；中断导致半成品状态风险存在 (P2/P3)。
- **半提交状态窗口**: macOS bundle 重组存在"先删后搬"窗口 (P2)。
- **协议/隐式约定**: README `-Runtime`/`-SelfContained` 与脚本一致；CI matrix 未破坏。
- **安全边界**: 无 `eval`/反引号/未引用命令替换/密钥泄露。
