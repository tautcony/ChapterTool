#!/usr/bin/env bash
# Build and run all test projects sequentially, then collect a Cobertura report.
# Usage:
#   ./scripts/test-coverage.sh
#   ./scripts/test-coverage.sh -Configuration Debug -NoRestore
#   ./scripts/test-coverage.sh -SkipHtml
set -euo pipefail

Configuration="Release"
NoRestore="false"
NoBuild="false"
SkipHtml="false"

usage() {
  echo "Usage: $0 [-Configuration <name>] [-NoRestore] [-NoBuild] [-SkipHtml]"
  echo
  echo "Runs all tests, collects Coverlet Cobertura XML, and optionally creates HTML."
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
    -NoBuild)
      NoBuild="true"
      shift
      ;;
    -SkipHtml)
      SkipHtml="true"
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
output="$repo_root/artifacts/coverage"
results_dir="$output/test-results"
report_dir="$output/html"
runsettings="$repo_root/scripts/coverage.runsettings"

test_projects=(
  "$repo_root/tests/ChapterTool.Core.Tests/ChapterTool.Core.Tests.csproj"
  "$repo_root/tests/ChapterTool.Infrastructure.Tests/ChapterTool.Infrastructure.Tests.csproj"
  "$repo_root/tests/ChapterTool.Avalonia.Tests/ChapterTool.Avalonia.Tests.csproj"
  "$repo_root/tests/ChapterTool.Avalonia.Headless.Tests/ChapterTool.Avalonia.Headless.Tests.csproj"
)

build_projects=(
  "$repo_root/src/ChapterTool.Core/ChapterTool.Core.csproj"
  "$repo_root/src/ChapterTool.Infrastructure/ChapterTool.Infrastructure.csproj"
  "$repo_root/src/ChapterTool.Avalonia/ChapterTool.Avalonia.csproj"
  "${test_projects[@]}"
)

if ! command -v dotnet >/dev/null 2>&1; then
  echo "ERROR: dotnet was not found on PATH" >&2
  exit 1
fi

for project in "${test_projects[@]}"; do
  if [[ ! -f "$project" ]]; then
    echo "ERROR: test project was not found at '$project'" >&2
    exit 1
  fi
done

if [[ ! -f "$runsettings" ]]; then
  echo "ERROR: coverage settings were not found at '$runsettings'" >&2
  exit 1
fi

# Keep each run self-contained so stale coverage files cannot enter the report.
rm -rf "$output"
mkdir -p "$results_dir"

if [[ "$NoBuild" != "true" ]]; then
  # Build referenced projects separately so multi-target Core reference assemblies
  # are complete before each test project is compiled.
  for project in "${build_projects[@]}"; do
    echo "Building $project"
    build_args=(
      build "$project"
      --configuration "$Configuration"
      -p:GenerateRuntimeConfigurationFiles=true
      -p:ProduceReferenceAssembly=true
      -p:GenerateReferenceAssembly=true
    )

    if [[ "$project" == "$repo_root/tests/"* ]]; then
      build_args+=(--no-dependencies)
    fi

    if [[ "$NoRestore" == "true" ]]; then
      build_args+=(--no-restore)
    fi

    dotnet "${build_args[@]}"
  done
fi

for project in "${test_projects[@]}"; do
  echo "Collecting coverage from $project"
  test_args=(
    test "$project"
    --configuration "$Configuration"
    --no-build
    --collect:"XPlat Code Coverage"
    --settings "$runsettings"
    --results-directory "$results_dir"
  )

  if [[ "$NoRestore" == "true" || "$NoBuild" != "true" ]]; then
    test_args+=(--no-restore)
  fi

  dotnet "${test_args[@]}"
done

coverage_file="$(find "$results_dir" -type f -name 'coverage.cobertura.xml' -print -quit)"
if [[ -z "$coverage_file" ]]; then
  echo "ERROR: no coverage.cobertura.xml file was produced" >&2
  exit 1
fi

echo "Coverage XML files:"
find "$results_dir" -type f -name 'coverage.cobertura.xml' -print

if [[ "$SkipHtml" == "true" ]]; then
  echo "Skipped HTML report generation."
  exit 0
fi

if ! command -v reportgenerator >/dev/null 2>&1; then
  echo "HTML report skipped: reportgenerator was not found on PATH."
  echo "Install it with: dotnet tool install -g dotnet-reportgenerator-globaltool"
  echo "Or rerun with -SkipHtml to intentionally produce XML only."
  exit 0
fi

mkdir -p "$report_dir"
reportgenerator \
  "-reports:$results_dir/**/coverage.cobertura.xml" \
  "-targetdir:$report_dir" \
  '-filefilters:-*/obj/*;-*.g.cs' \
  -reporttypes:Html

echo "HTML coverage report: $report_dir/index.html"
