[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ProfileReportPath,
    [Parameter(Mandatory = $false)]
    [string] $PerfSummaryPath = "",
    [Parameter(Mandatory = $false)]
    [double] $MaxRunFrameAvgMs = 4.0,
    [Parameter(Mandatory = $false)]
    [double] $MaxUiDocumentFrameAvgMs = 2.0,
    [Parameter(Mandatory = $false)]
    [double] $MaxTextRenderAvgMs = 1.0,
    [Parameter(Mandatory = $false)]
    [double] $MaxRunFrameAvgAllocBytes = 65536.0,
    [Parameter(Mandatory = $false)]
    [double] $MinFps = 2000.0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ProfileReportPath)) {
    throw "Profile report not found: $ProfileReportPath"
}

$rows = @{}
foreach ($line in [System.IO.File]::ReadAllLines($ProfileReportPath)) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    if ($line.StartsWith("scope`t") -or $line.StartsWith("frames=") -or $line.StartsWith("warmupTicks=")) { continue }
    $parts = $line.Split("`t")
    if ($parts.Length -lt 7) { continue }
    $rows[$parts[0]] = [pscustomobject]@{
        AvgMs = [double]$parts[2]
        AvgAllocB = [double]$parts[6]
    }
}

function Require-Scope {
    param([string] $Name)
    if (-not $rows.ContainsKey($Name)) {
        throw "Missing required scope '$Name' in $ProfileReportPath"
    }
    return $rows[$Name]
}

$failures = New-Object System.Collections.Generic.List[string]
$runFrame = Require-Scope -Name "Scheduler.RunFrame"
$uiFrame = Require-Scope -Name "Scheduler.Late.cyberland.engine/ui-document-frame"
$textFrame = Require-Scope -Name "Scheduler.Late.cyberland.engine/text-render"

if ($runFrame.AvgMs -gt $MaxRunFrameAvgMs) {
    $failures.Add("Scheduler.RunFrame avgMs=$($runFrame.AvgMs) > $MaxRunFrameAvgMs")
}
if ($uiFrame.AvgMs -gt $MaxUiDocumentFrameAvgMs) {
    $failures.Add("ui-document-frame avgMs=$($uiFrame.AvgMs) > $MaxUiDocumentFrameAvgMs")
}
if ($textFrame.AvgMs -gt $MaxTextRenderAvgMs) {
    $failures.Add("text-render avgMs=$($textFrame.AvgMs) > $MaxTextRenderAvgMs")
}
if ($runFrame.AvgAllocB -gt $MaxRunFrameAvgAllocBytes) {
    $failures.Add("Scheduler.RunFrame avgAllocB=$($runFrame.AvgAllocB) > $MaxRunFrameAvgAllocBytes")
}

if (-not [string]::IsNullOrWhiteSpace($PerfSummaryPath)) {
    if (-not (Test-Path -LiteralPath $PerfSummaryPath)) {
        $failures.Add("Perf summary path missing: $PerfSummaryPath")
    } else {
        $map = @{}
        foreach ($line in [System.IO.File]::ReadAllLines($PerfSummaryPath)) {
            $parts = $line.Split("=", 2)
            if ($parts.Length -eq 2) { $map[$parts[0].Trim()] = $parts[1].Trim() }
        }
        if ($map.ContainsKey("fps")) {
            $fps = [double]$map["fps"]
            if ($fps -lt $MinFps) {
                $failures.Add("fps=$fps < $MinFps")
            }
        } else {
            $failures.Add("Perf summary does not contain fps=: $PerfSummaryPath")
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Error ("Profile budget check failed:`n- " + ($failures -join "`n- "))
    exit 1
}

Write-Host "Profile budget check passed."
