param(
    [string]$Configuration = "Debug",
    [string]$GodotBin,
    [switch]$SkipBuild,
    [switch]$Quiet,
    [switch]$ShowWindow,
    [switch]$CleanupOnly
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$testShimsProj = Join-Path $PSScriptRoot 'MyGodotFSharp.TestShims.csproj'
$projectDir = (Resolve-Path $PSScriptRoot).ProviderPath

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

function Get-LatestGdUnitReport {
    param([string]$ReportsRoot)
    if (-not (Test-Path $ReportsRoot)) { return $null }

    Get-ChildItem -Path $ReportsRoot -Filter 'results.xml' -Recurse -File |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
}

function Write-GdUnitReportSummary {
    param(
        [System.IO.FileInfo]$ReportFile,
        [switch]$Verbose
    )

    if (-not $ReportFile) { return }

    try {
        [xml]$report = Get-Content -Path $ReportFile.FullName
    }
    catch {
        Write-Warning "[shimgen][tests] Failed to read report $($ReportFile.FullName): $($_.Exception.Message)"
        return
    }

    $root = $report.testsuites
    if (-not $root) { return }

    $total = [int]$root.tests
    $failures = [int]$root.failures
    $skipped = [int]$root.skipped
    $time = [double]$root.time
    $summaryColor = if ($failures -gt 0) { 'Red' } else { 'Green' }

    Write-Host ([string]::Format('[shimgen][tests] Report: {0}', $ReportFile.DirectoryName)) -ForegroundColor $summaryColor
    Write-Host ([string]::Format('[shimgen][tests] Total={0} Failures={1} Skipped={2} Time={3}s', $total, $failures, $skipped, $time)) -ForegroundColor $summaryColor

    if (-not $Verbose) { return }

    foreach ($suite in @($root.testsuite)) {
        if (-not $suite) { continue }
        $suiteFailures = [int]$suite.failures + [int]$suite.errors
        $suiteColor = if ($suiteFailures -gt 0) { 'Red' } else { 'Cyan' }
        Write-Host ([string]::Format('  Suite {0}: Tests={1} Failures={2} Skipped={3} Time={4}s', $suite.name, $suite.tests, $suiteFailures, $suite.skipped, $suite.time)) -ForegroundColor $suiteColor
        foreach ($case in @($suite.testcase)) {
            if (-not $case) { continue }
            if ($case.failure -or $case.error) {
                Write-Warning ([string]::Format('    FAIL {0}', $case.name))
            }
            elseif ($Verbose) {
                Write-Host ([string]::Format('    ok {0}', $case.name)) -ForegroundColor DarkGray
            }
        }
    }
}

if (-not $CleanupOnly) {
    if (-not $GodotBin) { $GodotBin = $env:GODOT_BIN }
    if (-not $GodotBin) {
        $defaultCandidate = Join-Path $repoRoot 'Godot' 'godot.exe'
        if (Test-Path $defaultCandidate) { $GodotBin = $defaultCandidate }
    }
    if (-not $GodotBin -or -not (Test-Path $GodotBin)) {
        Write-Error "Godot executable not found. Provide -GodotBin or set GODOT_BIN environment variable."
    }
}

$isVerbose = -not $Quiet

$processKillList = @('godot', 'godot*', 'testhost', 'testhost*', 'vstest*')
Stop-StaleProcesses -Names $processKillList

if ($CleanupOnly) {
    Write-Host "[shimgen][tests] Cleanup-only: terminated stale processes, exiting without build/run." -ForegroundColor Cyan
    return
}

if (-not $SkipBuild) {
    Write-Host "[shimgen][tests] Building TestShims project (configuration=$Configuration)" -ForegroundColor Cyan
    dotnet build $testShimsProj -c $Configuration | Write-Host
}

$binDir = Join-Path $PSScriptRoot ".godot/mono/temp/bin/$Configuration"
$testAsm = Join-Path $binDir 'MyGodotFSharp.TestShims.dll'
if (-not (Test-Path $testAsm)) {
    Write-Error "Compiled test assembly not found at $testAsm (build may have failed)."
}

$godotArgs = @()
if (-not $ShowWindow) {
    $godotArgs += '--headless'
    $godotArgs += '--audio-driver'
    $godotArgs += 'Dummy'
}
$godotArgs += '--quit'
$godotArgs += '--rendering-driver'
$godotArgs += 'opengl3'
$godotArgs += '--'
$godotArgs += '-s'
$godotArgs += 'res://addons/gdUnit4/runners/GdUnit4.dll'
$godotArgs += '-a'

Push-Location $projectDir
try {
    $joinedArgs = [string]::Join(' ', $godotArgs)
    $modeLabel = if ($ShowWindow) { 'windowed' } else { 'headless' }
    Write-Host ([string]::Format('[shimgen][tests] Running Godot {0}: {1} {2}', $modeLabel, $GodotBin, $joinedArgs)) -ForegroundColor Yellow
    $pinfo = New-Object System.Diagnostics.ProcessStartInfo
    $pinfo.FileName = $GodotBin
    if ($pinfo.PSObject.Properties.Match('ArgumentList').Count -gt 0 -and $null -ne $pinfo.ArgumentList) {
        $pinfo.ArgumentList.AddRange($godotArgs)
    }
    else {
        $pinfo.Arguments = [string]::Join(' ', $godotArgs)
    }
    $pinfo.RedirectStandardOutput = $true
    $pinfo.RedirectStandardError = $true
    $pinfo.UseShellExecute = $false
    $pinfo.Environment['GODOT_BIN'] = $GodotBin
    if ($isVerbose) { $pinfo.Environment['GDUNIT_VERBOSE'] = '1' }

    $p = [System.Diagnostics.Process]::Start($pinfo)
    $stdOut = $p.StandardOutput.ReadToEnd()
    $stdErr = $p.StandardError.ReadToEnd()
    $p.WaitForExit()

    Write-Host $stdOut
    if ($stdErr) { Write-Warning $stdErr }

    if ($p.ExitCode -ne 0) {
        Write-Error "Godot test run failed (exit code $($p.ExitCode))"
    }
    else {
        Write-Host '[shimgen][tests] Godot test run succeeded' -ForegroundColor Green
        $reportsRoot = Join-Path $projectDir 'reports'
        $latestReport = Get-LatestGdUnitReport -ReportsRoot $reportsRoot
        if ($latestReport) {
            Write-GdUnitReportSummary -ReportFile $latestReport -Verbose:$isVerbose
        }
        else {
            Write-Warning ([string]::Format('[shimgen][tests] No gdUnit4 report found under {0}', $reportsRoot))
        }
    }
}
finally {
    Stop-StaleProcesses -Names $processKillList
    Pop-Location
}
