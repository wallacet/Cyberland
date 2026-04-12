# Zips a completed dotnet publish output (exe, deps, Mods/, etc.) for upload to a store or CDN.
# Expects artifacts/publish/Cyberland.Host/<config>/ from Publish-Cyberland.ps1 or dotnet publish.
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$cfgLower = $Configuration.ToLowerInvariant()
$publishRoot = Join-Path $repoRoot "artifacts/publish/Cyberland.Host/$cfgLower"

if (-not (Test-Path -LiteralPath $publishRoot)) {
    throw "Publish output not found: $publishRoot. Run Publish-Cyberland.ps1 or dotnet publish first."
}

$exe = Join-Path $publishRoot 'Cyberland.Host.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Expected Cyberland.Host.exe missing under publish folder (incomplete publish?): $publishRoot"
}

$distRoot = Join-Path $repoRoot 'artifacts/dist'
New-Item -ItemType Directory -Path $distRoot -Force | Out-Null

$zipName = "Cyberland-Host-$cfgLower.zip"
$zipPath = Join-Path $distRoot $zipName
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

# Zip contents of publish folder so extract yields Cyberland.Host.exe and Mods/ at archive root.
Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Distribution archive ready: $zipPath"
Write-Host "Publish folder (unzipped layout): $publishRoot"
