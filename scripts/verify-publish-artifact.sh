#!/usr/bin/env bash
# Verifies the expected ChapterTool.Avalonia publish artifact layout.
set -euo pipefail

usage() {
  echo "Usage: $0 -Runtime <rid> -Path <publish-output>" >&2
}

Runtime=""
ArtifactPath=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    -Runtime)
      Runtime="${2-}"; shift 2 ;;
    -Runtime=*)
      Runtime="${1#*=}"; shift ;;
    -Path)
      ArtifactPath="${2-}"; shift 2 ;;
    -Path=*)
      ArtifactPath="${1#*=}"; shift ;;
    -h|--help)
      usage; exit 0 ;;
    *)
      echo "Unknown argument: $1" >&2; usage; exit 2 ;;
  esac
done

if [[ -z "$Runtime" || -z "$ArtifactPath" ]]; then
  usage
  exit 2
fi

if [[ ! -d "$ArtifactPath" ]]; then
  echo "ERROR: artifact path '$ArtifactPath' does not exist or is not a directory" >&2
  exit 1
fi

require_file() {
  local path="$1"
  if [[ ! -f "$path" ]]; then
    echo "ERROR: expected file '$path' was not found" >&2
    exit 1
  fi
}

require_executable() {
  local path="$1"
  require_file "$path"
  if [[ "$Runtime" != win-* && ! -x "$path" ]]; then
    echo "ERROR: expected executable '$path' is not executable" >&2
    exit 1
  fi
}

require_runtimeconfig_for_file_layout() {
  local path="$1"
  local app_dll="$path/ChapterTool.Avalonia.dll"
  local runtimeconfig="$path/ChapterTool.Avalonia.runtimeconfig.json"

  # Single-file publish embeds the runtimeconfig into the apphost bundle.
  # Multi-file framework-dependent publish keeps the app dll and runtimeconfig side by side.
  if [[ -f "$app_dll" ]]; then
    require_file "$runtimeconfig"
  fi
}

case "$Runtime" in
  win-*)
    require_file "$ArtifactPath/ChapterTool.Avalonia.exe"
    require_runtimeconfig_for_file_layout "$ArtifactPath"
    ;;
  linux-*)
    require_executable "$ArtifactPath/ChapterTool.Avalonia"
    require_runtimeconfig_for_file_layout "$ArtifactPath"
    ;;
  osx-*)
    app_dir="$ArtifactPath/ChapterTool.app"
    require_executable "$app_dir/Contents/MacOS/ChapterTool.Avalonia"
    require_file "$app_dir/Contents/Info.plist"
    require_file "$app_dir/Contents/Resources/app-icon.icns"
    require_file "$ArtifactPath/ChapterTool-$Runtime.dmg"
    ;;
  *)
    echo "ERROR: unsupported runtime '$Runtime'" >&2
    exit 2
    ;;
esac

echo "Verified ChapterTool artifact for $Runtime at $ArtifactPath"
