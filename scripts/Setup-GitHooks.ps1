Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Get-Location).Path
$hooksPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($repoRoot, ".githooks"))

if (-not (Test-Path -LiteralPath $hooksPath)) {
    throw "Expected hooks directory not found: $hooksPath"
}

git config core.hooksPath ".githooks"
Write-Host "Configured git hooks path to .githooks for this repository."
