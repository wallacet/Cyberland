# Publishes Cyberland.Host to artifacts/publish. Mods/ is staged by MSBuild (StageModsForHost.ps1) into the publish output.
# Mirrors .cursor/skills/publish-cyberland/SKILL.md (framework-dependent Release by default).
# If execution policy blocks unsigned scripts, use .\scripts\Publish-Cyberland.cmd (see README).
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Archive
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repoRoot

$hostProj = Join-Path $repoRoot 'src/Cyberland.Host/Cyberland.Host.csproj'
$cfgLower = $Configuration.ToLowerInvariant()

dotnet publish $hostProj -c $Configuration
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$publishRoot = Join-Path $repoRoot "artifacts/publish/Cyberland.Host/$cfgLower"
$modsDest = Join-Path $publishRoot 'Mods'

if (-not (Test-Path -LiteralPath $modsDest)) {
    throw "Expected staged mods folder missing after publish: $modsDest"
}

Write-Host "Publish complete: $publishRoot (with Mods/)"

if ($Archive) {
    $archiveScript = Join-Path $PSScriptRoot 'Archive-CyberlandPublish.ps1'
    & $archiveScript -Configuration $Configuration
}
