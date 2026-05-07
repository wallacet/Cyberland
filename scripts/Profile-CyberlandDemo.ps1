# Enables one demo mod manifest, runs the host with CPU profile flags, restores "disabled": true.
# Example: .\scripts\Profile-CyberlandDemo.ps1 -Demo idlegold -ProfileSeconds 10 -ProfileDump artifacts/profiles/idlegold.txt
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false, Position = 0)]
    [ValidateSet("hdr", "snake", "pong", "brick", "mousechase", "idlegold")]
    [string] $Demo = "idlegold",

    [Parameter(Mandatory = $false)]
    [double] $ProfileSeconds = 10.0,

    [Parameter(Mandatory = $false)]
    [string] $ProfileDump = "artifacts/profiles/demo-profile.txt",

    [Parameter(Mandatory = $false)]
    [string] $PerfDump = "artifacts/profiles/demo-perf.txt"
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
    "idlegold" { "mods\Cyberland.Demo.IdleGold\manifest.json" }
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

    $dumpPath = if ([System.IO.Path]::IsPathRooted($ProfileDump)) { $ProfileDump } else { Join-Path $repoRoot $ProfileDump }
    $perfPath = if ([System.IO.Path]::IsPathRooted($PerfDump)) { $PerfDump } else { Join-Path $repoRoot $PerfDump }
    foreach ($path in @($dumpPath, $perfPath)) {
        $dir = Split-Path -Parent $path
        if (-not [string]::IsNullOrEmpty($dir) -and -not (Test-Path -LiteralPath $dir)) {
            New-Item -ItemType Directory -Path $dir | Out-Null
        }
    }

    Write-Host "Profiling ${ProfileSeconds}s → $dumpPath (perf: $perfPath) ..."
    dotnet run --project (Join-Path $repoRoot "src\Cyberland.Host\Cyberland.Host.csproj") -c Debug -- `
        "--profile-seconds=$ProfileSeconds" `
        "--profile-dump=$dumpPath" `
        "--perf-dump=$perfPath"
}
finally {
    try {
        Write-Host "Disabling demo in manifest (restoring default publish/skip)..."
        Set-ManifestDisabled -LiteralPath $manifestPath -Disabled $true
    }
    catch {
        Write-Warning "Could not restore manifest disabled state: $_"
    }
}
