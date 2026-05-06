[CmdletBinding()]
param(
    [switch] $NonInteractive,
    [switch] $SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

Write-Host "=== Cyberland Rendering Validation Matrix ==="
Write-Host "Repo: $repoRoot"
Write-Host "Timestamp: $(Get-Date -Format o)"

if (-not $SkipTests) {
    Write-Host ""
    Write-Host "[1/2] Running rendering-focused unit test matrix..."
    dotnet test "tests/Cyberland.Engine.Tests/Cyberland.Engine.Tests.csproj" `
        -c Debug `
        --no-build `
        --filter "EngineShaderSourcesTests|RenderingHelpersTests|TextRenderSystemTests|RenderingCullingTests|DeferredSubmissionAndSceneSystemsTests|LightSubmissionOrderingTests|EngineDefaultGlobalPostProcessTests|CameraTests" `
        /p:CollectCoverage=false
}

Write-Host ""
Write-Host "[2/2] Vulkan runtime smoke matrix:"
Write-Host "  - Launch a render-heavy demo (e.g. idlegold, hdr, mousechase)."
Write-Host "  - Validate these scenarios in one run:"
Write-Host "      * Window resize (small -> large -> restored)"
Write-Host "      * Alt+Enter/fullscreen toggle (if supported)"
Write-Host "      * Camera movement/priority transitions"
Write-Host "      * HUD text-heavy overlay while moving camera"
Write-Host "      * Transparency-heavy scene content (WBOIT path)"
Write-Host "      * Present-mode/frame pacing transitions (if exposed)"
Write-Host "  - Confirm no Vulkan validation errors / fatal diagnostics."

if ($NonInteractive) {
    Write-Host ""
    Write-Host "NonInteractive mode: checklist emitted; interactive demo launch skipped."
    exit 0
}

Write-Host ""
Write-Host "Starting interactive demo smoke run (close the game window to continue)..."
& (Join-Path $PSScriptRoot "Run-CyberlandDemo-Test.ps1") -Demo idlegold
