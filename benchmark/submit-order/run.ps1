#!/usr/bin/env pwsh
# Submit-Order benchmark runner.
#
# Builds the probe suite and the requested arms, then for each arm: starts it, waits for /health,
# runs the black-box probes against it, records the defect score, and stops it. Finally prints a
# side-by-side scorecard. Cross-platform (Windows PowerShell 5.1+ or pwsh on Linux/macOS).
#
#   ./run.ps1                       # both arms (vanilla then trellis)
#   ./run.ps1 -Arms vanilla         # one arm
#   ./run.ps1 -BasePort 6000        # start ports at 6000

param(
    [string[]] $Arms = @('vanilla', 'vanilla-correct', 'trellis'),
    [int]      $BasePort = 5080
)

$ErrorActionPreference = 'Stop'
$root   = $PSScriptRoot
$probes = Join-Path $root 'probes'

function Wait-Health([string] $url, [int] $timeoutSec = 60) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        try {
            $r = Invoke-WebRequest -Uri "$url/health" -TimeoutSec 3 -UseBasicParsing
            if ($r.StatusCode -eq 200) { return $true }
        }
        catch { Start-Sleep -Milliseconds 500 }
    }
    return $false
}

Write-Host 'Building probe suite and arms (Release)...' -ForegroundColor Cyan
dotnet build (Join-Path $probes 'SubmitOrder.Probes.csproj') -c Release | Out-Null
foreach ($arm in $Arms) {
    dotnet build (Join-Path $root "arms/$arm") -c Release | Out-Null
}

$results = @()
$port = $BasePort

foreach ($arm in $Arms) {
    $url    = "http://localhost:$port"
    $armDir = Join-Path $root "arms/$arm"
    $log    = Join-Path $root "$arm.run.log"

    Write-Host ''
    Write-Host "=== Arm: $arm ($url) ===" -ForegroundColor Yellow

    $env:ASPNETCORE_URLS = $url
    $proc = Start-Process -FilePath 'dotnet' `
        -ArgumentList 'run', '-c', 'Release', '--no-build' `
        -WorkingDirectory $armDir `
        -PassThru `
        -RedirectStandardOutput $log `
        -RedirectStandardError "$log.err"

    try {
        if (-not (Wait-Health $url)) {
            Write-Host "Arm '$arm' never became healthy — see $log" -ForegroundColor Red
            $results += [pscustomobject]@{ Arm = $arm; Defects = 'ERR' }
            continue
        }

        & dotnet run --project (Join-Path $probes 'SubmitOrder.Probes.csproj') -c Release --no-build -- --url $url
        $results += [pscustomobject]@{ Arm = $arm; Defects = $LASTEXITCODE }
    }
    finally {
        if ($proc -and -not $proc.HasExited) {
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        }
        Remove-Item $log, "$log.err" -ErrorAction SilentlyContinue
    }

    $port++
}

Write-Host ''
Write-Host '================= SCORECARD =================' -ForegroundColor Cyan
foreach ($r in $results) {
    $color = if ($r.Defects -eq 0) { 'Green' } else { 'Red' }
    Write-Host ('  {0,-9} {1} defect(s) of 5' -f $r.Arm, $r.Defects) -ForegroundColor $color
}
Write-Host '============================================' -ForegroundColor Cyan

# Non-zero exit if any arm has defects, so CI can gate on a clean arm.
$worst = ($results | Where-Object { $_.Defects -is [int] } | Measure-Object -Property Defects -Maximum).Maximum
exit [int]$worst
