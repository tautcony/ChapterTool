param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [switch]$NoRestore,
    [switch]$PublishSingleFile
)

$ErrorActionPreference = "Stop"

if (-not $Runtime.StartsWith("win-", [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "publish.ps1 only supports Windows runtime identifiers. Use scripts/publish.sh for '$Runtime'."
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repoRoot "src/ChapterTool.Avalonia/ChapterTool.Avalonia.csproj"
$publishKind = if ($SelfContained) { "self-contained" } else { "framework-dependent" }
$output = Join-Path $repoRoot "artifacts/publish/$publishKind/$Runtime"

if (Test-Path $output) {
    Remove-Item -Recurse -Force $output
}
New-Item -ItemType Directory -Force -Path $output | Out-Null

if (-not $NoRestore) {
    dotnet restore $project --runtime $Runtime
}

$selfContainedValue = $SelfContained.IsPresent.ToString().ToLowerInvariant()
$publishSingleFileValue = $PublishSingleFile.IsPresent.ToString().ToLowerInvariant()
$publishArgs = @(
    "publish", $project,
    "--configuration", $Configuration,
    "--runtime", $Runtime,
    "--self-contained:$selfContainedValue",
    "--output", $output,
    "-p:PublishSingleFile=$publishSingleFileValue"
)

if ($PublishSingleFile) {
    $publishArgs += "-p:IncludeNativeLibrariesForSelfExtract=true"
}

if ($NoRestore) {
    $publishArgs += "--no-restore"
}

dotnet @publishArgs

Write-Host "Published ChapterTool Avalonia to $output"
