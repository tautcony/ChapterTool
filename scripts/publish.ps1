param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$publishKind = if ($SelfContained) { "self-contained" } else { "framework-dependent" }
$output = Join-Path $repoRoot "artifacts/publish/$publishKind/$Runtime"

dotnet restore (Join-Path $repoRoot "ChapterTool.Avalonia.slnx")
dotnet publish (Join-Path $repoRoot "src/ChapterTool.Avalonia/ChapterTool.Avalonia.csproj") `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained:$SelfContained `
    --output $output `
    -p:PublishSingleFile=false

Write-Host "Published ChapterTool Avalonia to $output"

# Build a proper .app bundle on macOS so the Dock/Finder show the real icon.
# Runs only for osx RIDs and only when invoked on a macOS host.
if ($Runtime -like "osx-*" -and [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX)) {
    $appName = "ChapterTool.Avalonia"
    $appBundle = Join-Path $output "$appName.app"
    $contentsDir = Join-Path $appBundle "Contents"
    $macosDir = Join-Path $contentsDir "MacOS"
    $resourcesDir = Join-Path $contentsDir "Resources"

    if (Test-Path $appBundle) { Remove-Item -Recurse -Force $appBundle }
    New-Item -ItemType Directory -Force -Path $macosDir, $resourcesDir | Out-Null

    # Move everything dotnet emitted into Contents/MacOS/.
    Get-ChildItem -Path $output -Exclude "$appName.app" |
        Where-Object { $_.Name -ne "$appName.app" } |
        Move-Item -Destination $macosDir -Force

    # Stage bundle metadata.
    $infoPlistSrc = Join-Path $repoRoot "src/ChapterTool.Avalonia/Assets/MacOS/Info.plist"
    $icnsSrc = Join-Path $repoRoot "src/ChapterTool.Avalonia/Assets/Icons/app-icon.icns"
    Copy-Item $infoPlistSrc (Join-Path $contentsDir "Info.plist") -Force
    Copy-Item $icnsSrc (Join-Path $resourcesDir "app-icon.icns") -Force

    # PkgInfo: 8 bytes, "APPL????" — required by LaunchServices.
    [System.IO.File]::WriteAllBytes((Join-Path $contentsDir "PkgInfo"), [System.Text.Encoding]::ASCII.GetBytes("APPL????"))

    # Mark the executable +applet so the bundle is double-clickable.
    $exePath = Join-Path $macosDir $appName
    if (Test-Path $exePath) { chmod +x $exePath }

    # Refresh icon cache so Dock picks up the new art immediately during dev.
    touch $appBundle
    if (Get-Command rebuild_icon_cache -ErrorAction SilentlyContinue) {
        rebuild_icon_cache 2>$null
    }

    Write-Host "Created macOS app bundle at $appBundle"
}
elseif ($Runtime -like "osx-*") {
    Write-Warning "Runtime is $Runtime but host is not macOS; skipped .app bundling. Run on macOS to produce the .app bundle."
}
