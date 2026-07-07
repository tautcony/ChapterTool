#!/usr/bin/env bash
# Publish ChapterTool.Avalonia from bash.
# Tested on macOS (bash 3.2), Linux (bash 4+/5), and Git Bash on Windows.
# Usage:
#   ./scripts/publish.sh                                      # win-x64, framework-dependent
#   ./scripts/publish.sh -Runtime osx-arm64
#   ./scripts/publish.sh -Runtime linux-x64 -SelfContained
#   ./scripts/publish.sh -Runtime osx-arm64 -NoRestore -PublishSingleFile  # creates .app and .dmg on macOS
set -euo pipefail

# ---- defaults ----
Configuration="Release"
Runtime="win-x64"
SelfContained="false"
NoRestore="false"
PublishSingleFile="false"

usage() {
  sed -n '2,8p' "$0"
}

normalize_bool() {
  case "$1" in
    true|True|TRUE|1|yes|Yes|YES|on|On|ON) printf 'true' ;;
    false|False|FALSE|0|no|No|NO|off|Off|OFF) printf 'false' ;;
    *) echo "ERROR: expected boolean value but got '$1'" >&2; exit 2 ;;
  esac
}

require_value() {
  local option="$1"
  local value="${2-}"
  if [[ -z "$value" || "$value" == -* ]]; then
    echo "ERROR: $option requires a value" >&2
    exit 2
  fi
}

# ---- arg parse ----
while [[ $# -gt 0 ]]; do
  case "$1" in
    -Configuration)
      require_value "$1" "${2-}"; Configuration="$2"; shift 2 ;;
    -Configuration=*)
      Configuration="${1#*=}"; shift ;;
    -Runtime)
      require_value "$1" "${2-}"; Runtime="$2"; shift 2 ;;
    -Runtime=*)
      Runtime="${1#*=}"; shift ;;
    -SelfContained)
      SelfContained="true"; shift ;;
    -SelfContained=*)
      SelfContained="$(normalize_bool "${1#*=}")"; shift ;;
    -NoRestore)
      NoRestore="true"; shift ;;
    -NoRestore=*)
      NoRestore="$(normalize_bool "${1#*=}")"; shift ;;
    -PublishSingleFile)
      PublishSingleFile="true"; shift ;;
    -PublishSingleFile=*)
      PublishSingleFile="$(normalize_bool "${1#*=}")"; shift ;;
    -h|--help)
      usage; exit 0 ;;
    *)
      echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

# ---- paths ----
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
project="$repo_root/src/ChapterTool.Avalonia/ChapterTool.Avalonia.csproj"

if [[ "$SelfContained" == "true" ]]; then
  publish_kind="self-contained"
else
  publish_kind="framework-dependent"
fi
output="$repo_root/artifacts/publish/$publish_kind/$Runtime"
dotnet_output="$output"
should_bundle_macos="false"

case "$Runtime" in
  osx-*)
    if [[ "$(uname -s)" == "Darwin" ]]; then
      should_bundle_macos="true"
      dotnet_output="$output/.publish-raw"
    fi
    ;;
esac

# ---- publish ----
rm -rf "$output"
mkdir -p "$dotnet_output"

if [[ "$NoRestore" != "true" ]]; then
  dotnet restore "$project" --runtime "$Runtime"
fi

publish_args=(
  publish "$project"
  --configuration "$Configuration"
  --runtime "$Runtime"
  --self-contained:"$SelfContained"
  --output "$dotnet_output"
  -p:PublishSingleFile="$PublishSingleFile"
)

if [[ "$PublishSingleFile" == "true" ]]; then
  publish_args+=(-p:IncludeNativeLibrariesForSelfExtract=true)
fi

if [[ "$NoRestore" == "true" ]]; then
  publish_args+=(--no-restore)
fi

dotnet "${publish_args[@]}"

# ---- macOS .app bundle ----
# Build a proper .app bundle on macOS so the Dock/Finder show the real icon.
# Runs only for osx RIDs and only when invoked on a macOS host.
case "$Runtime" in
  osx-*)
    if [[ "$should_bundle_macos" != "true" ]]; then
      echo "warning: Runtime is $Runtime but host is not macOS; skipped .app bundling. Run on macOS to produce the .app bundle." >&2
      echo "Published ChapterTool Avalonia to $output"
      exit 0
    fi

    executable_name="ChapterTool.Avalonia"
    app_display_name="ChapterTool"
    app_bundle="$output/$app_display_name.app"
    app_bundle_tmp="$output/.$app_display_name.app.tmp"
    contents_dir="$app_bundle_tmp/Contents"
    macos_dir="$contents_dir/MacOS"
    resources_dir="$contents_dir/Resources"

    rm -rf "$app_bundle" "$app_bundle_tmp"
    mkdir -p "$macos_dir" "$resources_dir"

    trap 'rm -rf "$app_bundle_tmp"' EXIT INT TERM

    # Move everything dotnet emitted into Contents/MacOS/.
    shopt -s nullglob
    items=("$dotnet_output"/*)
    shopt -u nullglob
    if (( ${#items[@]} == 0 )); then
      echo "ERROR: no publish output found in '$dotnet_output'" >&2
      exit 1
    fi
    for item in "${items[@]}"; do
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
    exe_path="$macos_dir/$executable_name"
    if [[ ! -f "$exe_path" ]]; then
      echo "ERROR: expected executable '$exe_path' not found" >&2
      exit 1
    fi
    chmod +x "$exe_path"

    trap - INT TERM

    rm -rf "$dotnet_output"
    mv "$app_bundle_tmp" "$app_bundle"
    trap - EXIT INT TERM

    # Refresh icon cache so Dock picks up the new art immediately during dev.
    touch "$app_bundle"

    dmg_staging="$output/.dmg-staging"
    dmg_output="$output/$app_display_name-$Runtime.dmg"

    rm -rf "$dmg_staging" "$dmg_output"
    mkdir -p "$dmg_staging"
    cp -R "$app_bundle" "$dmg_staging/"
    ln -s /Applications "$dmg_staging/Applications"

    hdiutil create \
      -volname "$app_display_name" \
      -srcfolder "$dmg_staging" \
      -ov \
      -format UDZO \
      "$dmg_output" >/dev/null

    rm -rf "$dmg_staging"

    echo "Created macOS app bundle at $app_bundle"
    echo "Created macOS DMG at $dmg_output"
    ;;
  *)
    echo "Published ChapterTool Avalonia to $output"
    ;;
esac
