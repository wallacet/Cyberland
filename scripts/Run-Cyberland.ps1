# Build and run the game host from the repository root (same as VS Code "run" task).
# Usage:  .\scripts\Run-Cyberland.ps1
#         .\scripts\Run-Cyberland.ps1 -Watch
# If execution policy blocks unsigned scripts, use .\scripts\Run-Cyberland.cmd (see README).

param(
    [switch] $Watch
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path (Join-Path $root "Cyberland.sln"))) {
    throw "Run this script from the Cyberland repo (expected Cyberland.sln next to scripts/)."
}

Set-Location $root

if ($Watch) {
    dotnet watch run --project "src/Cyberland.Host/Cyberland.Host.csproj" -c Debug
} else {
    dotnet run --project "src/Cyberland.Host/Cyberland.Host.csproj" -c Debug
}
