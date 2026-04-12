# Publishes Cyberland.Host to artifacts/publish. Mods/ is staged by MSBuild (StageModsForHost.ps1) into the publish output.
# Portable (no -RuntimeIdentifier): framework-dependent output under artifacts/publish/Cyberland.Host/<config>/.
# With -RuntimeIdentifier: framework-dependent RID-specific output under artifacts/publish/Cyberland.Host/<config>_<rid>/ by default
# (uses the machine's shared .NET runtime — small ship; add -SelfContained to bundle the runtime for offline/air-gapped installs).
# If execution policy blocks unsigned scripts, use .\scripts\Publish-Cyberland.cmd (see README).
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$RuntimeIdentifier,
    [switch]$SelfContained,
    [switch]$Archive
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot

$hostProj = Join-Path $repoRoot 'src/Cyberland.Host/Cyberland.Host.csproj'
$cfgLower = $Configuration.ToLowerInvariant()

$publishArgs = @('publish', $hostProj, '-c', $Configuration)
if (-not [string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
    $publishArgs += @('-r', $RuntimeIdentifier.Trim())
    if ($SelfContained) {
        $publishArgs += @('--self-contained', 'true')
    } else {
        $publishArgs += @('--self-contained', 'false')
    }
}

dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$publishFolderName = $cfgLower
if (-not [string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
    $publishFolderName = $cfgLower + '_' + $RuntimeIdentifier.Trim().ToLowerInvariant()
}
$publishRoot = Join-Path $repoRoot "artifacts/publish/Cyberland.Host/$publishFolderName"
$modsDest = Join-Path $publishRoot 'Mods'

if (-not (Test-Path -LiteralPath $modsDest)) {
    throw "Expected staged mods folder missing after publish: $modsDest"
}

if ($Configuration -eq 'Release') {
    $pdbs = @(Get-ChildItem -LiteralPath $publishRoot -Recurse -Filter '*.pdb' -File -ErrorAction Stop)
    if ($pdbs.Count -gt 0) {
        $names = ($pdbs | ForEach-Object { $_.FullName }) -join '; '
        throw "Release publish must not contain PDB files. Found: $names"
    }
}

Write-Host "Publish complete: $publishRoot (with Mods/)"

if ($Archive) {
    $archiveScript = Join-Path $PSScriptRoot 'Archive-CyberlandPublish.ps1'
    if ([string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
        & $archiveScript -Configuration $Configuration
    } else {
        & $archiveScript -Configuration $Configuration -RuntimeIdentifier $RuntimeIdentifier.Trim()
    }
}
