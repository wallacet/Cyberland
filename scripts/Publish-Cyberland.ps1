# Publishes Cyberland.Host to artifacts/publish and copies staged Mods/ beside the published exe.
# Mirrors .cursor/skills/publish-cyberland/SKILL.md (framework-dependent Release by default).
# If execution policy blocks unsigned scripts, use .\scripts\Publish-Cyberland.cmd (see README).
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
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

$modsSrc = Join-Path $repoRoot "artifacts/bin/Cyberland.Host/$cfgLower/Mods"
$publishRoot = Join-Path $repoRoot "artifacts/publish/Cyberland.Host/$cfgLower"

if (-not (Test-Path -LiteralPath $modsSrc)) {
    throw "Staged mods folder not found: $modsSrc (build/publish host first)."
}

New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
# Same as skill: place Mods/ next to the published exe under artifacts/publish/...
Copy-Item -Recurse -Force $modsSrc $publishRoot
Write-Host "Publish complete: $publishRoot (with Mods/)"
