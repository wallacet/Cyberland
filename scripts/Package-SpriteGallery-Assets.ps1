# Builds the Sprite Gallery release zip for GitHub Releases (see mods/Cyberland.Demo.SpriteGallery/content.release.manifest.json).
# Extract layout mirrors Content/ under the mod folder so Sync-CyberlandAssets.ps1 can unpack into targetContentPath.
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$contentRoot = Join-Path $repoRoot "mods\Cyberland.Demo.SpriteGallery\Content"
$staging = Join-Path $repoRoot "artifacts\spritegallery-asset-staging"
$outDir = Join-Path $repoRoot "artifacts\dist"
$zipName = "cyberland.demo.spritegallery.content.v0.1.0.zip"
$zipPath = Join-Path $outDir $zipName

$relativePaths = @(
    "Textures\Atlases\gallery.page0.png",
    "Textures\Atlases\ui_panel.page0.png",
    "Textures\Gallery\icon_static.png",
    "Textures\Source\Gallery\frame_a.png",
    "Textures\Source\Gallery\frame_b.png",
    "Textures\Source\Gallery\icon_static.png",
    "Textures\Source\Gallery\walk_strip.png",
    "Textures\Source\UiPanel\panel_bg.png",
    "Locale\de\Textures\Atlases\gallery.page0.png",
    "Locale\de\Textures\Gallery\icon_static.png"
)

if (Test-Path -LiteralPath $staging) {
    Remove-Item -LiteralPath $staging -Recurse -Force
}
New-Item -ItemType Directory -Path $staging -Force | Out-Null
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$missing = @()
foreach ($rel in $relativePaths) {
    $src = Join-Path $contentRoot $rel
    if (-not (Test-Path -LiteralPath $src)) {
        $missing += $rel
        continue
    }
    $dest = Join-Path $staging $rel
    $destDir = Split-Path -Parent $dest
    if (-not (Test-Path -LiteralPath $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }
    Copy-Item -LiteralPath $src -Destination $dest -Force
}

if ($missing.Count -gt 0) {
    throw "Missing PNG(s) under Content (bake or copy assets first):`n  - $($missing -join "`n  - ")"
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zipPath -CompressionLevel Optimal

$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash.ToLowerInvariant()
Write-Host "Wrote $zipPath"
Write-Host "sha256=$hash"
Write-Host "Update mods/Cyberland.Demo.SpriteGallery/content.release.manifest.json with this sha256, then publish the zip to the release tag."
