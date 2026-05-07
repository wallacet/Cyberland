[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [double] $ProfileSeconds = 12.0,
    [Parameter(Mandatory = $false)]
    [double] $MaxStartupLoadCallbackMs = 3000.0,
    [Parameter(Mandatory = $false)]
    [double] $MaxStartupFirstPresentMs = 15000.0,
    [Parameter(Mandatory = $false)]
    [double] $MinFps = 300.0,
    [Parameter(Mandatory = $false)]
    [double] $MaxGlyphCacheMisses = 400.0,
    [Parameter(Mandatory = $false)]
    [double] $MinBakedGlyphImports = 100.0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

$profilePath = "artifacts/profiles/idlegold-smoke-profile.txt"
$perfPath = "artifacts/profiles/idlegold-smoke-perf.txt"

& (Join-Path $PSScriptRoot "Profile-CyberlandDemo.ps1") `
    -Demo idlegold `
    -ProfileSeconds $ProfileSeconds `
    -ProfileDump $profilePath `
    -PerfDump $perfPath

& (Join-Path $PSScriptRoot "Check-CyberlandProfileBudget.ps1") `
    -ProfileReportPath $profilePath `
    -PerfSummaryPath $perfPath `
    -MinFps $MinFps

if (-not (Test-Path -LiteralPath $perfPath)) {
    throw "Perf summary not found: $perfPath"
}

$map = @{}
foreach ($line in [System.IO.File]::ReadAllLines($perfPath)) {
    $parts = $line.Split("=", 2)
    if ($parts.Length -eq 2) {
        $map[$parts[0].Trim()] = $parts[1].Trim()
    }
}

if (-not $map.ContainsKey("startupLoadCallbackMs") -or -not $map.ContainsKey("startupFirstPresentMs") -or -not $map.ContainsKey("glyphCacheMisses") -or -not $map.ContainsKey("glyphBakedImports")) {
    throw "Perf summary missing startup metrics in $perfPath"
}

$loadMs = [double]$map["startupLoadCallbackMs"]
$presentMs = [double]$map["startupFirstPresentMs"]
$glyphMisses = [double]$map["glyphCacheMisses"]
$bakedImports = [double]$map["glyphBakedImports"]
if ($loadMs -gt $MaxStartupLoadCallbackMs) {
    throw "startupLoadCallbackMs=$loadMs exceeds MaxStartupLoadCallbackMs=$MaxStartupLoadCallbackMs"
}
if ($presentMs -gt $MaxStartupFirstPresentMs) {
    throw "startupFirstPresentMs=$presentMs exceeds MaxStartupFirstPresentMs=$MaxStartupFirstPresentMs"
}
if ($glyphMisses -gt $MaxGlyphCacheMisses) {
    throw "glyphCacheMisses=$glyphMisses exceeds MaxGlyphCacheMisses=$MaxGlyphCacheMisses"
}
if ($bakedImports -lt $MinBakedGlyphImports) {
    throw "glyphBakedImports=$bakedImports is below MinBakedGlyphImports=$MinBakedGlyphImports"
}

Write-Host "IdleGold perf smoke passed. startupLoadCallbackMs=$loadMs startupFirstPresentMs=$presentMs glyphCacheMisses=$glyphMisses glyphBakedImports=$bakedImports"
