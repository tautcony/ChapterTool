#!/usr/bin/env bash
# Publish ChapterTool.Avalonia. Bash parity with publish.ps1.
# Tested on macOS (bash 3.2) and Linux (bash 4+/5).
# Usage:
#   ./scripts/publish.sh                            # win-x64, framework-dependent
#   ./scripts/publish.sh -Runtime osx-arm64
#   ./scripts/publish.sh -Runtime linux-x64 -SelfContained
set -euo pipefail

# ---- defaults ----
Configuration="Release"
Runtime="win-x64"
SelfContained="false"

# ---- arg parse ----
while [[ $# -gt 0 ]]; do
  case "$1" in
    -Configuration)
      Configuration="$2"; shift 2 ;;
    -Configuration=*)
      Configuration="${1#*=}"; shift ;;
    -Runtime)
      Runtime="$2"; shift 2 ;;
    -Runtime=*)
      Runtime="${1#*=}"; shift ;;
    -SelfContained)
      SelfContained="true"; shift ;;
    -SelfContained=*)
      SelfContained="${1#*=}"; shift ;;
    -h|--help)
      sed -n '2,8p' "$0"; exit 0 ;;
    *)
      echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

# ---- paths ----
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"

if [[ "$SelfContained" == "true" ]]; then
  publish_kind="self-contained"
else
  publish_kind="framework-dependent"
fi
output="$repo_root/artifacts/publish/$publish_kind/$Runtime"

# ---- publish ----
dotnet restore "$repo_root/ChapterTool.Avalonia.slnx"
dotnet publish "$repo_root/src/ChapterTool.Avalonia/ChapterTool.Avalonia.csproj" \
    --configuration "$Configuration" \
    --runtime "$Runtime" \
    --self-contained:"$SelfContained" \
    --output "$output" \
    -p:PublishSingleFile=false

echo "Published ChapterTool Avalonia to $output"

# ---- macOS .app bundle ----
# Build a proper .app bundle on macOS so the Dock/Finder show the real icon.
# Runs only for osx RIDs and only when invoked on a macOS host.
case "$Runtime" in
  osx-*)
    if [[ "$(uname -s)" != "Darwin" ]]; then
      echo "warning: Runtime is $Runtime but host is not macOS; skipped .app bundling. Run on macOS to produce the .app bundle." >&2
      exit 0
    fi

    app_name="ChapterTool.Avalonia"
    app_bundle="$output/$app_name.app"
    contents_dir="$app_bundle/Contents"
    macos_dir="$contents_dir/MacOS"
    resources_dir="$contents_dir/Resources"

    rm -rf "$app_bundle"
    mkdir -p "$macos_dir" "$resources_dir"

    # Move everything dotnet emitted into Contents/MacOS/.
    for item in "$output"/*; do
      name="$(basename "$item")"
      [[ "$name" == "$app_name.app" ]] && continue
      mv "$item" "$macos_dir/"
    done

    # Stage bundle metadata.
    info_plist_src="$repo_root/src/ChapterTool.Avalonia/Assets/MacOS/Info.plist"
    icns_src="$repo_root/src/ChapterTool.Avalonia/Assets/Icons/app-icon.icns"
    cp "$info_plist_src" "$contents_dir/Info.plist"
    cp "$icns_src" "$resources_dir/app-icon.icns"

    # PkgInfo: 8 bytes, "APPL????" — required by LaunchServices.
    printf 'APPL????' > "$contents_dir/PkgInfo"

    # Mark the executable so the bundle is double-clickable.
    exe_path="$macos_dir/$app_name"
    [[ -f "$exe_path" ]] && chmod +x "$exe_path"

    # Refresh icon cache so Dock picks up the new art immediately during dev.
    touch "$app_bundle"

    echo "Created macOS app bundle at $app_bundle"
    ;;
esac
