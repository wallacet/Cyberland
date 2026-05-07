[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string] $OutputDir = "src/Cyberland.Engine/Rendering/Text/Baked"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

$out = if ([System.IO.Path]::IsPathRooted($OutputDir)) { $OutputDir } else { Join-Path $repoRoot $OutputDir }
if (-not (Test-Path -LiteralPath $out)) {
    New-Item -ItemType Directory -Path $out | Out-Null
}

dotnet run --project (Join-Path $repoRoot "tools/Cyberland.MsdfAtlasBaker/Cyberland.MsdfAtlasBaker.csproj") -c Release -- "$out"
