Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$src = Join-Path $PSScriptRoot "..\mods\Cyberland.Demo.SpriteGallery\Content\Textures\Gallery\icon_static.png"
$destDir = Join-Path $PSScriptRoot "..\mods\Cyberland.Demo.SpriteGallery\Content\Locale\de\Textures\Gallery"
New-Item -ItemType Directory -Path $destDir -Force | Out-Null
$dest = Join-Path $destDir "icon_static.png"

$bmp = [System.Drawing.Bitmap]::FromFile($src)
for ($y = 0; $y -lt $bmp.Height; $y++) {
    for ($x = 0; $x -lt $bmp.Width; $x++) {
        $c = $bmp.GetPixel($x, $y)
        if ($c.A -gt 8) {
            $r = [byte][Math]::Min(255, [int]($c.R * 0.25 + 40))
            $g = [byte][Math]::Min(255, [int]($c.G * 0.55 + 160))
            $b = [byte][Math]::Min(255, [int]($c.B * 0.25 + 50))
            $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($c.A, $r, $g, $b))
        }
    }
}
$bmp.Save($dest, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Wrote $dest"
