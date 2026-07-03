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
