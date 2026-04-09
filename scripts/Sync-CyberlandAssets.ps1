param(
    [string]$ManifestName = "content.release.manifest.json",
    [string]$WorkspaceRoot = (Get-Location).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-Sha256([string]$Path) {
    return (Get-FileHash -Algorithm SHA256 -Path $Path).Hash.ToLowerInvariant()
}

function Resolve-PathFromRoot([string]$Root, [string]$Relative) {
    return [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($Root, $Relative))
}

$downloadCache = Resolve-PathFromRoot -Root $WorkspaceRoot -Relative "artifacts/assets-cache"
$modsRoot = Resolve-PathFromRoot -Root $WorkspaceRoot -Relative "mods"
$modsRootPrefix = $modsRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
New-Item -ItemType Directory -Path $downloadCache -Force | Out-Null

$manifestFiles = Get-ChildItem -Path $modsRoot -Filter $ManifestName -File -Recurse | Sort-Object FullName
if (-not $manifestFiles -or $manifestFiles.Count -eq 0) {
    throw "No per-mod asset manifests named '$ManifestName' found under '$modsRoot'."
}

Write-Host "Syncing assets from per-mod manifests..."

foreach ($manifestFile in $manifestFiles) {
    $manifest = Get-Content -LiteralPath $manifestFile.FullName -Raw | ConvertFrom-Json
    if ($manifest.schemaVersion -ne 1) {
        throw "Unsupported schemaVersion '$($manifest.schemaVersion)' in '$($manifestFile.FullName)'."
    }

    if ([string]::IsNullOrWhiteSpace($manifest.modId) -or
        [string]::IsNullOrWhiteSpace($manifest.assetArchiveUrl) -or
        [string]::IsNullOrWhiteSpace($manifest.sha256) -or
        [string]::IsNullOrWhiteSpace($manifest.targetContentPath)) {
        throw "Invalid manifest '$($manifestFile.FullName)'. Required: modId, assetArchiveUrl, sha256, targetContentPath."
    }

    if ($manifest.sha256 -match "REPLACE_WITH_REAL_SHA256") {
        throw "Manifest '$($manifestFile.FullName)' still contains placeholder sha256."
    }

    $archiveName = [System.IO.Path]::GetFileName([System.Uri]$manifest.assetArchiveUrl)
    $archivePath = Join-Path $downloadCache $archiveName

    if (-not (Test-Path -LiteralPath $archivePath)) {
        Write-Host "Downloading $($manifest.modId) assets..."
        Invoke-WebRequest -Uri $manifest.assetArchiveUrl -OutFile $archivePath
    } else {
        Write-Host "Using cached archive for $($manifest.modId): $archiveName"
    }

    $actualHash = Get-Sha256 -Path $archivePath
    $expectedHash = $manifest.sha256.ToLowerInvariant()
    if ($actualHash -ne $expectedHash) {
        Remove-Item -LiteralPath $archivePath -Force -ErrorAction SilentlyContinue
        throw "SHA256 mismatch for $archiveName (`$expected=$expectedHash, `$actual=$actualHash). Re-download blocked."
    }

    $targetPath = Resolve-PathFromRoot -Root $WorkspaceRoot -Relative $manifest.targetContentPath
    if (-not $targetPath.StartsWith($modsRootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to extract outside mods folder: $targetPath"
    }

    New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
    Write-Host "Extracting to $targetPath"
    Expand-Archive -LiteralPath $archivePath -DestinationPath $targetPath -Force
}

Write-Host "Asset sync complete."
