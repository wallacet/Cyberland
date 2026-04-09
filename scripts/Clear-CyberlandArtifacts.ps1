# Deletes repo-root artifacts/ safely (if present).
# Mirrors .cursor/skills/clear-cyberland-artifacts/SKILL.md.
# If execution policy blocks unsigned scripts, use .\scripts\Clear-CyberlandArtifacts.cmd (see README).

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

if (Test-Path -LiteralPath "artifacts") {
    Remove-Item -Recurse -Force "artifacts"
    Write-Host "Cleared artifacts/."
} else {
    Write-Host "artifacts/ not found; nothing to clear."
}
