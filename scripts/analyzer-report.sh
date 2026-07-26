#!/usr/bin/env bash
# Build the solution, keep the compiler SARIF report, and summarize diagnostics.
# Usage:
#   ./scripts/analyzer-report.sh
#   ./scripts/analyzer-report.sh -Configuration Debug -NoRestore
#   ./scripts/analyzer-report.sh -Prefix SA
set -uo pipefail

Configuration="Release"
NoRestore="false"
Prefix=""
Output=""

usage() {
  sed -n '2,7p' "$0"
  echo
  echo "Options:"
  echo "  -Configuration <name>  Build configuration. Default: Release."
  echo "  -NoRestore             Pass --no-restore to dotnet build."
  echo "  -Prefix <text>         Keep diagnostics whose rule id starts with this text."
  echo "  -Output <path>         SARIF output path."
}

require_value() {
  local option="$1"
  local value="${2-}"
  if [[ -z "$value" || "$value" == -* ]]; then
    echo "ERROR: $option requires a value" >&2
    exit 2
  fi
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    -Configuration)
      require_value "$1" "${2-}"
      Configuration="$2"
      shift 2
      ;;
    -Configuration=*)
      Configuration="${1#*=}"
      shift
      ;;
    -NoRestore)
      NoRestore="true"
      shift
      ;;
    -Prefix)
      require_value "$1" "${2-}"
      Prefix="$2"
      shift 2
      ;;
    -Prefix=*)
      Prefix="${1#*=}"
      shift
      ;;
    -Output)
      require_value "$1" "${2-}"
      Output="$2"
      shift 2
      ;;
    -Output=*)
      Output="${1#*=}"
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
repo_root="$(cd "$script_dir/.." && pwd -P)"
solution="$repo_root/ChapterTool.slnx"
report="${Output:-$repo_root/artifacts/analyzers/analyzers.sarif}"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "ERROR: dotnet was not found on PATH" >&2
  exit 1
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "ERROR: jq was not found on PATH" >&2
  echo "Install jq before running this script." >&2
  exit 1
fi

if [[ ! -f "$solution" ]]; then
  echo "ERROR: solution was not found at '$solution'" >&2
  exit 1
fi

report_dir="$(dirname "$report")"
mkdir -p "$report_dir"
rm -f "$report"

build_args=(
  build "$solution"
  --configuration "$Configuration"
  "-p:ErrorLog=$report"
)

if [[ "$NoRestore" == "true" ]]; then
  build_args+=(--no-restore)
fi

echo "Building $solution"
set +e
dotnet "${build_args[@]}"
build_exit_code=$?
set -e

if [[ ! -f "$report" ]]; then
  jq -n '{version: "1.0.0", runs: []}' > "$report"
fi

normalized="$(jq -c \
  --arg prefix "$Prefix" \
  '[.runs[]?.results[]?
    | select($prefix == "" or ((.ruleId // "") | startswith($prefix)))
    | {
        ruleId: (.ruleId // "<unknown-rule>"),
        level: (.level // "none"),
        message: ((.message // "")
          | if type == "object" then (.text // .markdown // "") else tostring end),
        file: (.locations[0].physicalLocation.artifactLocation.uri // "<no-file>"),
        line: (.locations[0].physicalLocation.region.startLine // 0),
        column: (.locations[0].physicalLocation.region.startColumn // 0)
      }]' "$report")"

count="$(jq 'length' <<<"$normalized")"
echo
if [[ -n "$Prefix" ]]; then
  echo "Diagnostics with rule prefix '$Prefix': $count"
else
  echo "Diagnostics: $count"
fi
echo "SARIF report: $report"

if [[ "$count" -eq 0 ]]; then
  echo "No matching diagnostics were found."
  exit "$build_exit_code"
fi

echo
echo "By rule"
jq -r '
  group_by(.ruleId)
  | .[]
  | "  \(.[0].ruleId) \(length)"' <<<"$normalized"

echo
echo "By severity"
jq -r '
  group_by(.level)
  | .[]
  | "  \(.[0].level) \(length)"' <<<"$normalized"

echo
echo "By file"
jq -r '
  group_by(.file)
  | .[]
  | "  \(.[0].file) \(length)"' <<<"$normalized"

echo
echo "Diagnostics"
jq -r '.[] | "  \(.ruleId) [\(.level)] \(.file):\(.line):\(.column) \(.message)"' <<<"$normalized"

exit "$build_exit_code"
