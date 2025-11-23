param(
    [string]$Configuration = "Debug",
    [switch]$SkipBuild,
    [switch]$Quiet,
    [switch]$CleanupOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$testProject = Join-Path $PSScriptRoot 'MyGodotFSharp.TestShims.csproj'
if (-not (Test-Path $testProject)) {
    throw "[shimgen][tests] Expected test project at $testProject."
}

function Stop-StaleProcesses {
    param([string[]]$Names)
    foreach ($name in $Names) {
        $processes = Get-Process -Name $name -ErrorAction SilentlyContinue
        foreach ($proc in $processes) {
            try {
                if ($proc.HasExited) { continue }
            }
            catch {
                continue
            }

            $summary = "$($proc.ProcessName) (Id=$($proc.Id))"
            try {
                Write-Host "[shimgen][tests] Killing stale process $summary" -ForegroundColor DarkYellow
                $proc.Kill()
                $null = $proc.WaitForExit(5000)
            }
            catch {
                Write-Warning "[shimgen][tests] Failed to kill $summary : $($_.Exception.Message)"
            }
        }
    }
}

function Write-TestSummary {
    param([string]$TrxPath)

    if (-not (Test-Path -LiteralPath $TrxPath)) {
        Write-Warning "[shimgen][tests] Expected TRX results at $TrxPath but none found."
        return
    }

    try {
        [xml]$trx = Get-Content -LiteralPath $TrxPath
    }
    catch {
        Write-Warning "[shimgen][tests] Failed to parse TRX results: $($_.Exception.Message)"
        return
    }

    $results = @($trx.TestRun.Results.UnitTestResult)
    if ($results.Count -eq 0) {
        Write-Host "[shimgen][tests] No test results found in TRX." -ForegroundColor Yellow
        return
    }

    Write-Host "[shimgen][tests] Test case results:" -ForegroundColor Cyan
    foreach ($result in $results) {
        $status = $result.outcome
        $name = $result.testName
        $duration = $result.duration

        $color = 'Gray'
        switch ($status.ToLowerInvariant()) {
            'passed' { $color = 'Green' }
            'failed' { $color = 'Red' }
            'skipped' { $color = 'Yellow' }
        }

        $durationText = if ($duration) { $duration } else { 'n/a' }
        Write-Host ("  [{0}] {1} ({2})" -f $status.ToUpperInvariant(), $name, $durationText) -ForegroundColor $color

        $outputNode = $null
        if ($result.PSObject.Properties.Name -contains 'Output') {
            $outputNode = $result.Output
        }

        $errorInfo = $null
        if ($outputNode -and $outputNode.PSObject.Properties.Name -contains 'ErrorInfo') {
            $errorInfo = $outputNode.ErrorInfo
        }

        if ($errorInfo) {
            $message = $errorInfo.Message
            if ($message) {
                Write-Host ("      Message: {0}" -f ($message.Trim())) -ForegroundColor $color
            }

            $stackTrace = $errorInfo.StackTrace
            if ($stackTrace) {
                Write-Host ("      StackTrace: {0}" -f ($stackTrace.Trim())) -ForegroundColor DarkGray
            }
        }
    }
}

$processKillList = @('godot', 'godot*', 'testhost', 'testhost*', 'vstest*')
Stop-StaleProcesses -Names $processKillList

if ($CleanupOnly) {
    Write-Host "[shimgen][tests] Cleanup-only: terminated stale processes, exiting without run." -ForegroundColor Cyan
    return
}

Push-Location $PSScriptRoot
try {
    $resultsDir = Join-Path $PSScriptRoot 'TestResults'
    if (-not (Test-Path -LiteralPath $resultsDir)) {
        $null = New-Item -ItemType Directory -Path $resultsDir | Out-Null
    }

    $resultsPath = Join-Path $resultsDir 'Latest.trx'
    if (Test-Path -LiteralPath $resultsPath) {
        Remove-Item -LiteralPath $resultsPath -Force
    }

    $dotnetArgs = @('test', $testProject, '--configuration', $Configuration, '--logger', "trx;LogFileName=$resultsPath")
    if ($SkipBuild) { $dotnetArgs += '--no-build' }
    if ($Quiet) { $dotnetArgs += @('--verbosity', 'quiet') }

    Write-Host "[shimgen][tests] Running dotnet $($dotnetArgs -join ' ')" -ForegroundColor Yellow
    dotnet @dotnetArgs
    $exitCode = $LASTEXITCODE

    if (Test-Path -LiteralPath $resultsPath) {
        Write-TestSummary -TrxPath $resultsPath
    }

    if ($exitCode -ne 0) {
        throw "[shimgen][tests] dotnet test failed (exit code $exitCode)."
    }
    Write-Host '[shimgen][tests] dotnet test succeeded' -ForegroundColor Green
}
finally {
    Pop-Location
    Stop-StaleProcesses -Names $processKillList
}
