# Enables one demo mod in its manifest, clears artifacts, runs the host until exit, then restores "disabled": true.
# Use from VS Code / Cursor: Tasks → Cyberland: Test demo (…).
# Manual PowerShell: if execution policy blocks this file, use scripts\Run-CyberlandDemo-Test.cmd or:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Run-CyberlandDemo-Test.ps1 -Demo hdr
# If you kill the task or close the terminal during the game, the finally block may not run — re-disable the mod in manifest.json manually if needed.
[CmdletBinding()]
param(
    # Use non-mandatory with explicit throw so missing -Demo does not open an interactive prompt in CI / tasks.
    [Parameter(Mandatory = $false, Position = 0)]
    [ValidateSet("hdr", "snake", "pong", "brick", "mousechase", "rts", "idlegold", "fonttest", "spritegallery", "whackamole")]
    [string] $Demo,
    # Debug-Instrumented: keeps debug diagnostics/profiler behavior.
    # Release-Perf: use Release for throughput/perf validation.
    [Parameter(Mandatory = $false)]
    [ValidateSet("Debug-Instrumented", "Release-Perf")]
    [string] $RunMode = "Debug-Instrumented",
    # Optional unattended profiler duration. When > 0, the host auto-exits after this many wall-clock seconds.
    [Parameter(Mandatory = $false)]
    [double] $ProfileSeconds = 0,
    # Optional profiler dump path. If omitted while profiling, defaults under artifacts/profiles/.
    [Parameter(Mandatory = $false)]
    [string] $ProfileDumpPath = "",
    # Optional perf summary dump path. If omitted while profiling, defaults under artifacts/profiles/.
    [Parameter(Mandatory = $false)]
    [string] $PerfDumpPath = "",
    # Skip clearing artifacts/ (useful for iterative cycle runs that archive outputs elsewhere).
    [Parameter(Mandatory = $false)]
    [switch] $SkipClearArtifacts,
    # Forces debug frame profiler scopes on (uses CYBERLAND_ENABLE_FRAME_PROFILER=1).
    [Parameter(Mandatory = $false)]
    [switch] $EnableProfiler,
    # Debug-only: enables per-scope allocation columns in the profiler dump (expensive — use for alloc diagnosis only).
    [Parameter(Mandatory = $false)]
    [switch] $ProfileAlloc
)

if ([string]::IsNullOrEmpty($Demo)) {
    throw "Required: -Demo (hdr | snake | pong | brick | mousechase | rts | idlegold | fonttest | spritegallery | whackamole). Example: .\Run-CyberlandDemo-Test.ps1 -Demo hdr"
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
    "rts" { "mods\Cyberland.Demo.Rts\manifest.json" }
    "idlegold" { "mods\Cyberland.Demo.IdleGold\manifest.json" }
    "fonttest" { "mods\Cyberland.Demo.FontTest\manifest.json" }
    "spritegallery" { "mods\Cyberland.Demo.SpriteGallery\manifest.json" }
    "whackamole" { "mods\Cyberland.Demo.WhackAMole\manifest.json" }
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

    if (-not $SkipClearArtifacts.IsPresent) {
        Write-Host "Clearing artifacts/..."
        & (Join-Path $PSScriptRoot "Clear-CyberlandArtifacts.ps1")
    } else {
        Write-Host "Skipping artifacts clear (-SkipClearArtifacts)."
    }

    $config = if ($RunMode -eq "Release-Perf") { "Release" } else { "Debug" }
    $hostArgs = @()
    if ($ProfileSeconds -gt 0) {
        $hostArgs += "--profile-seconds=$ProfileSeconds"
        if ([string]::IsNullOrWhiteSpace($ProfileDumpPath)) {
            $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
            $profileDir = Join-Path $repoRoot "artifacts\profiles"
            New-Item -ItemType Directory -Path $profileDir -Force | Out-Null
            $ProfileDumpPath = Join-Path $profileDir "$Demo-$RunMode-$stamp.txt"
        }
        $hostArgs += "--profile-dump=$ProfileDumpPath"
        if ([string]::IsNullOrWhiteSpace($PerfDumpPath)) {
            $PerfDumpPath = [System.IO.Path]::ChangeExtension($ProfileDumpPath, ".perf.txt")
        }
        $hostArgs += "--perf-dump=$PerfDumpPath"
    }

    if ($ProfileAlloc.IsPresent) {
        $hostArgs += "--profile-alloc"
    }

    if ($EnableProfiler.IsPresent -and $config -ne "Debug") {
        Write-Warning "EnableProfiler is meaningful for Debug builds; Release compiles frame profiler scopes out."
    }

    Write-Host "Running Cyberland.Host ($RunMode, -c $config; close the game to continue)..."
    if ($hostArgs.Count -gt 0) {
        Write-Host "Host args: $($hostArgs -join ' ')"
    }
    $envPrefix = ""
    if ($EnableProfiler.IsPresent) {
        $env:CYBERLAND_ENABLE_FRAME_PROFILER = "1"
        $envPrefix = "CYBERLAND_ENABLE_FRAME_PROFILER=1 "
    }

    dotnet run --project (Join-Path $repoRoot "src\Cyberland.Host\Cyberland.Host.csproj") -c $config -- @hostArgs

    if ($ProfileSeconds -gt 0 -and -not [string]::IsNullOrWhiteSpace($ProfileDumpPath)) {
        Write-Host "Profiler report: $ProfileDumpPath"
    }
    if ($ProfileSeconds -gt 0 -and -not [string]::IsNullOrWhiteSpace($PerfDumpPath)) {
        Write-Host "Perf summary: $PerfDumpPath"
    }
}
finally {
    try {
        Write-Host "Disabling demo in manifest (restoring default publish/skip)..."
        Set-ManifestDisabled -LiteralPath $manifestPath -Disabled $true
    } catch {
        Write-Warning "Could not restore manifest disabled state: $_"
    }
    if ($EnableProfiler.IsPresent) {
        Remove-Item Env:CYBERLAND_ENABLE_FRAME_PROFILER -ErrorAction SilentlyContinue
    }
}
