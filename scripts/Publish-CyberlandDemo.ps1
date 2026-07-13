# Enables one demo mod manifest, runs Publish-Cyberland.ps1, then restores "disabled": true.
# Use from VS Code / Cursor: Tasks → Cyberland: Publish Release with demo (Idle Gold), etc.
# If publish fails mid-flight, the finally block restores the manifest; if you terminate the shell abruptly, re-disable manually.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("hdr", "snake", "pong", "brick", "mousechase", "rts", "idlegold", "spritegallery", "whackamole", "audio")]
    [string] $Demo,
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',
    [string] $RuntimeIdentifier,
    [switch] $SelfContained,
    [switch] $Archive
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

$relManifest = switch ($Demo) {
    "hdr" { "mods\Cyberland.Demo\manifest.json" }
    "snake" { "mods\Cyberland.Demo.Snake\manifest.json" }
    "pong" { "mods\Cyberland.Demo.Pong\manifest.json" }
    "brick" { "mods\Cyberland.Demo.BrickBreaker\manifest.json" }
    "mousechase" { "mods\Cyberland.Demo.MouseChase\manifest.json" }
    "rts" { "mods\Cyberland.Demo.Rts\manifest.json" }
    "idlegold" { "mods\Cyberland.Demo.IdleGold\manifest.json" }
    "spritegallery" { "mods\Cyberland.Demo.SpriteGallery\manifest.json" }
    "whackamole" { "mods\Cyberland.Demo.WhackAMole\manifest.json" }
    "audio" { "mods\Cyberland.Demo.Audio\manifest.json" }
}

$manifestPath = Join-Path $repoRoot $relManifest
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Manifest not found: $manifestPath"
}

function Set-ManifestDisabled {
    param(
        [Parameter(Mandatory = $true)]
        [string] $LiteralPath,
        [Parameter(Mandatory = $true)]
        [bool] $Disabled
    )
    $raw = Get-Content -LiteralPath $LiteralPath -Raw
    $value = if ($Disabled) { "true" } else { "false" }
    $replacement = '"disabled": ' + $value
    $new = [regex]::Replace(
        $raw,
        '"disabled"\s*:\s*(true|false)',
        $replacement,
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
    )
    if ($new -eq $raw) {
        throw "Could not set disabled=$value in $LiteralPath (no matching `"disabled`" key?)."
    }
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($LiteralPath, $new, $utf8NoBom)
}

$publishScript = Join-Path $PSScriptRoot "Publish-Cyberland.ps1"

# Child script uses `exit` on dotnet failure; run it in a subprocess so this script's `finally` always restores the manifest.
$publishArgs = @(
    '-NoProfile',
    '-ExecutionPolicy', 'Bypass',
    '-File', $publishScript,
    '-Configuration', $Configuration
)
if (-not [string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
    $publishArgs += @('-RuntimeIdentifier', $RuntimeIdentifier.Trim())
}
if ($SelfContained) {
    $publishArgs += '-SelfContained'
}
if ($Archive) {
    $publishArgs += '-Archive'
}

try {
    Write-Host "Enabling demo for publish (manifest: $relManifest)..."
    Set-ManifestDisabled -LiteralPath $manifestPath -Disabled $false

    Write-Host "Publishing Cyberland.Host..."
    $proc = Start-Process -FilePath "powershell.exe" -ArgumentList $publishArgs -WorkingDirectory $repoRoot -Wait -PassThru -NoNewWindow
    if ($proc.ExitCode -ne 0) {
        exit $proc.ExitCode
    }
}
finally {
    try {
        Write-Host "Disabling demo in manifest (restoring default publish/skip)..."
        Set-ManifestDisabled -LiteralPath $manifestPath -Disabled $true
    } catch {
        Write-Warning "Could not restore manifest disabled state: $_"
    }
}
