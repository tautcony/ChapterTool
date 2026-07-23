#!/usr/bin/env bash
# Generate the application icons from the SVG source.
# Requires ImageMagick (`magick`) and macOS `iconutil`.
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
svg="$repo_root/src/ChapterTool.Avalonia/Assets/Icons/app-icon.svg"
icns="$repo_root/src/ChapterTool.Avalonia/Assets/Icons/app-icon.icns"
ico="$repo_root/src/ChapterTool.Avalonia/Assets/Icons/app-icon.ico"

if ! command -v magick >/dev/null 2>&1; then
  echo "ERROR: ImageMagick 'magick' is required to generate application icons." >&2
  exit 1
fi

if ! command -v iconutil >/dev/null 2>&1; then
  echo "ERROR: macOS 'iconutil' is required to generate the ICNS asset." >&2
  exit 1
fi

if [[ ! -f "$svg" ]]; then
  echo "ERROR: application icon source was not found: $svg" >&2
  exit 1
fi

temp_root="$(mktemp -d "${TMPDIR:-/tmp}/chaptertool-icon-build.XXXXXX")"
iconset_dir="$temp_root/app.iconset"
mkdir "$iconset_dir"
trap 'rm -rf "$temp_root"' EXIT INT TERM

render_icon() {
  local size="$1"
  local output="$2"
  magick -background none "$svg" -resize "${size}x${size}!" -strip -depth 8 "$output"
}

render_icon 16 "$iconset_dir/icon_16x16.png"
render_icon 32 "$iconset_dir/icon_16x16@2x.png"
render_icon 32 "$iconset_dir/icon_32x32.png"
render_icon 64 "$iconset_dir/icon_32x32@2x.png"
render_icon 128 "$iconset_dir/icon_128x128.png"
render_icon 256 "$iconset_dir/icon_128x128@2x.png"
render_icon 256 "$iconset_dir/icon_256x256.png"
render_icon 512 "$iconset_dir/icon_256x256@2x.png"
render_icon 512 "$iconset_dir/icon_512x512.png"
render_icon 1024 "$iconset_dir/icon_512x512@2x.png"

iconutil --convert icns --output "$icns" "$iconset_dir"
magick -background none "$svg" \
  -define icon:auto-resize=256,128,64,48,32,16 \
  "$ico"

echo "Generated: $icns"
echo "Generated: $ico"
