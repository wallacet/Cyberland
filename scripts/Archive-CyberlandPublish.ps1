# Zips a completed dotnet publish output (exe, deps, Mods/, etc.) for upload to a store or CDN.
# Expects artifacts/publish/Cyberland.Host/<config>/ (portable) or <config>_<rid>/ (RID publish) from Publish-Cyberland.ps1.
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$RuntimeIdentifier
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$cfgLower = $Configuration.ToLowerInvariant()
$publishFolderName = $cfgLower
if (-not [string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
    $publishFolderName = $cfgLower + '_' + $RuntimeIdentifier.Trim().ToLowerInvariant()
}
$publishRoot = Join-Path $repoRoot "artifacts/publish/Cyberland.Host/$publishFolderName"

if (-not (Test-Path -LiteralPath $publishRoot)) {
    throw "Publish output not found: $publishRoot. Run Publish-Cyberland.ps1 or dotnet publish first."
}

$exeWin = Join-Path $publishRoot 'Cyberland.Host.exe'
$exeUnix = Join-Path $publishRoot 'Cyberland.Host'
$hasWin = Test-Path -LiteralPath $exeWin
$hasUnix = (Test-Path -LiteralPath $exeUnix) -and ((Get-Item -LiteralPath $exeUnix) -is [System.IO.FileInfo])
if (-not ($hasWin -or $hasUnix)) {
    throw "Expected Cyberland.Host entrypoint missing under publish folder (incomplete publish?): $publishRoot"
}

$distRoot = Join-Path $repoRoot 'artifacts/dist'
New-Item -ItemType Directory -Path $distRoot -Force | Out-Null

if ([string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
    $zipName = "Cyberland-Host-$cfgLower.zip"
} else {
    $ridSafe = $RuntimeIdentifier.Trim().ToLowerInvariant().Replace([char]'/', [char]'-')
    $zipName = "Cyberland-Host-$cfgLower-$ridSafe.zip"
}
$zipPath = Join-Path $distRoot $zipName
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

# bsdtar zip: exclude *.pdb so distribution archives never contain symbols (defensive if an old tree still had PDBs).
# Layout matches dotnet publish: Cyberland.Host.exe and Mods/ at archive root.
& tar -caf $zipPath --exclude='*.pdb' -C $publishRoot .

Write-Host "Distribution archive ready: $zipPath"
Write-Host "Publish folder (unzipped layout): $publishRoot"
