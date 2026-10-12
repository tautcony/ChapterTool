$ErrorActionPreference = 'Stop'
$parseErrors = $null
$null = [System.Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot '../packaging/publish-desktop.ps1'),
    [ref]$null,
    [ref]$parseErrors)
if ($parseErrors.Count -gt 0) {
    throw ($parseErrors | Out-String)
}
