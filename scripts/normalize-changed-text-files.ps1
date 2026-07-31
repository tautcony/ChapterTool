param(
    [string[]]$Paths = @(),
    [switch]$IncludeUntracked,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repoRoot

try {
    function Get-RepoRelativePath {
        param(
            [string]$RootPath,
            [string]$FullPath
        )

        $rootUri = [System.Uri]((Resolve-Path -LiteralPath $RootPath).Path.TrimEnd('\') + '\')
        $fileUri = [System.Uri](Resolve-Path -LiteralPath $FullPath).Path
        return [System.Uri]::UnescapeDataString($rootUri.MakeRelativeUri($fileUri).ToString()).Replace('\', '/')
    }

    $allowedExtensions = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($extension in @(
        ".cs", ".md", ".axaml", ".json", ".xml", ".yml", ".yaml", ".props", ".targets", ".sln", ".slnx", ".sh", ".ps1", ".txt", ".csv", ".tsv")) {
        [void]$allowedExtensions.Add($extension)
    }

    $allowedFileNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($name in @(".editorconfig", ".gitattributes")) {
        [void]$allowedFileNames.Add($name)
    }

    $candidatePaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($inputPath in $Paths) {
        if ([string]::IsNullOrWhiteSpace($inputPath)) {
            continue
        }

        $normalizedInput = $inputPath.Trim().Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        $fullInputPath = Join-Path $repoRoot $normalizedInput
        if (-not (Test-Path -LiteralPath $fullInputPath)) {
            Write-Warning "Path not found: $inputPath"
            continue
        }

        $item = Get-Item -LiteralPath $fullInputPath
        if ($item.PSIsContainer) {
            foreach ($file in Get-ChildItem -LiteralPath $fullInputPath -Recurse -File) {
                $relative = Get-RepoRelativePath -RootPath $repoRoot -FullPath $file.FullName
                [void]$candidatePaths.Add($relative)
            }
        }
        else {
            $relative = Get-RepoRelativePath -RootPath $repoRoot -FullPath $item.FullName
            [void]$candidatePaths.Add($relative)
        }
    }

    foreach ($path in (git diff --name-only)) {
        if (-not [string]::IsNullOrWhiteSpace($path)) {
            [void]$candidatePaths.Add($path.Trim())
        }
    }

    foreach ($path in (git diff --cached --name-only)) {
        if (-not [string]::IsNullOrWhiteSpace($path)) {
            [void]$candidatePaths.Add($path.Trim())
        }
    }

    if ($IncludeUntracked) {
        foreach ($path in (git ls-files --others --exclude-standard)) {
            if (-not [string]::IsNullOrWhiteSpace($path)) {
                [void]$candidatePaths.Add($path.Trim())
            }
        }
    }

    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    $normalized = [System.Collections.Generic.List[string]]::new()
    $skipped = [System.Collections.Generic.List[string]]::new()

    foreach ($relativePath in $candidatePaths | Sort-Object) {
        $extension = [System.IO.Path]::GetExtension($relativePath)
        $fileName = [System.IO.Path]::GetFileName($relativePath)
        if (-not $allowedExtensions.Contains($extension) -and -not $allowedFileNames.Contains($fileName)) {
            $skipped.Add($relativePath)
            continue
        }

        $fullPath = Join-Path $repoRoot $relativePath
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            continue
        }

        $originalText = [System.IO.File]::ReadAllText($fullPath, [System.Text.Encoding]::UTF8)
        $normalizedText = $originalText.Replace("`r`n", "`n").Replace("`r", "`n")

        $originalBytes = [System.IO.File]::ReadAllBytes($fullPath)
        $hadBom = $originalBytes.Length -ge 3 -and $originalBytes[0] -eq 0xEF -and $originalBytes[1] -eq 0xBB -and $originalBytes[2] -eq 0xBF
        $hadCr = $originalText.Contains("`r")

        if (-not $hadBom -and -not $hadCr) {
            continue
        }

        if ($WhatIf) {
            $normalized.Add($relativePath)
            continue
        }

        [System.IO.File]::WriteAllText($fullPath, $normalizedText, $utf8NoBom)
        $normalized.Add($relativePath)
    }

    if ($normalized.Count -eq 0) {
        Write-Host "No changed text files required normalization."
    }
    else {
        Write-Host "Normalized files:"
        $normalized | ForEach-Object { Write-Host "  $_" }
    }

    if ($skipped.Count -gt 0) {
        Write-Host "Skipped non-text paths:"
        $skipped | ForEach-Object { Write-Host "  $_" }
    }
}
finally {
    Pop-Location
}