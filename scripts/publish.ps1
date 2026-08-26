param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [switch]$NoRestore,
    [switch]$PublishSingleFile
)

$ErrorActionPreference = "Stop"

if ($Runtime -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') {
    throw "Invalid runtime identifier '$Runtime'."
}

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
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE."
    }
}

$selfContainedValue = $SelfContained.IsPresent.ToString().ToLowerInvariant()
$publishSingleFileValue = $PublishSingleFile.IsPresent.ToString().ToLowerInvariant()
$publishArgs = @(
    "publish", $project,
    "--configuration", $Configuration,
    "--runtime", $Runtime,
    "--self-contained:$selfContainedValue",
    "--output", $output,
    "--no-restore",
    "-p:PublishSingleFile=$publishSingleFileValue",
    "-p:DebugType=None",
    "-p:DebugSymbols=false",
    "-p:CopyOutputSymbolsToPublishDirectory=false",
    "-p:PublishDocumentationFiles=false"
)

if ($PublishSingleFile) {
    $publishArgs += "-p:IncludeNativeLibrariesForSelfExtract=true"
}

dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

# Some native runtime packages classify their PDB files as runtime assets.
Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object {
    $_.Extension -in @(".pdb", ".dbg")
} | Remove-Item -Force

$developmentFiles = Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object {
    $_.Extension -in @(".pdb", ".dbg") -or
    $_.Name -eq ".DS_Store" -or
    $_.Name -like "AvaloniaUI.DiagnosticsSupport*.dll"
}
if ($developmentFiles) {
    $paths = ($developmentFiles.FullName | ForEach-Object { "  $_" }) -join [Environment]::NewLine
    throw "Publish output contains development-only files:$([Environment]::NewLine)$paths"
}

if ($PublishSingleFile) {
    $duplicateAssemblies = Get-ChildItem -LiteralPath $output -File -Filter "*.dll"
    if ($duplicateAssemblies) {
        $paths = ($duplicateAssemblies.FullName | ForEach-Object { "  $_" }) -join [Environment]::NewLine
        throw "Single-file publish output contains duplicate top-level assemblies:$([Environment]::NewLine)$paths"
    }
}

Write-Host "Published ChapterTool Avalonia to $output"
