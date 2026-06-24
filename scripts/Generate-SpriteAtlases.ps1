param(
    [Parameter(Mandatory = $true)]
    [string]$InputFolder,
    [Parameter(Mandatory = $true)]
    [string]$OutputManifest,
    [int]$PageSize = 2048
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "tools/Cyberland.SpriteAtlasBaker/Cyberland.SpriteAtlasBaker.csproj"
dotnet run --project $project -- $InputFolder $OutputManifest $PageSize
