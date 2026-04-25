# Enables one demo mod in its manifest, clears artifacts, runs the host until exit, then restores "disabled": true.
# Use from VS Code / Cursor: Tasks → Cyberland: Test demo (…).
# If you kill the task or close the terminal during the game, the finally block may not run — re-disable the mod in manifest.json manually if needed.
[CmdletBinding()]
param(
    # Use non-mandatory with explicit throw so missing -Demo does not open an interactive prompt in CI / tasks.
    [Parameter(Mandatory = $false, Position = 0)]
    [ValidateSet("hdr", "snake", "pong", "brick", "mousechase")]
    [string] $Demo
)

if ([string]::IsNullOrEmpty($Demo)) {
    throw "Required: -Demo (hdr | snake | pong | brick | mousechase). Example: .\Run-CyberlandDemo-Test.ps1 -Demo hdr"
}

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

try {
    Write-Host "Enabling demo (manifest: $relManifest)..."
    Set-ManifestDisabled -LiteralPath $manifestPath -Disabled $false

    Write-Host "Clearing artifacts/..."
    & (Join-Path $PSScriptRoot "Clear-CyberlandArtifacts.ps1")

    Write-Host "Running Cyberland.Host (close the game to continue)..."
    dotnet run --project (Join-Path $repoRoot "src\Cyberland.Host\Cyberland.Host.csproj") -c Debug
}
finally {
    try {
        Write-Host "Disabling demo in manifest (restoring default publish/skip)..."
        Set-ManifestDisabled -LiteralPath $manifestPath -Disabled $true
    } catch {
        Write-Warning "Could not restore manifest disabled state: $_"
    }
}
