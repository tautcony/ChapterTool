param(
    [string]$RepoRoot = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
}
else {
    $RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path
}

$srcRoot = Join-Path $RepoRoot "src"
$definitionFiles = @(
    (Join-Path $srcRoot "ChapterTool.Avalonia.UI/Resources/Themes.axaml"),
    (Join-Path $srcRoot "ChapterTool.Avalonia.UI/Resources/SharedResources.axaml"),
    (Join-Path $srcRoot "ChapterTool.Avalonia.UI/Resources/Styles.axaml")
)
$themeServicePath = Join-Path $srcRoot "ChapterTool.Avalonia/Services/AvaloniaThemeApplicationService.cs"

$systemKeyPrefixes = @(
    "System",
    "AccentButton",
    "Theme",
    "ContentControl",
    "TextControl",
    "ComboBox",
    "CheckBox",
    "RadioButton",
    "Slider",
    "TabView",
    "ScrollBar",
    "DataGrid",
    "Flyout",
    "MenuFlyout",
    "ToolTip",
    "ButtonBackground",
    "ButtonForeground",
    "ButtonBorder",
    "ButtonPadding"
)
$knownFrameworkKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($key in @(
        "MenuInputGestureTextMargin",
        "OverlayCornerRadius",
        "TooltipDataValidationErrors")) {
    [void]$knownFrameworkKeys.Add($key)
}

function Get-FileText {
    param([string]$Path)
    return [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
}

function Add-Key {
    param(
        [System.Collections.Generic.HashSet[string]]$Set,
        [string]$Key
    )

    if (-not [string]::IsNullOrWhiteSpace($Key)) {
        [void]$Set.Add($Key.Trim())
    }
}

$defined = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$referenced = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$localKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$serviceKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)

$keyAttributePattern = [regex]'x:Key\s*=\s*"([^"]+)"'
$resourceReferencePattern = [regex]'\{(?:DynamicResource|StaticResource)\s+([^}\s,]+)'
$importedKeysPattern = [regex]'(?s)ImportedThemeColorKeys\s*\{\s*get;\s*\}\s*=\s*\[(?<body>.*?)\];'
$quotedPattern = [regex]'"([^"]+)"'
$constKeyPattern = [regex]'public const string \w+ = "([^"]+)";'

foreach ($definitionFile in $definitionFiles) {
    if (-not (Test-Path -LiteralPath $definitionFile)) {
        throw "Definition file not found: $definitionFile"
    }

    $text = Get-FileText -Path $definitionFile
    foreach ($match in $keyAttributePattern.Matches($text)) {
        $key = $match.Groups[1].Value
        if ($key -eq "Light" -or $key -eq "Dark") {
            continue
        }
        Add-Key -Set $defined -Key $key
    }

    foreach ($styleResources in [regex]::Matches($text, '(?s)<Style\.Resources>(.*?)</Style\.Resources>')) {
        foreach ($match in $keyAttributePattern.Matches($styleResources.Groups[1].Value)) {
            Add-Key -Set $referenced -Key $match.Groups[1].Value
        }
    }
}

if (-not (Test-Path -LiteralPath $themeServicePath)) {
    throw "Theme service file not found: $themeServicePath"
}

$serviceText = Get-FileText -Path $themeServicePath
$importedMatch = $importedKeysPattern.Match($serviceText)
if ($importedMatch.Success) {
    foreach ($match in $quotedPattern.Matches($importedMatch.Groups["body"].Value)) {
        Add-Key -Set $defined -Key $match.Groups[1].Value
        Add-Key -Set $serviceKeys -Key $match.Groups[1].Value
    }
}

foreach ($match in $constKeyPattern.Matches($serviceText)) {
    Add-Key -Set $defined -Key $match.Groups[1].Value
    Add-Key -Set $serviceKeys -Key $match.Groups[1].Value
}

$axamlFiles = Get-ChildItem -LiteralPath $srcRoot -Recurse -Filter "*.axaml" -File
foreach ($axamlFile in $axamlFiles) {
    $text = Get-FileText -Path $axamlFile.FullName
    foreach ($match in $keyAttributePattern.Matches($text)) {
        Add-Key -Set $localKeys -Key $match.Groups[1].Value
    }
    foreach ($match in $resourceReferencePattern.Matches($text)) {
        Add-Key -Set $referenced -Key $match.Groups[1].Value
    }
}

$serviceFullPath = [System.IO.Path]::GetFullPath($themeServicePath)
$csFiles = Get-ChildItem -LiteralPath $srcRoot -Recurse -Filter "*.cs" -File
foreach ($csFile in $csFiles) {
    if ([System.IO.Path]::GetFullPath($csFile.FullName) -eq $serviceFullPath) {
        continue
    }

    $text = Get-FileText -Path $csFile.FullName
    foreach ($match in $quotedPattern.Matches($text)) {
        $value = $match.Groups[1].Value
        if ($defined.Contains($value)) {
            Add-Key -Set $referenced -Key $value
        }
    }
}

function Test-SystemKey {
    param([string]$Key)
    foreach ($prefix in $systemKeyPrefixes) {
        if ($Key.StartsWith($prefix, [System.StringComparison]::Ordinal)) {
            return $true
        }
    }
    return $false
}

$unresolved = [System.Collections.Generic.List[string]]::new()
foreach ($key in ($referenced | Sort-Object)) {
    if ($defined.Contains($key) -or $localKeys.Contains($key) -or $knownFrameworkKeys.Contains($key) -or (Test-SystemKey -Key $key)) {
        continue
    }
    $unresolved.Add($key)
}

$unconsumed = [System.Collections.Generic.List[string]]::new()
foreach ($key in ($defined | Sort-Object)) {
    if (-not $referenced.Contains($key)) {
        $unconsumed.Add($key)
    }
}

Write-Output "UI resource audit"
Write-Output ("Repo: {0}" -f $RepoRoot)
Write-Output ("Defined keys: {0}" -f $defined.Count)
Write-Output ("Referenced keys: {0}" -f $referenced.Count)
Write-Output ("Service-written keys: {0}" -f $serviceKeys.Count)
Write-Output ""
Write-Output ("Unresolved references ({0}):" -f $unresolved.Count)
if ($unresolved.Count -eq 0) {
    Write-Output "  (none)"
}
else {
    foreach ($key in $unresolved) {
        Write-Output ("  {0}" -f $key)
    }
}

Write-Output ""
Write-Output ("Unconsumed definitions ({0}):" -f $unconsumed.Count)
if ($unconsumed.Count -eq 0) {
    Write-Output "  (none)"
}
else {
    foreach ($key in $unconsumed) {
        Write-Output ("  {0}" -f $key)
    }
}

exit 0
