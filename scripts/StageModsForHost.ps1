# Stages shipped mods from mods/*/ into HostOutDir/Mods/<folder>/ (manifest, built DLLs, Content).
# Skips mods whose manifest.json has "disabled": true (case-insensitive JSON).
# Content-only mods: omit or leave empty "entryAssembly" — copies manifest + Content only (no .csproj build).
# Code mods: require "entryAssembly" and a .csproj; builds and copies DLLs (except Cyberland.Engine.dll).
# Invoked from Cyberland.Host.csproj after Build and after Publish.
param(
    [string]$HostOutDir = $env:CYBERLAND_STAGE_HOST_OUT,
    [string]$RepoRoot = $env:CYBERLAND_REPO_ROOT,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($HostOutDir)) {
    throw "HostOutDir is required (pass -HostOutDir or set CYBERLAND_STAGE_HOST_OUT)."
}
if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    throw "RepoRoot is required (pass -RepoRoot or set CYBERLAND_REPO_ROOT)."
}

$HostOutDir = [System.IO.Path]::GetFullPath($HostOutDir.TrimEnd([char]'\', [char]'/'))
$RepoRoot = [System.IO.Path]::GetFullPath($RepoRoot.TrimEnd([char]'\', [char]'/'))

$modsSrc = Join-Path $RepoRoot 'mods'
if (-not (Test-Path -LiteralPath $modsSrc)) {
    Write-Host "No mods folder at $modsSrc - skipping mod staging."
    exit 0
}

# Wipe the output Mods folder each run so mods that became disabled in source do not leave stale folders
# (otherwise Mods/<Name>/manifest.json from a previous build could still load).
$modsDestRoot = Join-Path $HostOutDir 'Mods'
if (Test-Path -LiteralPath $modsDestRoot) {
    Remove-Item -LiteralPath $modsDestRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $modsDestRoot -Force | Out-Null

Get-ChildItem -LiteralPath $modsSrc -Directory | ForEach-Object {
    $modFolder = $_.FullName
    $manifestPath = Join-Path $modFolder 'manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        return
    }

    $json = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $disabledProp = $json.psobject.Properties['disabled']
    if ($null -ne $disabledProp -and $disabledProp.Value -eq $true) {
        Write-Host "Skipping disabled mod: $($_.Name)"
        return
    }

    $entryAsmProp = $json.psobject.Properties['entryAssembly']
    $entryAsmVal = if ($null -eq $entryAsmProp) { $null } else { $entryAsmProp.Value }
    $contentOnlyMod = [string]::IsNullOrWhiteSpace([string]$entryAsmVal)

    $dest = Join-Path $modsDestRoot $_.Name
    if (Test-Path -LiteralPath $dest) {
        Remove-Item -LiteralPath $dest -Recurse -Force
    }
    New-Item -ItemType Directory -Path $dest -Force | Out-Null

    Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $dest 'manifest.json') -Force
    $contentSrc = Join-Path $modFolder 'Content'
    if (Test-Path -LiteralPath $contentSrc) {
        Copy-Item -LiteralPath $contentSrc -Destination (Join-Path $dest 'Content') -Recurse -Force
    }

    if ($contentOnlyMod) {
        Write-Host "Staging content-only mod: $($_.Name)"
        return
    }

    $csproj = Get-ChildItem -LiteralPath $modFolder -Filter '*.csproj' -File | Select-Object -First 1
    if ($null -eq $csproj) {
        throw "Mod '$($_.Name)' has entryAssembly in manifest but no .csproj in: $modFolder"
    }

    Write-Host "Building and staging mod: $($_.Name)"
    dotnet build $csproj.FullName -c $Configuration --nologo -v:q
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    $targetPath = (dotnet msbuild $csproj.FullName -nologo -getProperty:TargetPath -p:Configuration=$Configuration).Trim()
    if (-not $targetPath) {
        throw "Could not resolve TargetPath for $($csproj.FullName)"
    }

    $targetDir = [System.IO.Path]::GetDirectoryName($targetPath)

    Get-ChildItem -LiteralPath $targetDir -Filter '*.dll' | ForEach-Object {
        if ($_.Name -ieq 'Cyberland.Engine.dll') {
            return
        }
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $dest $_.Name) -Force
    }
}

Write-Host ('Mod staging complete: ' + $modsDestRoot)
